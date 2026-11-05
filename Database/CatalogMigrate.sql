-- ============================================================================================================
-- Moves each company's existing solution data into the catalogue (CatalogSchema.sql). Once per company
-- (CatalogMigrations); safe to rerun. Also run by the backend on start. Nothing old is deleted.
--
--   SolutionProductLines.InStandard / InPremium  -> CatalogFamilyTiers (Standard / Premium rows in CatalogTiers)
--   Products (one row per colour)                -> SKUs of a CatalogProducts row: the name without its colour;
--                                                   default SKU = the White one, else the first
--   Products.Price                               -> the active price list "Inventory prices (migrated)"
--   PackageItems                                 -> the product of its linked SKU, else a product of its own name
--                                                   (not on the price list); its name is kept as a 'package' alias
--   PackageQuantities (one per size)             -> CatalogPackageLines for every tier the product's family allows
--   names in the reader's rules file             -> CatalogDrawingRuleItems (via the package item names)
-- ============================================================================================================

-- every company has the two tiers (new companies too)
INSERT IGNORE INTO `CatalogTiers` (`TenantId`, `Name`, `Code`, `SortOrder`) SELECT `Id`, 'Standard', 'standard', 1 FROM `Tenants`;
INSERT IGNORE INTO `CatalogTiers` (`TenantId`, `Name`, `Code`, `SortOrder`) SELECT `Id`, 'Premium', 'premium', 2 FROM `Tenants`;

START TRANSACTION;

DROP TEMPORARY TABLE IF EXISTS cat_mt;
CREATE TEMPORARY TABLE cat_mt (`TenantId` int NOT NULL PRIMARY KEY)
SELECT t.`Id` AS `TenantId` FROM `Tenants` t WHERE NOT EXISTS (SELECT 1 FROM `CatalogMigrations` m WHERE m.`TenantId` = t.`Id`);

-- ── tiers allowed per family ─────────────────────────────────────────────────────────────────────────────
INSERT IGNORE INTO `CatalogFamilyTiers` (`TenantId`, `FamilyId`, `TierId`)
SELECT l.`TenantId`, l.`Id`, t.`Id`
FROM `SolutionProductLines` l
JOIN cat_mt m ON m.`TenantId` = l.`TenantId`
JOIN `CatalogTiers` t ON t.`TenantId` = l.`TenantId` AND ((t.`Code` = 'standard' AND l.`InStandard` = 1) OR (t.`Code` = 'premium' AND l.`InPremium` = 1))
WHERE l.`IsDeleted` = 0;

-- ── colours, products, SKUs ──────────────────────────────────────────────────────────────────────────────
INSERT IGNORE INTO `CatalogColors` (`TenantId`, `Name`)
SELECT DISTINCT p.`TenantId`, TRIM(p.`Color`)
FROM `Products` p JOIN cat_mt m ON m.`TenantId` = p.`TenantId`
WHERE p.`IsDeleted` = 0 AND TRIM(COALESCE(p.`Color`, '')) <> '';

-- product name = the SKU name without its colour at the end ("Innswitch Piano 1 Gang White" -> "Innswitch Piano 1 Gang")
DROP TEMPORARY TABLE IF EXISTS cat_sku;
CREATE TEMPORARY TABLE cat_sku (`SkuId` int NOT NULL PRIMARY KEY, `TenantId` int NOT NULL, `Name` varchar(255) NOT NULL, `Color` varchar(100) NULL)
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci
SELECT p.`Id` AS `SkuId`, p.`TenantId`,
       CASE WHEN TRIM(COALESCE(p.`Color`, '')) <> '' AND LOWER(TRIM(p.`ProductName`)) LIKE CONCAT('% ', LOWER(TRIM(p.`Color`)))
            THEN TRIM(LEFT(TRIM(p.`ProductName`), CHAR_LENGTH(TRIM(p.`ProductName`)) - CHAR_LENGTH(TRIM(p.`Color`))))
            ELSE TRIM(p.`ProductName`) END AS `Name`,
       NULLIF(TRIM(p.`Color`), '') AS `Color`
FROM `Products` p JOIN cat_mt m ON m.`TenantId` = p.`TenantId`
WHERE p.`IsDeleted` = 0 AND p.`CatalogProductId` IS NULL;

INSERT IGNORE INTO `CatalogProducts` (`TenantId`, `Name`)
SELECT s.`TenantId`, MIN(s.`Name`) FROM cat_sku s GROUP BY s.`TenantId`, s.`Name`;

UPDATE `Products` p
JOIN cat_sku s ON s.`SkuId` = p.`Id`
JOIN `CatalogProducts` c ON c.`TenantId` = s.`TenantId` AND c.`Name` = s.`Name`
LEFT JOIN `CatalogColors` col ON col.`TenantId` = s.`TenantId` AND col.`Name` = s.`Color`
SET p.`CatalogProductId` = c.`Id`, p.`ColorId` = col.`Id`;

