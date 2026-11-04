-- PostgreSQL, схема SimplifyLogisticsMvp.
-- Запускайте файл целиком в SQL-редакторе pgAdmin или DBeaver.
-- Каждый запуск ДОБАВЛЯЕТ новый набор. Существующие данные и пользователи сохраняются.
-- При ошибке выполните ROLLBACK; перед повторным запуском файла.

BEGIN;

DO $seed$
DECLARE
    -- Объём тестовых данных: меняйте эти четыре значения.
    v_product_count integer := 500;
    v_warehouse_count integer := 6;
    v_pickup_count integer := 12;
    v_order_count integer := 100;

    v_run text := substr(md5(clock_timestamp()::text || random()::text), 1, 12);
    v_cities text[] := ARRAY['Москва', 'Тула', 'Ярославль', 'Казань', 'Калуга', 'Тверь'];
    v_latitudes numeric[] := ARRAY[55.751244, 54.193122, 57.626074, 55.796127, 54.513845, 56.858721];
    v_longitudes numeric[] := ARRAY[37.618423, 37.617348, 39.884470, 49.106405, 36.261215, 35.917596];
    v_models text[] := ARRAY['Север', 'Лофт', 'Сканди', 'Модуль', 'Атлас', 'Орион', 'Вектор', 'Альфа'];
    v_colors text[] := ARRAY['Белый', 'Дуб сонома', 'Венге', 'Графит', 'Орех'];
    v_users text[];
    v_warehouses bigint[] := ARRAY[]::bigint[];
    v_pickups bigint[] := ARRAY[]::bigint[];
    v_products bigint[] := ARRAY[]::bigint[];
    v_all_points bigint[];
    v_creator text;
    v_responsible text;
    v_point_id bigint;
    v_product_id bigint;
    v_order_id bigint;
    v_item_id bigint;
    v_physical_id bigint;
    v_source_id bigint;
    v_destination_id bigint;
    v_pack record;
    v_product record;
    v_index integer;
    v_city_index integer;
    v_phase integer;
    v_units integer;
    v_stock integer;
    v_box_index integer;
    v_unit_index integer;
    v_loaded boolean;
    v_created_at timestamptz;
    v_loaded_at timestamptz;
    v_unloaded_at timestamptz;
