-- ============================================================================================================
-- Product catalogue, normalized and per company (tenant). MySQL 8 / MariaDB 10.5. Safe to rerun.
-- Also run by the backend on start (Data/SchemaUpgrades.cs), so a fresh database gets it too.
--
--   Tenants (company)
--    ├─ SolutionGroups            category            e.g. Lighting Control
--    │   └─ SolutionProductLines  product family      e.g. Piano Series      (tiers allowed: CatalogFamilyTiers)
--    │       └─ CatalogProducts   product             price-list name without colour, e.g. "Innswitch Piano 2 Gang"
--    │           ├─ Products      SKU                 exact price-list row: "Innswitch Piano 2 Gang White", Model = T1L2W
--    │           │   └─ CatalogPriceListItems         price per price list
--    │           └─ CatalogAliases                    every other name (packages / categorization file, drawings)
--    ├─ CatalogTiers              Standard, Premium (rows; Code = what the drawing reader calls the plan)
--    ├─ CatalogColors
--    ├─ CatalogPriceLists         several, exactly one active
--    └─ PackageSizes              1 BHK ... 10 Kanal
--        └─ CatalogPackageLines   (package, tier, product) -> quantity (decimal; missing = no row)
--
-- Every reference between company tables is a composite foreign key (TenantId, x) -> parent (TenantId, Id),
-- so a row can never point at another company's data. Names are unique per company.
-- Prices are never stored per package line: v_package_quote computes them from the active price list.
-- ============================================================================================================

DROP PROCEDURE IF EXISTS _cat_exec_if;
DELIMITER $$
-- runs `stmt` when `missing` (a COUNT(*) query over information_schema) returns 0
CREATE PROCEDURE _cat_exec_if(IN missing TEXT, IN stmt TEXT)
BEGIN
  SET @cat_q = CONCAT('SELECT COUNT(*) INTO @cat_n FROM (', missing, ') x');
  PREPARE s FROM @cat_q; EXECUTE s; DEALLOCATE PREPARE s;
  IF @cat_n = 0 THEN
    SET @cat_s = stmt;
    PREPARE s FROM @cat_s; EXECUTE s; DEALLOCATE PREPARE s;
  END IF;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS _cat_add_column;
