-- ============================================================================================================
-- CATL: product catalogue, normalized and per company (tenant). MySQL 8 / MariaDB 10.5. Safe to rerun.
-- Run by the backend on start (Data/DatabaseSetup.cs) after Database/SYST/Procedures.sql.
--
--   SYST_TNNT (company)
--    ├─ CATL_CATG             category            e.g. Lighting Control
--    │   └─ CATL_FMLY         family              e.g. Piano Series      (plans allowed: CATL_FMTR)
--    │       └─ CATL_PROD     product             name without colour, e.g. "Innswitch Piano 2 Gang"
--    │           ├─ INVT_PROD SKU                 the inventory product: "Innswitch Piano 2 Gang White", its price
--    │           └─ CATL_ALIS                     every other name (package items / categorization file, drawings)
--    ├─ CATL_TIER             Standard, Premium (rows; Code = what the drawing reader calls the plan)
--    ├─ CATL_COLR
--    └─ CATL_SIZE             1 BHK ... 10 Kanal
--        └─ CATL_PKLN         (size, plan, product) -> quantity (decimal; missing = no row)
--
-- Every reference between company tables is a composite foreign key (TenantId, x) -> parent (TenantId, Id),
-- so a row can never point at another company's data. Names are unique per company.
-- Prices are never stored per package line: CATL_VIEW_PKQT takes them from the inventory (INVT_PROD.Price).
-- ============================================================================================================

-- ── parents: (TenantId, Id) is unique, so children can reference it ───────────────────────────────────────
CALL SYST_PROC_ADIX('CATL_CATG',       'UX_SolutionGroups_Tenant_Id',       'UNIQUE KEY `UX_SolutionGroups_Tenant_Id` (`TenantId`, `Id`)');
CALL SYST_PROC_ADIX('CATL_FMLY', 'UX_SolutionProductLines_Tenant_Id', 'UNIQUE KEY `UX_SolutionProductLines_Tenant_Id` (`TenantId`, `Id`)');
CALL SYST_PROC_ADIX('CATL_SIZE',         'UX_PackageSizes_Tenant_Id',         'UNIQUE KEY `UX_PackageSizes_Tenant_Id` (`TenantId`, `Id`)');
CALL SYST_PROC_ADIX('INVT_PROD',             'UX_Products_Tenant_Id',             'UNIQUE KEY `UX_Products_Tenant_Id` (`TenantId`, `Id`)');
-- a family belongs to a category of the same company
CALL SYST_PROC_ADIX('CATL_FMLY', 'IX_SolutionProductLines_Tenant_Group', 'KEY `IX_SolutionProductLines_Tenant_Group` (`TenantId`, `GroupId`)');
CALL SYST_PROC_ADFK('CATL_FMLY', 'FK_SolutionProductLines_Tenant_Group',
  'FOREIGN KEY (`TenantId`, `GroupId`) REFERENCES `CATL_CATG` (`TenantId`, `Id`)');