BEGIN
    IF v_product_count < 1 OR v_warehouse_count < 1 OR v_pickup_count < 1 OR v_order_count < 0 THEN
        RAISE EXCEPTION 'Нужны хотя бы один товар, склад и ПВЗ; число рейсов не может быть отрицательным.';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20261104101802_SimplifyLogisticsMvp'
    ) THEN
        RAISE EXCEPTION 'Сначала примените миграцию SimplifyLogisticsMvp.';
    END IF;
    SELECT array_agg("Id" ORDER BY "Id") INTO v_users FROM "AspNetUsers";
    IF coalesce(cardinality(v_users), 0) = 0 THEN
        RAISE EXCEPTION 'Нет пользователей. Сначала зарегистрируйте пользователя через приложение.';
    END IF;

    -- Точки: склады активны, последний ПВЗ (если их несколько) неактивен.
    FOR v_index IN 1..v_warehouse_count LOOP
        v_city_index := 1 + (v_index - 1) % cardinality(v_cities);
        INSERT INTO logistics_points(name, type, address, city, latitude, longitude, is_active)
        VALUES (
            format('Склад %s — %s', v_index, v_cities[v_city_index]), 'warehouse',
            format('ул. Складская, %s', v_index), v_cities[v_city_index],
            round(v_latitudes[v_city_index] + (random()::numeric - 0.5) / 10, 6),
            round(v_longitudes[v_city_index] + (random()::numeric - 0.5) / 10, 6), true
        ) RETURNING id INTO v_point_id;
        v_warehouses := array_append(v_warehouses, v_point_id);
    END LOOP;
    FOR v_index IN 1..v_pickup_count LOOP
        v_city_index := 1 + (v_index - 1) % cardinality(v_cities);
        INSERT INTO logistics_points(name, type, address, city, latitude, longitude, is_active)
        VALUES (
            format('ПВЗ %s — %s', v_index, v_cities[v_city_index]), 'pvz',
            format('ул. Центральная, %s', v_index), v_cities[v_city_index],
            round(v_latitudes[v_city_index] + (random()::numeric - 0.5) / 10, 6),
            round(v_longitudes[v_city_index] + (random()::numeric - 0.5) / 10, 6),
            NOT (v_pickup_count > 1 AND v_index = v_pickup_count)
        ) RETURNING id INTO v_point_id;
        v_pickups := array_append(v_pickups, v_point_id);
    END LOOP;
    v_all_points := v_warehouses || v_pickups;

    -- Один шкаф = две одинаковые коробки «Дверь», одна стенка и один крепёж.
    FOR v_index IN 1..v_product_count LOOP
        INSERT INTO products(name, url)
        VALUES (
            format('Шкаф %s %s — %s',
                v_models[1 + floor(random() * cardinality(v_models))::integer],
                lpad(v_index::text, 4, '0'),
                v_colors[1 + floor(random() * cardinality(v_colors))::integer]),
            format('https://example.test/products/%s/%s', v_run, v_index)
        ) RETURNING id INTO v_product_id;
        v_products := array_append(v_products, v_product_id);
        INSERT INTO boxes(product_id, name, quantity_per_product,
            width_cm, height_cm, length_cm, weight_kg, is_fragile)
        VALUES
            (v_product_id, 'Дверь', 2, round((40 + random() * 40)::numeric, 2),
                round((4 + random() * 4)::numeric, 2), round((150 + random() * 60)::numeric, 2),
                round((8 + random() * 10)::numeric, 3), random() < 0.2),
            (v_product_id, 'Задняя стенка', 1, round((60 + random() * 40)::numeric, 2),
                round((1 + random() * 2)::numeric, 2), round((150 + random() * 60)::numeric, 2),
                round((3 + random() * 4)::numeric, 3), false),
            (v_product_id, 'Пакет крепежа', 1, 15, 8, 25,
                round((0.3 + random())::numeric, 3), false);

        -- Свободный запас: 2–12 шкафов в 1–3 случайных складах.
        FOR v_point_id IN
            SELECT point FROM unnest(v_warehouses) AS source(point)
            ORDER BY random() LIMIT least(v_warehouse_count, 1 + floor(random() * 3)::integer)
        LOOP
            v_stock := 2 + floor(random() * 11)::integer;
            INSERT INTO physical_boxes(box_id, url, current_point_id, current_order_item_id, status)
            SELECT pack.id,
                format('https://example.test/boxes/%s/stock/%s/%s/%s', v_run, v_point_id, pack.id, number),
                v_point_id, NULL, 1
            FROM boxes pack
            CROSS JOIN LATERAL generate_series(1, v_stock * pack.quantity_per_product) AS series(number)
            WHERE pack.product_id = v_product_id;
        END LOOP;
    END LOOP;

    -- Фазы: 0 — новый; 1 — частично погружен; 2 — в пути; 3 — доставлен.
    -- Названия статусов рейса — тестовые значения: в текущей БД это строка.
    FOR v_index IN 1..v_order_count LOOP
        v_phase := (v_index - 1) % 4;
        v_source_id := v_warehouses[1 + floor(random() * cardinality(v_warehouses))::integer];
        v_creator := v_users[1 + floor(random() * cardinality(v_users))::integer];
        v_responsible := CASE WHEN v_phase = 0 AND v_index % 8 = 1 THEN NULL
            ELSE v_users[1 + floor(random() * cardinality(v_users))::integer] END;
        v_created_at := CASE v_phase
            WHEN 0 THEN current_timestamp - (random() * 24) * interval '1 hour'
            WHEN 1 THEN current_timestamp - (60 + random() * 60) * interval '1 minute'
            WHEN 2 THEN current_timestamp - (2 + random() * 4) * interval '1 hour'
            ELSE current_timestamp - (1 + random() * 29) * interval '1 day' END;

        INSERT INTO delivery_orders(user_id, responsible_user_id, car_plate, contact_phone,
            cargo_length_cm, cargo_width_cm, cargo_height_cm, max_load_kg,
            source_point_id, status, created_at)
        VALUES (v_creator, v_responsible,
            CASE WHEN v_phase = 0 THEN NULL ELSE format('А%sВС777', 100 + floor(random() * 900)::integer) END,
            CASE WHEN v_phase = 0 THEN NULL ELSE '+7999' || lpad(floor(random() * 10000000)::bigint::text, 7, '0') END,
            CASE WHEN v_phase = 0 THEN NULL ELSE 600 END,
            CASE WHEN v_phase = 0 THEN NULL ELSE 240 END,
            CASE WHEN v_phase = 0 THEN NULL ELSE 250 END,
            CASE WHEN v_phase = 0 THEN NULL ELSE 3500 END,
            v_source_id, (ARRAY['new', 'loading', 'in_transit', 'delivered'])[v_phase + 1], v_created_at
        ) RETURNING id INTO v_order_id;

        -- 1–3 разных модели товара на рейс, по 2–5 шкафов каждой модели.
        FOR v_product IN
            SELECT product FROM unnest(v_products) AS candidates(product)
            ORDER BY random() LIMIT least(v_product_count, 1 + floor(random() * 3)::integer)
        LOOP
            v_units := 2 + floor(random() * 4)::integer;
            SELECT id INTO STRICT v_destination_id FROM logistics_points
            WHERE id = ANY(v_pickups) AND is_active ORDER BY random() LIMIT 1;
            FOR v_pack IN SELECT id, quantity_per_product FROM boxes
                WHERE product_id = v_product.product ORDER BY id
            LOOP
                INSERT INTO delivery_order_items(order_id, box_id, destination_point_id, quantity)
                VALUES (v_order_id, v_pack.id, v_destination_id, v_units * v_pack.quantity_per_product)
                RETURNING id INTO v_item_id;

                FOR v_box_index IN 1..(v_units * v_pack.quantity_per_product) LOOP
                    v_unit_index := 1 + (v_box_index - 1) / v_pack.quantity_per_product;
                    v_loaded := v_phase IN (2, 3) OR (v_phase = 1 AND v_unit_index <= v_units / 2);
                    INSERT INTO physical_boxes(box_id, url, current_point_id, current_order_item_id, status)
                    VALUES (v_pack.id,
                        format('https://example.test/boxes/%s/trip/%s/%s/%s',
                            v_run, v_order_id, v_item_id, v_box_index),
                        CASE WHEN v_phase = 3 THEN v_destination_id
                            WHEN v_loaded THEN NULL ELSE v_source_id END,
                        CASE WHEN v_phase IN (0, 3) THEN NULL ELSE v_item_id END,
                        CASE WHEN v_loaded AND v_phase <> 3 THEN 2 ELSE 1 END)
                    RETURNING id INTO v_physical_id;

                    IF v_loaded THEN
                        v_loaded_at := v_created_at + interval '15 minutes' + v_box_index * interval '1 second';
                        INSERT INTO box_movements(physical_box_id, order_item_id, operation, point_id, occurred_at, user_id)
                        VALUES (v_physical_id, v_item_id, 1, v_source_id, v_loaded_at, v_responsible);
                    END IF;
                    IF v_phase = 3 THEN
                        v_unloaded_at := v_created_at + interval '4 hours' + v_box_index * interval '1 second';
                        INSERT INTO box_movements(physical_box_id, order_item_id, operation, point_id, occurred_at, user_id)
                        VALUES (v_physical_id, v_item_id, 2, v_destination_id, v_unloaded_at, v_responsible);
                    END IF;
                END LOOP;
            END LOOP;
        END LOOP;
    END LOOP;

    -- Фактический остаток полных товаров во всех новых точках (включая нулевые).
    -- Например, 6 дверей + 3 стенки + 2 крепежа = min(6/2, 3/1, 2/1) = 2 шкафа.
    -- Резерв остаётся в складском остатке до погрузки. Коробки в машине исключены.
    WITH pieces AS (
        SELECT current_point_id, box_id, count(*) AS quantity
        FROM physical_boxes WHERE current_point_id = ANY(v_all_points)
        GROUP BY current_point_id, box_id
    ), stocks AS (
        SELECT point.id AS point_id, pack.product_id,
            min(coalesce(pieces.quantity, 0) / pack.quantity_per_product)::integer AS quantity
        FROM unnest(v_all_points) AS point(id)
        CROSS JOIN boxes pack
        LEFT JOIN pieces ON pieces.current_point_id = point.id AND pieces.box_id = pack.id
        WHERE pack.product_id = ANY(v_products)
        GROUP BY point.id, pack.product_id
    )
    INSERT INTO warehouse_products(logistics_points_id, product_id, quantity)
    SELECT point_id, product_id, quantity FROM stocks;

    RAISE NOTICE 'Набор % добавлен: % товаров, % складов, % ПВЗ, % рейсов. Пользователи сохранены.',
        v_run, v_product_count, v_warehouse_count, v_pickup_count, v_order_count;
END;
$seed$;

COMMIT;

-- Общие количества после выполнения (включая данные предыдущих запусков).
SELECT 'products' AS table_name, count(*) AS row_count FROM products
UNION ALL SELECT 'boxes', count(*) FROM boxes
UNION ALL SELECT 'physical_boxes', count(*) FROM physical_boxes
UNION ALL SELECT 'warehouse_products', count(*) FROM warehouse_products
UNION ALL SELECT 'logistics_points', count(*) FROM logistics_points
UNION ALL SELECT 'delivery_orders', count(*) FROM delivery_orders
UNION ALL SELECT 'delivery_order_items', count(*) FROM delivery_order_items
UNION ALL SELECT 'box_movements', count(*) FROM box_movements;