DELIMITER $$
CREATE PROCEDURE _cat_add_column(IN tbl VARCHAR(64), IN col VARCHAR(64), IN ddl TEXT)
BEGIN
  CALL _cat_exec_if(CONCAT('SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(''', tbl, ''') AND COLUMN_NAME = ''', col, ''''),
                    CONCAT('ALTER TABLE `', tbl, '` ADD COLUMN ', ddl));
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS _cat_add_index;
DELIMITER $$
CREATE PROCEDURE _cat_add_index(IN tbl VARCHAR(64), IN idx VARCHAR(64), IN ddl TEXT)
BEGIN
  CALL _cat_exec_if(CONCAT('SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(''', tbl, ''') AND INDEX_NAME = ''', idx, ''''),
                    CONCAT('ALTER TABLE `', tbl, '` ADD ', ddl));
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS _cat_add_fk;
DELIMITER $$
CREATE PROCEDURE _cat_add_fk(IN tbl VARCHAR(64), IN fk VARCHAR(64), IN ddl TEXT)
BEGIN
  CALL _cat_exec_if(CONCAT('SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(''', tbl, ''') AND CONSTRAINT_NAME = ''', fk, ''''),
                    CONCAT('ALTER TABLE `', tbl, '` ADD CONSTRAINT `', fk, '` ', ddl));
END$$
DELIMITER ;

-- ── parents: (TenantId, Id) is unique, so children can reference it ───────────────────────────────────────
CALL _cat_add_index('SolutionGroups',       'UX_SolutionGroups_Tenant_Id',       'UNIQUE KEY `UX_SolutionGroups_Tenant_Id` (`TenantId`, `Id`)');
CALL _cat_add_index('SolutionProductLines', 'UX_SolutionProductLines_Tenant_Id', 'UNIQUE KEY `UX_SolutionProductLines_Tenant_Id` (`TenantId`, `Id`)');
CALL _cat_add_index('PackageSizes',         'UX_PackageSizes_Tenant_Id',         'UNIQUE KEY `UX_PackageSizes_Tenant_Id` (`TenantId`, `Id`)');
CALL _cat_add_index('Products',             'UX_Products_Tenant_Id',             'UNIQUE KEY `UX_Products_Tenant_Id` (`TenantId`, `Id`)');
-- a family belongs to a category of the same company
CALL _cat_add_index('SolutionProductLines', 'IX_SolutionProductLines_Tenant_Group', 'KEY `IX_SolutionProductLines_Tenant_Group` (`TenantId`, `GroupId`)');
CALL _cat_add_fk('SolutionProductLines', 'FK_SolutionProductLines_Tenant_Group',
  'FOREIGN KEY (`TenantId`, `GroupId`) REFERENCES `SolutionGroups` (`TenantId`, `Id`)');

-- ── tiers (plans) ────────────────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CatalogTiers` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(50) NOT NULL,
  `Code` varchar(20) NOT NULL,                 -- the plan the drawing reader is asked for: standard / premium
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogTiers_Tenant_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_CatalogTiers_Tenant_Name` (`TenantId`, `Name`),
  UNIQUE KEY `UX_CatalogTiers_Tenant_Code` (`TenantId`, `Code`),
  CONSTRAINT `FK_CatalogTiers_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- which families are allowed in which tier (replaces SolutionProductLines.InStandard / InPremium, kept in step)
CREATE TABLE IF NOT EXISTS `CatalogFamilyTiers` (
  `TenantId` int NOT NULL,
  `FamilyId` int NOT NULL,
  `TierId` int NOT NULL,
  PRIMARY KEY (`TenantId`, `FamilyId`, `TierId`),
  KEY `IX_CatalogFamilyTiers_Tier` (`TenantId`, `TierId`),
  CONSTRAINT `FK_CatalogFamilyTiers_Family` FOREIGN KEY (`TenantId`, `FamilyId`) REFERENCES `SolutionProductLines` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogFamilyTiers_Tier` FOREIGN KEY (`TenantId`, `TierId`) REFERENCES `CatalogTiers` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CatalogColors` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(100) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogColors_Tenant_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_CatalogColors_Tenant_Name` (`TenantId`, `Name`),
  CONSTRAINT `FK_CatalogColors_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── products: one row per product; specs once, here ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CatalogProducts` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `FamilyId` int NULL,
  `Name` varchar(255) NOT NULL,                -- the price-list name without colour (what plan suggestions show)
  `Description` longtext NULL,
  `Unit` varchar(20) NOT NULL DEFAULT 'pc',    -- 'm' for rails etc.: package quantities may be fractional
  `FallbackPrice` decimal(18,2) NULL,          -- packages-file price, used while the product is not on the price list
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogProducts_Tenant_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_CatalogProducts_Tenant_Name` (`TenantId`, `Name`),
  KEY `IX_CatalogProducts_Family` (`TenantId`, `FamilyId`),
  CONSTRAINT `FK_CatalogProducts_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`),
  CONSTRAINT `FK_CatalogProducts_Family` FOREIGN KEY (`TenantId`, `FamilyId`) REFERENCES `SolutionProductLines` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- SKU = the existing inventory row (Products): stock, sales and purchases keep pointing at it
CALL _cat_add_column('Products', 'CatalogProductId', '`CatalogProductId` int NULL');
CALL _cat_add_column('Products', 'ColorId', '`ColorId` int NULL');
CALL _cat_add_column('Products', 'IsDefaultSku', '`IsDefaultSku` tinyint(1) NOT NULL DEFAULT 0');
-- one default SKU per product (a deleted SKU doesn't count)
CALL _cat_add_column('Products', 'DefaultSkuKey', '`DefaultSkuKey` int GENERATED ALWAYS AS (IF(`IsDefaultSku` = 1 AND `IsDeleted` = 0, `CatalogProductId`, NULL)) STORED');
CALL _cat_add_index('Products', 'UX_Products_DefaultSku', 'UNIQUE KEY `UX_Products_DefaultSku` (`TenantId`, `DefaultSkuKey`)');
CALL _cat_add_index('Products', 'IX_Products_CatalogProduct', 'KEY `IX_Products_CatalogProduct` (`TenantId`, `CatalogProductId`)');
CALL _cat_add_index('Products', 'IX_Products_Color', 'KEY `IX_Products_Color` (`TenantId`, `ColorId`)');
CALL _cat_add_fk('Products', 'FK_Products_CatalogProduct', 'FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CatalogProducts` (`TenantId`, `Id`)');
CALL _cat_add_fk('Products', 'FK_Products_CatalogColor', 'FOREIGN KEY (`TenantId`, `ColorId`) REFERENCES `CatalogColors` (`TenantId`, `Id`)');

-- every other name of a product (or a family): used for matching, never displayed
CREATE TABLE IF NOT EXISTS `CatalogAliases` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Alias` varchar(255) NOT NULL,
  `AliasKey` varchar(255) NOT NULL,            -- lower case, single spaces, trimmed
  `Source` varchar(20) NOT NULL,               -- package / categorization / drawing / pricelist / manual
  `TargetKind` varchar(10) NOT NULL,           -- product / family
  `CatalogProductId` int NULL,
  `FamilyId` int NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogAliases_Key` (`TenantId`, `TargetKind`, `AliasKey`),
  KEY `IX_CatalogAliases_Product` (`TenantId`, `CatalogProductId`),
  KEY `IX_CatalogAliases_Family` (`TenantId`, `FamilyId`),
  CONSTRAINT `CK_CatalogAliases_Target` CHECK ((`TargetKind` = 'product' AND `CatalogProductId` IS NOT NULL AND `FamilyId` IS NULL)
                                            OR (`TargetKind` = 'family' AND `FamilyId` IS NOT NULL AND `CatalogProductId` IS NULL)),
  CONSTRAINT `CK_CatalogAliases_Source` CHECK (`Source` IN ('package', 'categorization', 'drawing', 'pricelist', 'manual')),
  CONSTRAINT `FK_CatalogAliases_Product` FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CatalogProducts` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogAliases_Family` FOREIGN KEY (`TenantId`, `FamilyId`) REFERENCES `SolutionProductLines` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── price lists: several per company, exactly one active ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CatalogPriceLists` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 0,
  `ActiveKey` tinyint GENERATED ALWAYS AS (IF(`IsActive` = 1, 1, NULL)) STORED,
  `SourceFile` varchar(255) NULL,
  `ImportedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogPriceLists_Tenant_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_CatalogPriceLists_Tenant_Name` (`TenantId`, `Name`),
  UNIQUE KEY `UX_CatalogPriceLists_OneActive` (`TenantId`, `ActiveKey`),
  CONSTRAINT `FK_CatalogPriceLists_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CatalogPriceListItems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PriceListId` int NOT NULL,
  `SkuId` int NOT NULL,
  `Price` decimal(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogPriceListItems_Sku` (`TenantId`, `PriceListId`, `SkuId`),
  KEY `IX_CatalogPriceListItems_SkuId` (`TenantId`, `SkuId`),
  CONSTRAINT `CK_CatalogPriceListItems_Price` CHECK (`Price` >= 0),
  CONSTRAINT `FK_CatalogPriceListItems_List` FOREIGN KEY (`TenantId`, `PriceListId`) REFERENCES `CatalogPriceLists` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogPriceListItems_Sku` FOREIGN KEY (`TenantId`, `SkuId`) REFERENCES `Products` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── package quantities per tier ──────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CatalogPackageLines` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PackageSizeId` int NOT NULL,
  `TierId` int NOT NULL,
  `CatalogProductId` int NOT NULL,
  `Quantity` decimal(12,3) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogPackageLines` (`TenantId`, `PackageSizeId`, `TierId`, `CatalogProductId`),
  KEY `IX_CatalogPackageLines_Tier` (`TenantId`, `TierId`),
  KEY `IX_CatalogPackageLines_Product` (`TenantId`, `CatalogProductId`),
  CONSTRAINT `CK_CatalogPackageLines_Quantity` CHECK (`Quantity` > 0),
  CONSTRAINT `FK_CatalogPackageLines_Package` FOREIGN KEY (`TenantId`, `PackageSizeId`) REFERENCES `PackageSizes` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogPackageLines_Tier` FOREIGN KEY (`TenantId`, `TierId`) REFERENCES `CatalogTiers` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogPackageLines_Product` FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CatalogProducts` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- which products each drawing rule (config/solution_rules.yaml in the reader) uses, best first; Gang '' = any
CREATE TABLE IF NOT EXISTS `CatalogDrawingRuleItems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `RuleName` varchar(100) NOT NULL,
  `Gang` varchar(10) NOT NULL DEFAULT '',
  `Rank` int NOT NULL DEFAULT 1,
  `CatalogProductId` int NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogDrawingRuleItems` (`TenantId`, `RuleName`, `Gang`, `CatalogProductId`),
  KEY `IX_CatalogDrawingRuleItems_Product` (`TenantId`, `CatalogProductId`),
  CONSTRAINT `FK_CatalogDrawingRuleItems_Product` FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CatalogProducts` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── import: column layout per company and file, and names waiting for a person to map ───────────────────
CREATE TABLE IF NOT EXISTS `CatalogImportProfiles` (
  `TenantId` int NOT NULL,
  `Kind` varchar(20) NOT NULL,                 -- pricelist / categorization / packages
  `ProfileJson` longtext NOT NULL,
  PRIMARY KEY (`TenantId`, `Kind`),
  CONSTRAINT `CK_CatalogImportProfiles_Kind` CHECK (`Kind` IN ('pricelist', 'categorization', 'packages')),
  CONSTRAINT `FK_CatalogImportProfiles_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CatalogUnmatched` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Kind` varchar(20) NOT NULL,                 -- the file it came from
  `Name` varchar(255) NOT NULL,
  `NameKey` varchar(255) NOT NULL,
  `RowJson` longtext NOT NULL,                 -- the row, applied once the name is mapped
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogUnmatched` (`TenantId`, `Kind`, `NameKey`),
  CONSTRAINT `FK_CatalogUnmatched_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- companies whose old solution data has been moved into the catalogue (CatalogMigrate.sql), once each
CREATE TABLE IF NOT EXISTS `CatalogMigrations` (
  `TenantId` int NOT NULL,
  `MigratedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`TenantId`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- old package item -> the catalogue product it became (kept until the old tables are removed)
CALL _cat_add_column('PackageItems', 'CatalogProductId', '`CatalogProductId` int NULL');

-- ── views ────────────────────────────────────────────────────────────────────────────────────────────────
-- price of each SKU in the company's active price list
CREATE OR REPLACE VIEW `v_active_sku_price` AS
SELECT s.`TenantId`, s.`Id` AS `SkuId`, s.`CatalogProductId`, pli.`Price`
FROM `Products` s
JOIN `CatalogPriceLists` pl ON pl.`TenantId` = s.`TenantId` AND pl.`IsActive` = 1
JOIN `CatalogPriceListItems` pli ON pli.`TenantId` = s.`TenantId` AND pli.`PriceListId` = pl.`Id` AND pli.`SkuId` = s.`Id`
WHERE s.`IsDeleted` = 0;

-- the products a tier may use: family allowed in that tier; NeedsConfirmation = the family is allowed in every
-- tier (Standard and Premium) - when designing Premium the user confirms it first
CREATE OR REPLACE VIEW `v_suggestable_product` AS
SELECT p.`TenantId`, t.`Id` AS `TierId`, t.`Code` AS `TierCode`, t.`Name` AS `TierName`,
       p.`Id` AS `CatalogProductId`, p.`Name` AS `ProductName`, f.`Id` AS `FamilyId`, f.`Name` AS `FamilyName`, g.`Name` AS `CategoryName`,
       ((SELECT COUNT(*) FROM `CatalogFamilyTiers` x WHERE x.`TenantId` = p.`TenantId` AND x.`FamilyId` = f.`Id`)
        = (SELECT COUNT(*) FROM `CatalogTiers` y WHERE y.`TenantId` = p.`TenantId`)) AS `NeedsConfirmation`,
       s.`Id` AS `DefaultSkuId`, s.`ProductName` AS `SkuName`, s.`Model` AS `ModelCode`,
       COALESCE(ap.`Price`, IF(s.`Id` IS NULL, p.`FallbackPrice`, NULL)) AS `UnitPrice`
FROM `CatalogProducts` p
JOIN `SolutionProductLines` f ON f.`TenantId` = p.`TenantId` AND f.`Id` = p.`FamilyId` AND f.`IsDeleted` = 0
JOIN `SolutionGroups` g ON g.`TenantId` = f.`TenantId` AND g.`Id` = f.`GroupId` AND g.`IsDeleted` = 0
JOIN `CatalogFamilyTiers` ft ON ft.`TenantId` = p.`TenantId` AND ft.`FamilyId` = f.`Id`
JOIN `CatalogTiers` t ON t.`TenantId` = ft.`TenantId` AND t.`Id` = ft.`TierId`
LEFT JOIN `Products` s ON s.`TenantId` = p.`TenantId` AND s.`CatalogProductId` = p.`Id` AND s.`IsDefaultSku` = 1 AND s.`IsDeleted` = 0
LEFT JOIN `v_active_sku_price` ap ON ap.`TenantId` = s.`TenantId` AND ap.`SkuId` = s.`Id`;

-- package quote lines: quantity x the default SKU's price in the active price list (never stored);
-- a product not on the price list uses its packages-file price; UnitPrice NULL = no price at all
CREATE OR REPLACE VIEW `v_package_quote` AS
SELECT l.`TenantId`, l.`PackageSizeId`, ps.`Name` AS `PackageName`, l.`TierId`, t.`Name` AS `TierName`,
       p.`Id` AS `CatalogProductId`, p.`Name` AS `ProductName`, s.`Id` AS `SkuId`, s.`ProductName` AS `SkuName`, s.`Model` AS `ModelCode`,
       l.`Quantity`,
       COALESCE(ap.`Price`, IF(s.`Id` IS NULL, p.`FallbackPrice`, NULL)) AS `UnitPrice`,
       ROUND(l.`Quantity` * COALESCE(ap.`Price`, IF(s.`Id` IS NULL, p.`FallbackPrice`, NULL)), 2) AS `LineTotal`
FROM `CatalogPackageLines` l
JOIN `PackageSizes` ps ON ps.`TenantId` = l.`TenantId` AND ps.`Id` = l.`PackageSizeId` AND ps.`IsDeleted` = 0
JOIN `CatalogTiers` t ON t.`TenantId` = l.`TenantId` AND t.`Id` = l.`TierId`
JOIN `CatalogProducts` p ON p.`TenantId` = l.`TenantId` AND p.`Id` = l.`CatalogProductId`
LEFT JOIN `Products` s ON s.`TenantId` = p.`TenantId` AND s.`CatalogProductId` = p.`Id` AND s.`IsDefaultSku` = 1 AND s.`IsDeleted` = 0
LEFT JOIN `v_active_sku_price` ap ON ap.`TenantId` = s.`TenantId` AND ap.`SkuId` = s.`Id`;

-- package lines whose product's family is not allowed in that tier (or that have no family)
CREATE OR REPLACE VIEW `v_package_tier_conflict` AS
SELECT l.`TenantId`, l.`Id` AS `PackageLineId`, ps.`Name` AS `PackageName`, t.`Name` AS `TierName`,
       p.`Id` AS `CatalogProductId`, p.`Name` AS `ProductName`, f.`Name` AS `FamilyName`, l.`Quantity`
FROM `CatalogPackageLines` l
JOIN `PackageSizes` ps ON ps.`TenantId` = l.`TenantId` AND ps.`Id` = l.`PackageSizeId`
JOIN `CatalogTiers` t ON t.`TenantId` = l.`TenantId` AND t.`Id` = l.`TierId`
JOIN `CatalogProducts` p ON p.`TenantId` = l.`TenantId` AND p.`Id` = l.`CatalogProductId`
LEFT JOIN `SolutionProductLines` f ON f.`TenantId` = p.`TenantId` AND f.`Id` = p.`FamilyId` AND f.`IsDeleted` = 0
WHERE f.`Id` IS NULL
   OR NOT EXISTS (SELECT 1 FROM `CatalogFamilyTiers` ft WHERE ft.`TenantId` = l.`TenantId` AND ft.`FamilyId` = f.`Id` AND ft.`TierId` = l.`TierId`);

-- ── delete a company and everything it owns ──────────────────────────────────────────────────────────────
-- every table with a TenantId column, then the company; one transaction. Not called by the app - run by hand.
DROP PROCEDURE IF EXISTS sp_delete_company;
DELIMITER $$
CREATE PROCEDURE sp_delete_company(IN p_company_id INT)
BEGIN
  DECLARE done INT DEFAULT 0;
  DECLARE t VARCHAR(64);
  DECLARE cur CURSOR FOR
    SELECT c.TABLE_NAME FROM information_schema.COLUMNS c
    JOIN information_schema.TABLES tb ON tb.TABLE_SCHEMA = c.TABLE_SCHEMA AND tb.TABLE_NAME = c.TABLE_NAME AND tb.TABLE_TYPE = 'BASE TABLE'
    WHERE c.TABLE_SCHEMA = DATABASE() AND c.COLUMN_NAME = 'TenantId';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; SET FOREIGN_KEY_CHECKS = 1; RESIGNAL; END;
  IF NOT EXISTS (SELECT 1 FROM `Tenants` WHERE `Id` = p_company_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'No such company';
  END IF;
  START TRANSACTION;
  SET FOREIGN_KEY_CHECKS = 0;      -- the company's own rows reference each other; all of them go
  OPEN cur;
  read_loop: LOOP
    FETCH cur INTO t;
    IF done THEN LEAVE read_loop; END IF;
    SET @sql = CONCAT('DELETE FROM `', t, '` WHERE `TenantId` = ', p_company_id);
    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;
  END LOOP;
  CLOSE cur;
  DELETE FROM `CatalogMigrations` WHERE `TenantId` = p_company_id;
  DELETE FROM `Tenants` WHERE `Id` = p_company_id;
  SET FOREIGN_KEY_CHECKS = 1;
  COMMIT;
END$$
DELIMITER ;