-- specs once, on the product: the first SKU's description
UPDATE `CatalogProducts` c
JOIN (SELECT p.`CatalogProductId`, MIN(p.`Id`) AS `SkuId` FROM `Products` p JOIN cat_sku s ON s.`SkuId` = p.`Id` GROUP BY p.`CatalogProductId`) d ON d.`CatalogProductId` = c.`Id`
JOIN `Products` p ON p.`Id` = d.`SkuId`
SET c.`Description` = p.`Description`
WHERE c.`Description` IS NULL;

-- default SKU: White if there is one, else the first
UPDATE `Products` p
JOIN (SELECT p2.`CatalogProductId`,
             COALESCE(MIN(CASE WHEN LOWER(s.`Color`) = 'white' THEN p2.`Id` END), MIN(p2.`Id`)) AS `SkuId`
      FROM `Products` p2 JOIN cat_sku s ON s.`SkuId` = p2.`Id`
      GROUP BY p2.`CatalogProductId`) d ON d.`SkuId` = p.`Id`
SET p.`IsDefaultSku` = 1;

-- ── the current inventory prices become the active price list ────────────────────────────────────────────
INSERT INTO `CatalogPriceLists` (`TenantId`, `Name`, `IsActive`)
SELECT DISTINCT s.`TenantId`, 'Inventory prices (migrated)', 1
FROM cat_sku s
WHERE NOT EXISTS (SELECT 1 FROM `CatalogPriceLists` x WHERE x.`TenantId` = s.`TenantId` AND x.`IsActive` = 1);

INSERT IGNORE INTO `CatalogPriceListItems` (`TenantId`, `PriceListId`, `SkuId`, `Price`)
SELECT p.`TenantId`, pl.`Id`, p.`Id`, GREATEST(p.`Price`, 0)
FROM `Products` p
JOIN cat_sku s ON s.`SkuId` = p.`Id`
JOIN `CatalogPriceLists` pl ON pl.`TenantId` = p.`TenantId` AND pl.`IsActive` = 1;

-- ── package items -> products ────────────────────────────────────────────────────────────────────────────
-- linked to a SKU: that SKU's product
UPDATE `PackageItems` i
JOIN cat_mt m ON m.`TenantId` = i.`TenantId`
JOIN `Products` p ON p.`TenantId` = i.`TenantId` AND p.`Id` = i.`ProductId` AND p.`IsDeleted` = 0
SET i.`CatalogProductId` = p.`CatalogProductId`
WHERE i.`IsDeleted` = 0 AND i.`CatalogProductId` IS NULL AND p.`CatalogProductId` IS NOT NULL;

-- not linked (not on the price list): a product with the package name (or the product that already has it)
INSERT IGNORE INTO `CatalogProducts` (`TenantId`, `Name`, `FamilyId`)
SELECT i.`TenantId`, TRIM(i.`Name`), i.`ProductLineId`
FROM `PackageItems` i JOIN cat_mt m ON m.`TenantId` = i.`TenantId`
WHERE i.`IsDeleted` = 0 AND i.`CatalogProductId` IS NULL;

UPDATE `PackageItems` i
JOIN cat_mt m ON m.`TenantId` = i.`TenantId`
JOIN `CatalogProducts` c ON c.`TenantId` = i.`TenantId` AND c.`Name` = TRIM(i.`Name`) COLLATE utf8mb4_unicode_ci
SET i.`CatalogProductId` = c.`Id`
WHERE i.`IsDeleted` = 0 AND i.`CatalogProductId` IS NULL;

-- the product's family = its package item's categorization line
UPDATE `CatalogProducts` c
JOIN cat_mt m ON m.`TenantId` = c.`TenantId`
JOIN `PackageItems` i ON i.`TenantId` = c.`TenantId` AND i.`CatalogProductId` = c.`Id` AND i.`IsDeleted` = 0
JOIN `SolutionProductLines` l ON l.`TenantId` = i.`TenantId` AND l.`Id` = i.`ProductLineId` AND l.`IsDeleted` = 0
SET c.`FamilyId` = l.`Id`
WHERE c.`FamilyId` IS NULL;

-- the packages-file name, for matching later imports
INSERT IGNORE INTO `CatalogAliases` (`TenantId`, `Alias`, `AliasKey`, `Source`, `TargetKind`, `CatalogProductId`)
SELECT i.`TenantId`, TRIM(i.`Name`), REGEXP_REPLACE(LOWER(TRIM(i.`Name`)), '[[:space:]]+', ' '), 'package', 'product', i.`CatalogProductId`
FROM `PackageItems` i
JOIN cat_mt m ON m.`TenantId` = i.`TenantId`
JOIN `CatalogProducts` c ON c.`TenantId` = i.`TenantId` AND c.`Id` = i.`CatalogProductId`
WHERE i.`IsDeleted` = 0 AND LOWER(TRIM(i.`Name`)) COLLATE utf8mb4_unicode_ci <> LOWER(c.`Name`);

