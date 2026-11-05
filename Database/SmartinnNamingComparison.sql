-- Smartinn_Naming_Comparison.xlsx applied to Solution Packages, for every company (tenant). Safe to rerun.
--
-- 1. Package items that have no line on the categorization: a line is added for them, used in BOTH Standard and Premium.
-- 2. Price-list products missing from the packages: added as package items (no quantities yet - enter them on the
--    Package Quantities tab); their categorization lines are used in BOTH Standard and Premium.
-- 3. Package items are linked to their price-list product (PackageItems.ProductId) - this is the only link the
--    drawing suggestion and the quotation use for the product and its price.
-- 4. Package items that are NOT in the price list are unlinked: no price, and on a quotation the user enters them by hand.
--
-- Run it again after importing the price list into a company's inventory, to link the products.

START TRANSACTION;

-- ── 1 + 2: categorization lines ──────────────────────────────────────────────
CREATE TEMPORARY TABLE nc_line (grp VARCHAR(100), line VARCHAR(150), ord INT) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
INSERT INTO nc_line VALUES
 ('Touch Panel',      'Hpad X',               1),   -- package item, not on the categorization
 ('Touch Panel',      'Qbrid',                2),   -- package item, not on the categorization
 ('Touch Panel',      'Hpad 7',               3),   -- price list (Harmony HPAD 7), in neither file
 ('Smart Access',     'Door Lock i7',         1),
 ('Lighting Control', 'Marina Series',        1),
 ('Lighting Control', 'Floating Series',      2),
 ('Smart Sensors',    'Water Leakage Sensor', 1),
 ('Smart Intercom',   'Indoor Unit',          1);

INSERT INTO SolutionProductLines (TenantId, GroupId, Name, InStandard, InPremium, SortOrder, IsDeleted)
SELECT g.TenantId, g.Id, n.line, 1, 1,
       (SELECT COALESCE(MAX(x.SortOrder), 0) FROM SolutionProductLines x WHERE x.GroupId = g.Id AND x.IsDeleted = 0) + n.ord, 0
FROM SolutionGroups g
JOIN nc_line n ON n.grp = g.Name COLLATE utf8mb4_unicode_ci
WHERE g.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM SolutionProductLines e WHERE e.TenantId = g.TenantId AND e.IsDeleted = 0
                  AND e.Name COLLATE utf8mb4_unicode_ci = n.line);

-- price-list products being added to the packages: used in both Standard and Premium
UPDATE SolutionProductLines SET InStandard = 1, InPremium = 1
WHERE IsDeleted = 0 AND Name IN ('Hpad 7', 'Matrix Series', 'Fusion Series', 'Matter Gateway', 'Smart Panic Button', 'Human Presence Sensor');

-- ── 2: package items for the price-list products that the packages did not have ──
CREATE TEMPORARY TABLE nc_item (item VARCHAR(150), line VARCHAR(150), ord INT) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
INSERT INTO nc_item VALUES
 ('Smart Panel Hpad 7',    'Hpad 7',                1),
 ('Matter Gateway',        'Matter Gateway',        2),
 ('Piano Switch 4 Gang',   'Piano Series',          3),
 ('Matrix Switch 1 Gang',  'Matrix Series',         4),
 ('Matrix Switch 2 Gang',  'Matrix Series',         5),
 ('Matrix Switch 3 Gang',  'Matrix Series',         6),
 ('Matrix Switch 4 Gang',  'Matrix Series',         7),
 ('Fusion Switch 1 Gang',  'Fusion Series',         8),
 ('Fusion Switch 2 Gang',  'Fusion Series',         9),
 ('Fusion Switch 3 Gang',  'Fusion Series',        10),
 ('Fusion Switch 4 Gang',  'Fusion Series',        11),
 ('Smart Panic Button',    'Smart Panic Button',   12),
 ('Human Presence Sensor', 'Human Presence Sensor', 13);

INSERT INTO PackageItems (TenantId, Name, ProductLineId, ProductId, SortOrder, IsDeleted)
SELECT l.TenantId, n.item, l.Id, NULL,
       (SELECT COALESCE(MAX(x.SortOrder), 0) FROM PackageItems x WHERE x.TenantId = l.TenantId AND x.IsDeleted = 0) + n.ord, 0
FROM SolutionProductLines l
JOIN nc_item n ON n.line = l.Name COLLATE utf8mb4_unicode_ci
WHERE l.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM PackageItems e WHERE e.TenantId = l.TenantId AND e.IsDeleted = 0
                  AND e.Name COLLATE utf8mb4_unicode_ci = n.item);

