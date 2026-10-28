-- ТОЛЬКО ЧТЕНИЕ. Можно выполнять блоками в pgAdmin/DBeaver.
-- Нужны logistics_points, products, warehouse_products.
-- В тестовых примерах ПВЗ обозначается type='pvz'; сначала проверьте свои значения.

-- 1. Какие типы реально используются, сколько точек активно.
SELECT type, count(*) AS total, count(*) FILTER (WHERE is_active) AS active
FROM logistics_points GROUP BY type ORDER BY type;

-- 2. Список и сведения о точках, включая неактивные (так работает текущий API).
SELECT id, name, type, city, address, latitude, longitude, is_active
FROM logistics_points ORDER BY name, id;

-- 3. Поиск как в сервисе. Меняйте query и point_type; NULL означает отсутствие фильтра.
WITH input AS (
    SELECT 'Москва'::text AS query, NULL::text AS point_type
), search AS (
    SELECT '%' || replace(replace(replace(btrim(query), E'\\', E'\\\\'), '%', E'\\%'), '_', E'\\_') || '%' AS pattern,
           NULLIF(btrim(query), '') AS query, NULLIF(btrim(point_type), '') AS point_type
    FROM input
)
SELECT p.id, p.name, p.type, p.city, p.address, p.is_active
FROM logistics_points p CROSS JOIN search s
WHERE (s.point_type IS NULL OR p.type = s.point_type)
  AND (s.query IS NULL OR p.name ILIKE s.pattern ESCAPE E'\\'
       OR p.city ILIKE s.pattern ESCAPE E'\\' OR p.address ILIKE s.pattern ESCAPE E'\\')
ORDER BY p.name, p.id;

-- 4. Товары выбранной точки. Замените 1 на существующий ID склада или ПВЗ.
-- Это SQL-проверка; отдельного API /{id}/products пока нет.
WITH input AS (SELECT 1::bigint AS point_id, NULL::text AS product_query),
search AS (
    SELECT point_id, NULLIF(btrim(product_query), '') AS product_query,
           '%' || replace(replace(replace(btrim(product_query), E'\\', E'\\\\'), '%', E'\\%'), '_', E'\\_') || '%' AS pattern
    FROM input
)
SELECT p.id AS point_id, p.name AS point_name, p.type,
       product.id AS product_id, product.name AS product_name, stock.quantity
FROM warehouse_products stock
JOIN logistics_points p ON p.id = stock.logistics_points_id
JOIN products product ON product.id = stock.product_id
CROSS JOIN search s
WHERE p.id = s.point_id
  AND (s.product_query IS NULL OR product.id::text = s.product_query
       OR product.name ILIKE s.pattern ESCAPE E'\\')
ORDER BY product.name, product.id;

-- 5. Сводка: различаем положительные остатки и строки с количеством 0.
SELECT p.id, p.name, p.type,
       count(stock.id) FILTER (WHERE stock.quantity > 0) AS products_in_stock,
       coalesce(sum(stock.quantity), 0) AS total_units
FROM logistics_points p
LEFT JOIN warehouse_products stock ON stock.logistics_points_id = p.id
GROUP BY p.id, p.name, p.type ORDER BY p.name, p.id;

-- 6. Качество справочника: в норме пустой результат.
-- Эти правила пока не все закреплены CHECK-ограничениями в БД.
SELECT id, name, type, city, address, latitude, longitude
FROM logistics_points
WHERE btrim(name) = '' OR btrim(type) = '' OR btrim(city) = '' OR btrim(address) = ''
   OR latitude NOT BETWEEN -90 AND 90 OR longitude NOT BETWEEN -180 AND 180;

-- 7. Ошибки остатков: в норме пустые результаты.
SELECT logistics_points_id, product_id, count(*) AS duplicate_rows
FROM warehouse_products GROUP BY logistics_points_id, product_id HAVING count(*) > 1;

SELECT stock.id, stock.logistics_points_id, stock.product_id, stock.quantity
FROM warehouse_products stock
LEFT JOIN logistics_points p ON p.id = stock.logistics_points_id
LEFT JOIN products product ON product.id = stock.product_id
WHERE stock.quantity < 0 OR p.id IS NULL OR product.id IS NULL;

-- 8. Наличие уникальности, связей и CHECK у складских остатков.
SELECT conname, pg_get_constraintdef(oid) AS definition
FROM pg_constraint WHERE conrelid = 'public.warehouse_products'::regclass ORDER BY conname;
SELECT indexname, indexdef FROM pg_indexes
WHERE schemaname = 'public' AND tablename = 'warehouse_products' ORDER BY indexname;