-- ── package quantities -> per tier: every tier the product's family allows ───────────────────────────────
INSERT INTO `CatalogPackageLines` (`TenantId`, `PackageSizeId`, `TierId`, `CatalogProductId`, `Quantity`)
SELECT q.`TenantId`, q.`PackageSizeId`, ft.`TierId`, i.`CatalogProductId`, SUM(q.`Quantity`)
FROM `PackageQuantities` q
JOIN cat_mt m ON m.`TenantId` = q.`TenantId`
JOIN `PackageItems` i ON i.`TenantId` = q.`TenantId` AND i.`Id` = q.`PackageItemId` AND i.`IsDeleted` = 0
JOIN `PackageSizes` s ON s.`TenantId` = q.`TenantId` AND s.`Id` = q.`PackageSizeId` AND s.`IsDeleted` = 0
JOIN `CatalogProducts` c ON c.`TenantId` = i.`TenantId` AND c.`Id` = i.`CatalogProductId`
JOIN `CatalogFamilyTiers` ft ON ft.`TenantId` = c.`TenantId` AND ft.`FamilyId` = c.`FamilyId`
WHERE q.`IsDeleted` = 0 AND q.`Quantity` > 0
GROUP BY q.`TenantId`, q.`PackageSizeId`, ft.`TierId`, i.`CatalogProductId`
ON DUPLICATE KEY UPDATE `Quantity` = VALUES(`Quantity`);

-- ── drawing rules: the item names the reader's rules file used, now product links per company ────────────
DROP TEMPORARY TABLE IF EXISTS cat_rule;
CREATE TEMPORARY TABLE cat_rule (`RuleName` varchar(100), `Gang` varchar(10), `Rank` int, `ItemName` varchar(150))
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
INSERT INTO cat_rule VALUES
 ('Switch boards', '3', 1, 'Piano Switch 3 Gang'), ('Switch boards', '3', 2, 'Matrix Switch 3 Gang'),
 ('Switch boards', '3', 3, 'Fusion Switch 3 Gang'), ('Switch boards', '3', 4, 'Floating Switch 3 Gang'),
 ('Switch boards', '2', 1, 'Piano Switch 2 Gang'), ('Switch boards', '2', 2, 'Matrix Switch 2 Gang'),
 ('Switch boards', '2', 3, 'Fusion Switch 2 Gang'), ('Switch boards', '2', 4, 'Floating Switch 2 Gang'),
 ('Switch boards', '1', 1, 'Piano Switch 1 Gang'), ('Switch boards', '1', 2, 'Matrix Switch 1 Gang'),
 ('Switch boards', '1', 3, 'Fusion Switch 1 Gang'), ('Switch boards', '1', 4, 'Floating Switch 1 Gang'),
 ('AC / TV control', '', 1, 'IR Remote Control'),
 ('Curtain motors', '', 1, 'Smart Curtain Motor 1.5Nm'), ('Curtain motors', '', 2, 'Smart Curtain Motor 6Nm'),
 ('Curtain rail', '', 1, 'Curtain Rail 1 meter'),
 ('Gas sensor', '', 1, 'Gas Sensor'),
 ('Smoke sensor', '', 1, 'Smoke Sensor'),
 ('Motion sensor', '', 1, 'Motion Sensor'),
 ('Temperature / humidity', '', 1, 'Temp and Humidity Sensor'),
 ('Main door lock', '', 1, 'Smart Lock i5'), ('Main door lock', '', 2, 'Smart Lock i9 Pro'),
 ('Intercom', '', 1, 'Intercom Outdoor Unit Gate Keeper'),
 ('Matter gateway', '', 1, 'Matter Gateway');

INSERT IGNORE INTO `CatalogDrawingRuleItems` (`TenantId`, `RuleName`, `Gang`, `Rank`, `CatalogProductId`)
SELECT i.`TenantId`, r.`RuleName`, r.`Gang`, r.`Rank`, i.`CatalogProductId`
FROM cat_rule r
JOIN `PackageItems` i ON i.`Name` COLLATE utf8mb4_unicode_ci = r.`ItemName` AND i.`IsDeleted` = 0 AND i.`CatalogProductId` IS NOT NULL
JOIN cat_mt m ON m.`TenantId` = i.`TenantId`;

INSERT IGNORE INTO `CatalogMigrations` (`TenantId`) SELECT `TenantId` FROM cat_mt;

DROP TEMPORARY TABLE cat_rule;
DROP TEMPORARY TABLE cat_sku;
DROP TEMPORARY TABLE cat_mt;

COMMIT;