-- ── tiers (plans) ────────────────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CATL_TIER` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(50) NOT NULL,
  `Code` varchar(20) NOT NULL,                 -- the plan the drawing reader is asked for: standard / premium
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogTiers_Tenant_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_CatalogTiers_Tenant_Name` (`TenantId`, `Name`),
  UNIQUE KEY `UX_CatalogTiers_Tenant_Code` (`TenantId`, `Code`),
  CONSTRAINT `FK_CatalogTiers_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `SYST_TNNT` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- which families are allowed in which tier (replaces CATL_FMLY.InStandard / InPremium, kept in step)
CREATE TABLE IF NOT EXISTS `CATL_FMTR` (
  `TenantId` int NOT NULL,
  `FamilyId` int NOT NULL,
  `TierId` int NOT NULL,
  PRIMARY KEY (`TenantId`, `FamilyId`, `TierId`),
  KEY `IX_CatalogFamilyTiers_Tier` (`TenantId`, `TierId`),
  CONSTRAINT `FK_CatalogFamilyTiers_Family` FOREIGN KEY (`TenantId`, `FamilyId`) REFERENCES `CATL_FMLY` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogFamilyTiers_Tier` FOREIGN KEY (`TenantId`, `TierId`) REFERENCES `CATL_TIER` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CATL_COLR` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(100) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogColors_Tenant_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_CatalogColors_Tenant_Name` (`TenantId`, `Name`),
  CONSTRAINT `FK_CatalogColors_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `SYST_TNNT` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── products: one row per product; specs once, here ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CATL_PROD` (
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
  CONSTRAINT `FK_CatalogProducts_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `SYST_TNNT` (`Id`),
  CONSTRAINT `FK_CatalogProducts_Family` FOREIGN KEY (`TenantId`, `FamilyId`) REFERENCES `CATL_FMLY` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- SKU = the existing inventory row (INVT_PROD): stock, sales and purchases keep pointing at it
CALL SYST_PROC_ADCL('INVT_PROD', 'CatalogProductId', '`CatalogProductId` int NULL');
CALL SYST_PROC_ADCL('INVT_PROD', 'ColorId', '`ColorId` int NULL');
CALL SYST_PROC_ADCL('INVT_PROD', 'IsDefaultSku', '`IsDefaultSku` tinyint(1) NOT NULL DEFAULT 0');
-- one default SKU per product (a deleted SKU doesn't count)
CALL SYST_PROC_ADCL('INVT_PROD', 'DefaultSkuKey', '`DefaultSkuKey` int GENERATED ALWAYS AS (IF(`IsDefaultSku` = 1 AND `IsDeleted` = 0, `CatalogProductId`, NULL)) STORED');
CALL SYST_PROC_ADIX('INVT_PROD', 'UX_Products_DefaultSku', 'UNIQUE KEY `UX_Products_DefaultSku` (`TenantId`, `DefaultSkuKey`)');
CALL SYST_PROC_ADIX('INVT_PROD', 'IX_Products_CatalogProduct', 'KEY `IX_Products_CatalogProduct` (`TenantId`, `CatalogProductId`)');
CALL SYST_PROC_ADIX('INVT_PROD', 'IX_Products_Color', 'KEY `IX_Products_Color` (`TenantId`, `ColorId`)');
CALL SYST_PROC_ADFK('INVT_PROD', 'FK_Products_CatalogProduct', 'FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CATL_PROD` (`TenantId`, `Id`)');
CALL SYST_PROC_ADFK('INVT_PROD', 'FK_Products_CatalogColor', 'FOREIGN KEY (`TenantId`, `ColorId`) REFERENCES `CATL_COLR` (`TenantId`, `Id`)');

-- every other name of a product (or a family): used for matching, never displayed
CREATE TABLE IF NOT EXISTS `CATL_ALIS` (
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
  CONSTRAINT `FK_CatalogAliases_Product` FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CATL_PROD` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogAliases_Family` FOREIGN KEY (`TenantId`, `FamilyId`) REFERENCES `CATL_FMLY` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── package quantities per tier ──────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CATL_PKLN` (
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
  CONSTRAINT `FK_CatalogPackageLines_Package` FOREIGN KEY (`TenantId`, `PackageSizeId`) REFERENCES `CATL_SIZE` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogPackageLines_Tier` FOREIGN KEY (`TenantId`, `TierId`) REFERENCES `CATL_TIER` (`TenantId`, `Id`),
  CONSTRAINT `FK_CatalogPackageLines_Product` FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CATL_PROD` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- which products each drawing rule (config/solution_rules.yaml in the reader) uses, best first; Gang '' = any
CREATE TABLE IF NOT EXISTS `CATL_RULE` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `RuleName` varchar(100) NOT NULL,
  `Gang` varchar(10) NOT NULL DEFAULT '',
  `Rank` int NOT NULL DEFAULT 1,
  `CatalogProductId` int NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogDrawingRuleItems` (`TenantId`, `RuleName`, `Gang`, `CatalogProductId`),
  KEY `IX_CatalogDrawingRuleItems_Product` (`TenantId`, `CatalogProductId`),
  CONSTRAINT `FK_CatalogDrawingRuleItems_Product` FOREIGN KEY (`TenantId`, `CatalogProductId`) REFERENCES `CATL_PROD` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── import: column layout per company and file, and names waiting for a person to map ───────────────────
CREATE TABLE IF NOT EXISTS `CATL_IMPF` (
  `TenantId` int NOT NULL,
  `Kind` varchar(20) NOT NULL,                 -- pricelist / categorization / packages
  `ProfileJson` longtext NOT NULL,
  PRIMARY KEY (`TenantId`, `Kind`),
  CONSTRAINT `CK_CatalogImportProfiles_Kind` CHECK (`Kind` IN ('pricelist', 'categorization', 'packages')),
  CONSTRAINT `FK_CatalogImportProfiles_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `SYST_TNNT` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CATL_UNMT` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Kind` varchar(20) NOT NULL,                 -- the file it came from
  `Name` varchar(255) NOT NULL,
  `NameKey` varchar(255) NOT NULL,
  `RowJson` longtext NOT NULL,                 -- the row, applied once the name is mapped
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_CatalogUnmatched` (`TenantId`, `Kind`, `NameKey`),
  CONSTRAINT `FK_CatalogUnmatched_Tenant` FOREIGN KEY (`TenantId`) REFERENCES `SYST_TNNT` (`Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ── views ────────────────────────────────────────────────────────────────────────────────────────────────
-- the products a tier may use: family allowed in that tier; NeedsConfirmation = the family is allowed in every
-- tier (Standard and Premium) - when designing Premium the user confirms it first
CREATE OR REPLACE VIEW `CATL_VIEW_SUGG` AS
SELECT p.`TenantId`, t.`Id` AS `TierId`, t.`Code` AS `TierCode`, t.`Name` AS `TierName`,
       p.`Id` AS `CatalogProductId`, p.`Name` AS `ProductName`, f.`Id` AS `FamilyId`, f.`Name` AS `FamilyName`, g.`Name` AS `CategoryName`,
       ((SELECT COUNT(*) FROM `CATL_FMTR` x WHERE x.`TenantId` = p.`TenantId` AND x.`FamilyId` = f.`Id`)
        = (SELECT COUNT(*) FROM `CATL_TIER` y WHERE y.`TenantId` = p.`TenantId`)) AS `NeedsConfirmation`,
       s.`Id` AS `DefaultSkuId`, s.`ProductName` AS `SkuName`, s.`Model` AS `ModelCode`,
       IF(s.`Id` IS NULL, p.`FallbackPrice`, s.`Price`) AS `UnitPrice`
FROM `CATL_PROD` p
JOIN `CATL_FMLY` f ON f.`TenantId` = p.`TenantId` AND f.`Id` = p.`FamilyId` AND f.`IsDeleted` = 0
JOIN `CATL_CATG` g ON g.`TenantId` = f.`TenantId` AND g.`Id` = f.`GroupId` AND g.`IsDeleted` = 0
JOIN `CATL_FMTR` ft ON ft.`TenantId` = p.`TenantId` AND ft.`FamilyId` = f.`Id`
JOIN `CATL_TIER` t ON t.`TenantId` = ft.`TenantId` AND t.`Id` = ft.`TierId`
LEFT JOIN `INVT_PROD` s ON s.`TenantId` = p.`TenantId` AND s.`CatalogProductId` = p.`Id` AND s.`IsDefaultSku` = 1 AND s.`IsDeleted` = 0;

-- package quote lines: quantity x the default SKU's inventory price (never stored);
-- a product not in the inventory uses its fallback price, if one was entered; UnitPrice NULL = no price at all
CREATE OR REPLACE VIEW `CATL_VIEW_PKQT` AS
SELECT l.`TenantId`, l.`PackageSizeId`, ps.`Name` AS `PackageName`, l.`TierId`, t.`Name` AS `TierName`,
       p.`Id` AS `CatalogProductId`, p.`Name` AS `ProductName`, s.`Id` AS `SkuId`, s.`ProductName` AS `SkuName`, s.`Model` AS `ModelCode`,
       l.`Quantity`,
       IF(s.`Id` IS NULL, p.`FallbackPrice`, s.`Price`) AS `UnitPrice`,
       ROUND(l.`Quantity` * IF(s.`Id` IS NULL, p.`FallbackPrice`, s.`Price`), 2) AS `LineTotal`
FROM `CATL_PKLN` l
JOIN `CATL_SIZE` ps ON ps.`TenantId` = l.`TenantId` AND ps.`Id` = l.`PackageSizeId` AND ps.`IsDeleted` = 0
JOIN `CATL_TIER` t ON t.`TenantId` = l.`TenantId` AND t.`Id` = l.`TierId`
JOIN `CATL_PROD` p ON p.`TenantId` = l.`TenantId` AND p.`Id` = l.`CatalogProductId`
LEFT JOIN `INVT_PROD` s ON s.`TenantId` = p.`TenantId` AND s.`CatalogProductId` = p.`Id` AND s.`IsDefaultSku` = 1 AND s.`IsDeleted` = 0;

-- package lines whose product's family is not allowed in that tier (or that have no family)
CREATE OR REPLACE VIEW `CATL_VIEW_TRCF` AS
SELECT l.`TenantId`, l.`Id` AS `PackageLineId`, ps.`Name` AS `PackageName`, t.`Name` AS `TierName`,
       p.`Id` AS `CatalogProductId`, p.`Name` AS `ProductName`, f.`Name` AS `FamilyName`, l.`Quantity`
FROM `CATL_PKLN` l
JOIN `CATL_SIZE` ps ON ps.`TenantId` = l.`TenantId` AND ps.`Id` = l.`PackageSizeId`
JOIN `CATL_TIER` t ON t.`TenantId` = l.`TenantId` AND t.`Id` = l.`TierId`
JOIN `CATL_PROD` p ON p.`TenantId` = l.`TenantId` AND p.`Id` = l.`CatalogProductId`
LEFT JOIN `CATL_FMLY` f ON f.`TenantId` = p.`TenantId` AND f.`Id` = p.`FamilyId` AND f.`IsDeleted` = 0
WHERE f.`Id` IS NULL
   OR NOT EXISTS (SELECT 1 FROM `CATL_FMTR` ft WHERE ft.`TenantId` = l.`TenantId` AND ft.`FamilyId` = f.`Id` AND ft.`TierId` = l.`TierId`);
