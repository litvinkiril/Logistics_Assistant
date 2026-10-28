-- Только чтение. Для базы, обновлённой до AddDeliveryOrderBoxes.
-- Каждая строка с problem_count > 0 требует разбора перед новой миграцией.
SELECT 'duplicate physical box URLs' AS issue, count(*) AS problem_count
FROM (SELECT url FROM physical_boxes GROUP BY url HAVING count(*) > 1) d
UNION ALL
SELECT 'empty physical box URLs', count(*) FROM physical_boxes WHERE length(btrim(url)) = 0
UNION ALL
SELECT 'duplicate product URLs', count(*)
FROM (SELECT url FROM products WHERE url IS NOT NULL GROUP BY url HAVING count(*) > 1) d
UNION ALL
SELECT 'duplicate stock rows', count(*)
FROM (SELECT logistics_points_id,product_id FROM warehouse_products
      GROUP BY logistics_points_id,product_id HAVING count(*) > 1) d
UNION ALL
SELECT 'negative stock', count(*) FROM warehouse_products WHERE quantity < 0
UNION ALL
SELECT 'duplicate packaging numbers', count(*)
FROM (SELECT product_id,box_number FROM boxes GROUP BY product_id,box_number HAVING count(*) > 1) d
UNION ALL
SELECT 'invalid packaging dimensions/number/weight', count(*) FROM boxes
WHERE box_number <= 0 OR width_cm <= 0 OR height_cm <= 0 OR length_cm <= 0 OR weight_kg <= 0
UNION ALL
SELECT 'invalid need quantities', count(*) FROM need_to_deliver WHERE quantity <= 0
UNION ALL
SELECT 'needs referencing missing rows', count(*) FROM need_to_deliver n
WHERE (n.product_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM products p WHERE p.id=n.product_id))
   OR (n.box_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM boxes b WHERE b.id=n.box_id))
   OR (n.logistic_point_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM logistics_points p WHERE p.id=n.logistic_point_id))
UNION ALL
SELECT 'orders referencing missing users', count(*) FROM delivery_orders o
WHERE NOT EXISTS(SELECT 1 FROM "AspNetUsers" u WHERE u."Id"=o.user_id)
UNION ALL
SELECT 'order boxes referencing missing rows', count(*) FROM delivery_order_boxes b
WHERE NOT EXISTS(SELECT 1 FROM delivery_orders o WHERE o.id=b.order_id)
   OR NOT EXISTS(SELECT 1 FROM physical_boxes p WHERE p.id=b.physical_box_id)
   OR NOT EXISTS(SELECT 1 FROM logistics_points p WHERE p.id=b.point_id)
UNION ALL
SELECT 'boxes assigned to multiple active orders', count(*)
FROM (SELECT physical_box_id FROM delivery_order_boxes WHERE status <> 4
      GROUP BY physical_box_id HAVING count(*) > 1) d;
