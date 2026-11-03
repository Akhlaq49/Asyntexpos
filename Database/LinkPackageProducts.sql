-- Run again any time (safe): it only fills in items that are not linked yet, and adds the Matter Gateway item once.
-- Run after the price list products are in the company's product list (e.g. Create Product > Bulk Add).
-- Smartinn_Product_Mapping.xlsx: link the solution package items to the inventory products (product list = standard).
-- Per company (tenant), by name; only items that are not linked yet; the lowest-Id live product when a name repeats.
-- Package item names are kept - the drawing rules refer to them.

CREATE TEMPORARY TABLE map_item_product (item VARCHAR(150), product VARCHAR(255)) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
INSERT INTO map_item_product VALUES
 ('Smart Panel Echo',                  'Harmony HPAD Echo'),
 ('Smart Panel Q2',                    'Harmony HPAD Q2'),
 ('Wireless Zigbee Gateway',           'Zigbee Gate Way Controller'),
 ('Smart Lock i5',                     'Innshield Smart Door Lock i5'),
 ('Smart Lock i9 Pro',                 'Innshield Smart Door Lock i9 Pro'),
 ('Piano Switch 1 Gang',               'Innswitch Piano 1 Gang White'),
 ('Piano Switch 2 Gang',               'Innswitch Piano 2 Gang White'),
 ('Piano Switch 3 Gang',               'Innswitch Piano 3 Gang White'),
 ('Smart Curtain Motor 1.5Nm',         'Motility Smart Curtain Motor 1.5Nm'),
 ('Curtain Rail 1 meter',              'Curtain Track/1m'),
 ('Smart Roller Blind 1.8Nm',          'Motility Tubular Motor 1.8Nm'),
 ('Smart Roller Blind 6Nm',            'Motility Tubular Motor 6Nm'),
 ('Anti Glare Smart Light 7w',         'Zigbee Spot Light 7W'),
 ('Anti Glare Smart Light 10w',        'Zigbee Anti Glare Spot Light 10W'),
 ('Door Window Sensor',                'Door Window Sensor'),
 ('Motion Sensor',                     'Zigbee Motion PIR Sensor'),
 ('Water Leakage Sensor',              'water Leakge sensor'),
 ('Smoke Sensor',                      'Smoke Sensor'),
 ('Temp and Humidity Sensor',          'Temp and humidity sensor'),
 ('Gas Sensor',                        'Combistable Gas sensor'),
 ('IR Remote Control',                 'IR Blaster'),
 ('Intercom Outdoor Unit Gate Keeper', 'Touchmate / Video Door Bell');

UPDATE PackageItems i
JOIN map_item_product m ON m.item = i.Name COLLATE utf8mb4_unicode_ci
JOIN (SELECT TenantId, ProductName, MIN(Id) AS Id FROM Products WHERE IsDeleted = 0 GROUP BY TenantId, ProductName) p
  ON p.TenantId = i.TenantId AND p.ProductName COLLATE utf8mb4_unicode_ci = m.product
SET i.ProductId = p.Id
WHERE i.IsDeleted = 0 AND i.ProductId IS NULL;

-- Matter Gateway: a package item on the "Matter Gateway" line, linked to "Zigbee Matter Gateway"
-- (no package quantities - the drawing page adds one per area where another company's automation is used)
INSERT INTO PackageItems (TenantId, Name, ProductLineId, ProductId, SortOrder, IsDeleted)
SELECT l.TenantId, 'Matter Gateway', l.Id, p.Id,
       (SELECT COALESCE(MAX(x.SortOrder), 0) + 1 FROM PackageItems x WHERE x.TenantId = l.TenantId), 0
FROM SolutionProductLines l
JOIN (SELECT TenantId, MIN(Id) AS Id FROM Products WHERE IsDeleted = 0 AND ProductName = 'Zigbee Matter Gateway' GROUP BY TenantId) p
  ON p.TenantId = l.TenantId
WHERE l.Name = 'Matter Gateway' AND l.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM PackageItems e WHERE e.TenantId = l.TenantId AND e.IsDeleted = 0
                  AND (e.Name = 'Matter Gateway' OR e.ProductLineId = l.Id));

DROP TEMPORARY TABLE map_item_product;
