-- Проверка склада, ПВЗ и остатков на временных записях.
-- Запускайте ЦЕЛИКОМ, в отдельной сессии без незавершённых операций.
-- Требуется применённая AddDeliveryPlanningAndBoxTracking (уникальность и quantity >= 0).
-- В конце ROLLBACK: записи не сохраняются. Явные отрицательные ID не двигают sequences.
-- При ошибке выполните ROLLBACK; результат проверки считается неуспешным.
BEGIN;

DO $test$
DECLARE
    warehouse_id bigint := -(extract(epoch FROM clock_timestamp()) * 1000000)::bigint;
    pickup_id bigint := warehouse_id - 1;
    product_id_value bigint := warehouse_id;
    missing_id bigint := warehouse_id - 99;
    actual bigint;
BEGIN
    IF EXISTS (SELECT 1 FROM logistics_points WHERE id = missing_id) THEN
        RAISE EXCEPTION 'Test ID collision; rerun the script.';
    END IF;

    INSERT INTO logistics_points(id,name,type,address,city,latitude,longitude,is_active)
    VALUES (warehouse_id,'ТЕСТ Склад 100%','warehouse','Тестовая улица, 1','Москва',55.75,37.61,true),
           (pickup_id,'ТЕСТ ПВЗ_A','pvz','Тестовая улица, 2','Москва',55.76,37.62,false);
    INSERT INTO products(id,name,url) VALUES(product_id_value,'ТЕСТ Товар',NULL);
    INSERT INTO warehouse_products(id,logistics_points_id,product_id,quantity)
    VALUES (warehouse_id,warehouse_id,product_id_value,12),
           (pickup_id,pickup_id,product_id_value,3);

    SELECT count(*) INTO actual FROM logistics_points
    WHERE id IN (warehouse_id,pickup_id) AND type='pvz' AND NOT is_active;
    IF actual <> 1 THEN RAISE EXCEPTION 'Pickup point details check failed.'; END IF;

    SELECT count(*) INTO actual FROM logistics_points
    WHERE id IN (warehouse_id,pickup_id) AND name ILIKE E'%\\%%' ESCAPE E'\\';
    IF actual <> 1 THEN RAISE EXCEPTION 'Literal percent search failed.'; END IF;

    SELECT sum(quantity) INTO actual FROM warehouse_products WHERE product_id=product_id_value;
    IF actual <> 15 THEN RAISE EXCEPTION 'Warehouse/pickup stock check failed.'; END IF;

    BEGIN
        INSERT INTO warehouse_products(id,logistics_points_id,product_id,quantity)
        VALUES (warehouse_id-2,warehouse_id,product_id_value,1);
        RAISE EXCEPTION 'Missing unique constraint on point/product.';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;

    BEGIN
        UPDATE warehouse_products SET quantity=-1 WHERE id=warehouse_id;
        RAISE EXCEPTION 'Missing nonnegative quantity constraint.';
    EXCEPTION WHEN check_violation THEN NULL;
    END;

    BEGIN
        UPDATE warehouse_products SET logistics_points_id=missing_id WHERE id=warehouse_id;
        RAISE EXCEPTION 'Missing foreign key to logistics_points.';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;

    BEGIN
        DELETE FROM logistics_points WHERE id=warehouse_id;
        RAISE EXCEPTION 'Point with stock was unexpectedly deleted.';
    EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN NULL;
    END;

    RAISE NOTICE 'PASS: warehouse, pickup point, stock and database constraints.';
END;
$test$;

ROLLBACK;
