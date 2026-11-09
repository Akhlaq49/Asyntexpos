-- ============================================================================================================
-- INVT: inventory additions EF can't describe. MySQL 8 / MariaDB 10.5. Safe to rerun.
-- Run by the backend on start (Data/DatabaseSetup.cs) after Database/SYST/Procedures.sql.
--
--   INVT_PPRC   the purchase price of each inventory product (one row per product), set on Inventory > Purchase
--               Prices; purchase orders take their unit cost from it and quotations show their profit against it.
--               A product without one gets its end user price less 10,000 (never below 0).
-- ============================================================================================================

-- products: (TenantId, Id) is unique, so company tables can reference it (also made by Database/CATL/Schema.sql)
CALL SYST_PROC_ADIX('INVT_PROD', 'UX_Products_Tenant_Id', 'UNIQUE KEY `UX_Products_Tenant_Id` (`TenantId`, `Id`)');

CREATE TABLE IF NOT EXISTS `INVT_PPRC` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `PurchasePrice` decimal(18,2) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `UpdatedBy` varchar(150) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_INVT_PPRC_Product` (`TenantId`, `ProductId`),
  CONSTRAINT `FK_INVT_PPRC_Product` FOREIGN KEY (`TenantId`, `ProductId`) REFERENCES `INVT_PROD` (`TenantId`, `Id`) ON DELETE CASCADE,
  CONSTRAINT `CK_INVT_PPRC_Price` CHECK (`PurchasePrice` >= 0)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- every product has its purchase price: the ones without get the starting price (end user price - 10,000)
INSERT INTO `INVT_PPRC` (`TenantId`, `ProductId`, `PurchasePrice`, `UpdatedAt`, `UpdatedBy`)
SELECT p.`TenantId`, p.`Id`, GREATEST(p.`Price` - 10000, 0), UTC_TIMESTAMP(6), 'system'
FROM `INVT_PROD` p
WHERE NOT EXISTS (SELECT 1 FROM `INVT_PPRC` x WHERE x.`TenantId` = p.`TenantId` AND x.`ProductId` = p.`Id`);