-- ── 1: package items without a categorization line -> their line ─────────────
CREATE TEMPORARY TABLE nc_item_line (item VARCHAR(150), line VARCHAR(150)) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
INSERT INTO nc_item_line VALUES
 ('Smart Panel Hpad X',         'Hpad X'),
 ('Smart Panel Qbrid',          'Qbrid'),
 ('Wireless Zigbee Gateway',    'Mini Gateway'),
 ('Smart Lock i7',              'Door Lock i7'),
 ('Luxury Smart Switch Marina', 'Marina Series'),
 ('Floating Switch 1 Gang',     'Floating Series'),
 ('Floating Switch 2 Gang',     'Floating Series'),
 ('Floating Switch 3 Gang',     'Floating Series'),
 ('Floating Switch 4 Gang',     'Floating Series'),
 ('Water Leakage Sensor',       'Water Leakage Sensor'),
 ('Intercom Indoor Unit',       'Indoor Unit');

UPDATE PackageItems i
JOIN nc_item_line m ON m.item = i.Name COLLATE utf8mb4_unicode_ci
JOIN SolutionProductLines l ON l.TenantId = i.TenantId AND l.IsDeleted = 0 AND l.Name COLLATE utf8mb4_unicode_ci = m.line
SET i.ProductLineId = l.Id
WHERE i.IsDeleted = 0 AND i.ProductLineId IS NULL;

-- ── 3: package item -> price-list product (lowest Id when a name repeats) ─────
CREATE TEMPORARY TABLE nc_product (item VARCHAR(150), product VARCHAR(255)) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
INSERT INTO nc_product VALUES
 ('Smart Panel Echo',                  'Harmony HPAD Echo'),
 ('Smart Panel Q2',                    'Harmony HPAD Q2'),
 ('Smart Panel Hpad 7',                'Harmony HPAD 7'),
 ('Wireless Zigbee Gateway',           'Zigbee Gate Way Controller'),
 ('Matter Gateway',                    'Zigbee Matter Gateway'),
 ('Smart Lock i5',                     'Innshield Smart Door Lock i5'),
 ('Smart Lock i9 Pro',                 'Innshield Smart Door Lock i9 Pro'),
 ('Piano Switch 1 Gang',               'Innswitch Piano 1 Gang White'),
 ('Piano Switch 2 Gang',               'Innswitch Piano 2 Gang White'),
 ('Piano Switch 3 Gang',               'Innswitch Piano 3 Gang White'),
 ('Piano Switch 4 Gang',               'Innswitch Piano 4 Gang White'),
 ('Matrix Switch 1 Gang',              'InnSwitch Matrix 1 gang Switch'),
 ('Matrix Switch 2 Gang',              'InnSwitch Matrix 2 gang Switch'),
 ('Matrix Switch 3 Gang',              'InnSwitch Matrix 3 gang Switch'),
 ('Matrix Switch 4 Gang',              'InnSwitch Matrix 4 gang Switch'),
 ('Fusion Switch 1 Gang',              'InnSwitch Fusion 1 gang Switch'),
 ('Fusion Switch 2 Gang',              'InnSwitch Fusion 2 gang Switch'),
 ('Fusion Switch 3 Gang',              'InnSwitch Fusion 3 gang Switch'),
 ('Fusion Switch 4 Gang',              'InnSwitch Fusion 4 gang Switch'),
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
 ('Smart Panic Button',                'Zigbee Emergency button'),
 ('Human Presence Sensor',             'Human Detection Sensor'),
 ('IR Remote Control',                 'IR Blaster'),
 ('Intercom Outdoor Unit Gate Keeper', 'Touchmate / Video Door Bell');

-- linked when not linked yet, or linked to a product that was deleted
UPDATE PackageItems i
JOIN nc_product m ON m.item = i.Name COLLATE utf8mb4_unicode_ci
JOIN (SELECT TenantId, ProductName, MIN(Id) AS Id FROM Products WHERE IsDeleted = 0 GROUP BY TenantId, ProductName) p
  ON p.TenantId = i.TenantId AND p.ProductName COLLATE utf8mb4_unicode_ci = m.product
SET i.ProductId = p.Id
WHERE i.IsDeleted = 0
  AND (i.ProductId IS NULL
       OR NOT EXISTS (SELECT 1 FROM Products x WHERE x.Id = i.ProductId AND x.TenantId = i.TenantId AND x.IsDeleted = 0));

-- ── 4: not in the price list -> never taken from the inventory ──────────────
UPDATE PackageItems SET ProductId = NULL
WHERE IsDeleted = 0 AND ProductId IS NOT NULL AND Name IN (
  'Smart Panel Hpad X Pro', 'Smart Panel Hpad X', 'Smart Panel Qbrid', 'Smart Lock i7', 'Luxury Smart Switch Marina',
  'Floating Switch 1 Gang', 'Floating Switch 2 Gang', 'Floating Switch 3 Gang', 'Floating Switch 4 Gang',
  'Smart Curtain Motor 6Nm', 'Intercom Indoor Unit');

DROP TEMPORARY TABLE nc_line;
DROP TEMPORARY TABLE nc_item;
DROP TEMPORARY TABLE nc_item_line;
DROP TEMPORARY TABLE nc_product;

COMMIT;
