-- =====================================================================
-- Retail suite changes (branch feature/pharma-chnages), on top of PharmacyChanges.sql
--
--   Locations + stock movement ledger, transfers in two steps, branch requisitions,
--   wholesale price levels, customer credit accounts and receipts, medicine catalogue,
--   Urdu/Arabic product names, Shopify channel, colour/size variants, manufacturing
--   (on the tables the manufacturing branch created) and restaurant.
--
-- Safe to run more than once. Works on MySQL 8+ and MariaDB 10.5+.
-- Run PharmacyChanges.sql first. Take a backup first (Database/Backup-Database.ps1).
--   mysql -u <user> -p <database> < Database/RetailSuiteChanges.sql
-- Generated from the EF model; do not hand-edit the table/column section, regenerate it.
-- =====================================================================

SET NAMES utf8mb4;
DROP PROCEDURE IF EXISTS suite_add_col;
DROP PROCEDURE IF EXISTS suite_add_index;

DELIMITER $$
CREATE PROCEDURE suite_add_col(IN t VARCHAR(64), IN c VARCHAR(64), IN def TEXT)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                   WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(t) AND COLUMN_NAME = c) THEN
        SET @ddl = CONCAT('ALTER TABLE `', t, '` ADD COLUMN `', c, '` ', def);
        PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END$$

CREATE PROCEDURE suite_add_index(IN t VARCHAR(64), IN idx VARCHAR(64), IN cols TEXT)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS
                   WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(t) AND INDEX_NAME = idx) THEN
        SET @ddl = CONCAT('CREATE INDEX `', idx, '` ON `', t, '` (', cols, ')');
        PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

SET FOREIGN_KEY_CHECKS = 0;

-- ── New tables ──
CREATE TABLE IF NOT EXISTS `bomoperations` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `BomId` int NOT NULL,
  `Sequence` int NOT NULL,
  `WorkCentreId` int NOT NULL,
  `Description` varchar(300) CHARACTER SET utf8mb4 DEFAULT NULL,
  `PlannedMinutes` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_BomOperations_BomId` (`BomId`),
  CONSTRAINT `FK_BomOperations_billofmaterials_BomId` FOREIGN KEY (`BomId`) REFERENCES `billofmaterials` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `channelconnections` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Channel` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
  `ShopDomain` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
  `AccessToken` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `WebhookSecret` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `LocationId` int NOT NULL,
  `ExternalLocationId` varchar(50) CHARACTER SET utf8mb4 DEFAULT NULL,
  `SyncProducts` tinyint(1) NOT NULL,
  `SyncStock` tinyint(1) NOT NULL,
  `SyncPrices` tinyint(1) NOT NULL,
  `ImportOrders` tinyint(1) NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `LastError` varchar(1000) CHARACTER SET utf8mb4 DEFAULT NULL,
  `LastSyncAt` datetime(6) DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `channelorders` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ConnectionId` int NOT NULL,
  `ExternalOrderId` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
  `ExternalNumber` varchar(50) CHARACTER SET utf8mb4 DEFAULT NULL,
  `SaleId` int DEFAULT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `Error` varchar(1000) CHARACTER SET utf8mb4 DEFAULT NULL,
  `PayloadJson` mediumtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `ImportedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ChannelOrders_TenantId_ConnectionId_ExternalOrderId` (`TenantId`,`ConnectionId`,`ExternalOrderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `channelproductlinks` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ConnectionId` int NOT NULL,
  `ProductId` int NOT NULL,
  `VariantId` int DEFAULT NULL,
  `ExternalProductId` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
  `ExternalVariantId` varchar(50) CHARACTER SET utf8mb4 DEFAULT NULL,
  `ExternalInventoryItemId` varchar(50) CHARACTER SET utf8mb4 DEFAULT NULL,
  `LastPushedQty` int DEFAULT NULL,
  `LastPushedPrice` decimal(18,2) DEFAULT NULL,
  `LastSyncedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ChannelProductLinks_TenantId_ConnectionId_ProductId_VariantId` (`TenantId`,`ConnectionId`,`ProductId`,`VariantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `customerledgerentries` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `CustomerId` int NOT NULL,
  `EntryDate` datetime(6) NOT NULL,
  `EntryType` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `Reference` varchar(50) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Debit` decimal(18,2) NOT NULL,
  `Credit` decimal(18,2) NOT NULL,
  `Narration` varchar(500) CHARACTER SET utf8mb4 DEFAULT NULL,
  `CreatedBy` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_CustomerLedgerEntries_TenantId_CustomerId_EntryDate` (`TenantId`,`CustomerId`,`EntryDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `customerreceipts` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Reference` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
  `CustomerId` int NOT NULL,
  `LocationId` int DEFAULT NULL,
  `TillSessionId` int DEFAULT NULL,
  `Amount` decimal(18,2) NOT NULL,
  `PaymentMethod` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
  `ChequeNo` varchar(50) CHARACTER SET utf8mb4 DEFAULT NULL,
  `ChequeDate` datetime(6) DEFAULT NULL,
  `SalesReturnId` int DEFAULT NULL,
  `Notes` varchar(500) CHARACTER SET utf8mb4 DEFAULT NULL,
  `ReceivedBy` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `ReceiptDate` datetime(6) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_CustomerReceipts_TenantId_CustomerId` (`TenantId`,`CustomerId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `diningtables` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `LocationId` int NOT NULL,
  `Name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
  `Seats` int NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `druginteractions` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `GenericA` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `GenericB` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `Note` varchar(1000) CHARACTER SET utf8mb4 DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_DrugInteractions_GenericA_GenericB` (`GenericA`,`GenericB`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `kitchenstations` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `LocationId` int NOT NULL,
  `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `PrinterName` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `kitchenticketitems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `TicketId` int NOT NULL,
  `SaleItemId` int NOT NULL,
  `ItemName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `Quantity` decimal(18,3) NOT NULL,
  `Notes` varchar(500) CHARACTER SET utf8mb4 DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_KitchenTicketItems_TicketId` (`TicketId`),
  CONSTRAINT `FK_KitchenTicketItems_KitchenTickets_TicketId` FOREIGN KEY (`TicketId`) REFERENCES `kitchentickets` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `kitchentickets` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `SaleId` int NOT NULL,
  `StationId` int NOT NULL,
  `TicketNo` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `ReadyAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_KitchenTickets_TenantId_StationId_Status` (`TenantId`,`StationId`,`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `locations` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Code` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
  `Type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `ParentId` int DEFAULT NULL,
  `Address` varchar(500) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Phone` varchar(50) CHARACTER SET utf8mb4 DEFAULT NULL,
  `DrugLicenceNo` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `IsDefault` tinyint(1) NOT NULL,
  `SellsOnline` tinyint(1) NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Locations_TenantId_Code` (`TenantId`,`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `locationstocklevels` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `LocationId` int NOT NULL,
  `MinQty` int NOT NULL,
  `MaxQty` int NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_LocationStockLevels_TenantId_ProductId_LocationId` (`TenantId`,`ProductId`,`LocationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `medicinemasters` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `BrandName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `GenericName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `Strength` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `DosageForm` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `PackSize` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `UnitsPerStrip` int DEFAULT NULL,
  `StripsPerBox` int DEFAULT NULL,
  `Manufacturer` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `RegistrationNo` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `TherapeuticClass` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Schedule` varchar(20) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Barcode` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `MaxRetailPrice` decimal(18,2) DEFAULT NULL,
  `PriceUpdatedOn` datetime(6) DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_MedicineMasters_Barcode` (`Barcode`),
  KEY `IX_MedicineMasters_BrandName` (`BrandName`),
  KEY `IX_MedicineMasters_GenericName` (`GenericName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `modifiergroups` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `MinSelect` int NOT NULL,
  `MaxSelect` int NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `modifiers` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ModifierGroupId` int NOT NULL,
  `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `Price` decimal(18,2) NOT NULL,
  `IngredientId` int DEFAULT NULL,
  `IngredientQty` decimal(18,3) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Modifiers_ModifierGroupId` (`ModifierGroupId`),
  CONSTRAINT `FK_Modifiers_ModifierGroups_ModifierGroupId` FOREIGN KEY (`ModifierGroupId`) REFERENCES `modifiergroups` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `moitembatches` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `MoItemId` int NOT NULL,
  `BatchId` int DEFAULT NULL,
  `Quantity` int NOT NULL,
  `UnitCost` decimal(18,4) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_MoItemBatches_MoItemId` (`MoItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `mooperations` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `OrderId` int NOT NULL,
  `Sequence` int NOT NULL,
  `WorkCentreId` int NOT NULL,
  `Description` varchar(300) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `Operator` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `StartedAt` datetime(6) DEFAULT NULL,
  `CompletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_MoOperations_OrderId` (`OrderId`),
  CONSTRAINT `FK_MoOperations_manufacturingorders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `manufacturingorders` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `pricelevels` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
  `IsDefault` tinyint(1) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `productmodifiergroups` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `ModifierGroupId` int NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProductModifierGroups_TenantId_ProductId_ModifierGroupId` (`TenantId`,`ProductId`,`ModifierGroupId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `productoptions` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `VariantAttributeId` int NOT NULL,
  `Position` int NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `productprices` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `VariantId` int DEFAULT NULL,
  `PriceLevelId` int NOT NULL,
  `SaleUnit` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
  `MinQty` int NOT NULL,
  `Price` decimal(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProductPrices_TenantId_ProductId_VariantId_PriceLevelId_Sale~` (`TenantId`,`ProductId`,`VariantId`,`PriceLevelId`,`SaleUnit`,`MinQty`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `producttranslations` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `Lang` varchar(2) CHARACTER SET utf8mb4 NOT NULL,
  `Name` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `UsageNote` varchar(500) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Description` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProductTranslations_TenantId_ProductId_Lang` (`TenantId`,`ProductId`,`Lang`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `productvariants` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `Sku` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `Barcode` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Option1` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Option2` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Option3` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `Price` decimal(18,2) DEFAULT NULL,
  `CostPrice` decimal(18,4) DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProductVariants_TenantId_Sku` (`TenantId`,`Sku`),
  KEY `IX_ProductVariants_TenantId_Barcode` (`TenantId`,`Barcode`),
  KEY `IX_ProductVariants_TenantId_ProductId` (`TenantId`,`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `productvariantvalues` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `VariantId` int NOT NULL,
  `AttributeValueId` int NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProductVariantValues_VariantId_AttributeValueId` (`VariantId`,`AttributeValueId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `recipeitems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `RecipeId` int NOT NULL,
  `IngredientId` int NOT NULL,
  `Quantity` decimal(18,3) NOT NULL,
  `Unit` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `WastagePct` decimal(5,2) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_RecipeItems_RecipeId` (`RecipeId`),
  CONSTRAINT `FK_RecipeItems_Recipes_RecipeId` FOREIGN KEY (`RecipeId`) REFERENCES `recipes` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `recipes` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `YieldQty` decimal(18,3) NOT NULL,
  `Notes` varchar(1000) CHARACTER SET utf8mb4 DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Recipes_TenantId_ProductId` (`TenantId`,`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `stockmovements` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ProductId` int NOT NULL,
  `VariantId` int DEFAULT NULL,
  `LocationId` int DEFAULT NULL,
  `BatchId` int DEFAULT NULL,
  `Quantity` int NOT NULL,
  `UnitCost` decimal(18,4) NOT NULL,
  `MovementType` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
  `SourceType` varchar(30) CHARACTER SET utf8mb4 DEFAULT NULL,
  `SourceId` int DEFAULT NULL,
  `Reference` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,
  `CreatedBy` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_StockMovements_BatchId` (`BatchId`),
  KEY `IX_StockMovements_TenantId_LocationId_CreatedAt` (`TenantId`,`LocationId`,`CreatedAt`),
  KEY `IX_StockMovements_TenantId_ProductId_CreatedAt` (`TenantId`,`ProductId`,`CreatedAt`),
  KEY `IX_StockMovements_TenantId_SourceType_SourceId` (`TenantId`,`SourceType`,`SourceId`),
  CONSTRAINT `FK_StockMovements_ProductBatches_BatchId` FOREIGN KEY (`BatchId`) REFERENCES `productbatches` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `stockrequestitems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `StockRequestId` int NOT NULL,
  `ProductId` int NOT NULL,
  `VariantId` int DEFAULT NULL,
  `RequestedQty` int NOT NULL,
  `ApprovedQty` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_StockRequestItems_StockRequestId` (`StockRequestId`),
  CONSTRAINT `FK_StockRequestItems_StockRequests_StockRequestId` FOREIGN KEY (`StockRequestId`) REFERENCES `stockrequests` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `stockrequests` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Reference` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
  `FromLocationId` int NOT NULL,
  `ToLocationId` int NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `Source` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `TransferId` int DEFAULT NULL,
  `Notes` varchar(500) CHARACTER SET utf8mb4 DEFAULT NULL,
  `RequestedBy` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `ApprovedBy` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `stores` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Value` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `Label` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `syncoutbox` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `ConnectionId` int NOT NULL,
  `Kind` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
  `EntityId` int NOT NULL,
  `Attempts` int NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
  `LastError` varchar(1000) CHARACTER SET utf8mb4 DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `SentAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_SyncOutbox_Status_CreatedAt` (`Status`,`CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `variantattributevalues` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `VariantAttributeId` int NOT NULL,
  `Value` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `Code` varchar(20) CHARACTER SET utf8mb4 DEFAULT NULL,
  `SortOrder` int NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_VariantAttributeValues_TenantId_VariantAttributeId_Value` (`TenantId`,`VariantAttributeId`,`Value`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `warehouses` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Value` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `Label` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `workcentres` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `LocationId` int DEFAULT NULL,
  `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
  `CostPerHour` decimal(18,2) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- ── New columns on existing tables ──
CALL suite_add_col('billofmaterials', 'Version', "int NOT NULL DEFAULT 1");
CALL suite_add_col('billofmaterials', 'ShelfLifeDays', "int NULL");
CALL suite_add_col('billofmaterials', 'ExpectedYieldPct', "decimal(5,2) NOT NULL DEFAULT 100");
CALL suite_add_col('manufacturingorderitems', 'WastageQty', "decimal(18,3) NOT NULL DEFAULT 0");
CALL suite_add_col('manufacturingorders', 'LocationId', "int NULL");
CALL suite_add_col('manufacturingorders', 'OutputLocationId', "int NULL");
CALL suite_add_col('manufacturingorders', 'OutputBatchNo', "varchar(100) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('manufacturingorders', 'OutputExpiryDate', "datetime(6) NULL");
CALL suite_add_col('manufacturingorders', 'OutputBatchId', "int NULL");
CALL suite_add_col('manufacturingorders', 'ActualOutputQty', "int NULL");
CALL suite_add_col('manufacturingorders', 'ScrapQty', "int NOT NULL DEFAULT 0");
CALL suite_add_col('parties', 'LocationId', "int NULL");
CALL suite_add_col('parties', 'RestrictToLocation', "tinyint(1) NOT NULL DEFAULT 0");
CALL suite_add_col('parties', 'Language', "varchar(2) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'en'");
CALL suite_add_col('parties', 'CustomerType', "varchar(20) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('parties', 'PriceLevelId', "int NULL");
CALL suite_add_col('parties', 'CreditLimit', "decimal(18,2) NULL");
CALL suite_add_col('parties', 'CreditDays', "int NULL");
CALL suite_add_col('parties', 'OpeningBalance', "decimal(18,2) NOT NULL DEFAULT 0");
CALL suite_add_col('parties', 'Ntn', "varchar(50) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('parties', 'Strn', "varchar(50) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('parties', 'CreditBlocked', "tinyint(1) NOT NULL DEFAULT 0");
CALL suite_add_col('pharmacysettings', 'ReceiptLanguage', "varchar(10) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'en'");
CALL suite_add_col('pharmacysettings', 'BlockOverCreditLimit', "tinyint(1) NOT NULL DEFAULT 1");
CALL suite_add_col('productbatches', 'LocationId', "int NULL");
CALL suite_add_col('productbatches', 'VariantId', "int NULL");
CALL suite_add_col('products', 'MedicineMasterId', "int NULL");
CALL suite_add_col('products', 'ArticleNo', "varchar(100) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('products', 'Season', "varchar(50) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('products', 'HasVariants', "tinyint(1) NOT NULL DEFAULT 0");
CALL suite_add_col('products', 'SellOnline', "tinyint(1) NOT NULL DEFAULT 1");
CALL suite_add_col('products', 'IsMenuItem', "tinyint(1) NOT NULL DEFAULT 0");
CALL suite_add_col('products', 'KitchenStationId', "int NULL");
CALL suite_add_col('purchaseitems', 'VariantId', "int NULL");
CALL suite_add_col('purchasereturnitems', 'VariantId', "int NULL");
CALL suite_add_col('purchasereturns', 'LocationId', "int NULL");
CALL suite_add_col('purchases', 'LocationId', "int NULL");
CALL suite_add_col('quotationitems', 'VariantId', "int NULL");
CALL suite_add_col('saleitems', 'VariantId', "int NULL");
CALL suite_add_col('saleitems', 'ModifiersJson', "longtext CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('salepayments', 'CustomerReceiptId', "int NULL");
CALL suite_add_col('sales', 'LocationId', "int NULL");
CALL suite_add_col('sales', 'PriceLevelId', "int NULL");
CALL suite_add_col('sales', 'PreviousBalance', "decimal(18,2) NULL");
CALL suite_add_col('sales', 'CreditOverrideBy', "varchar(200) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('sales', 'Channel', "varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'POS'");
CALL suite_add_col('sales', 'OrderType', "varchar(20) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('sales', 'DiningTableId', "int NULL");
CALL suite_add_col('sales', 'Covers', "int NULL");
CALL suite_add_col('salesreturnitems', 'VariantId', "int NULL");
CALL suite_add_col('stockadjustments', 'LocationId', "int NULL");
CALL suite_add_col('stockadjustments', 'VariantId', "int NULL");
CALL suite_add_col('stocktransferitems', 'VariantId', "int NULL");
CALL suite_add_col('stocktransferitems', 'BatchId', "int NULL");
CALL suite_add_col('stocktransferitems', 'BatchNo', "varchar(100) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('stocktransferitems', 'ExpiryDate', "datetime(6) NULL");
CALL suite_add_col('stocktransferitems', 'ReceivedBatchId', "int NULL");
CALL suite_add_col('stocktransferitems', 'ReceivedQty', "int NULL");
CALL suite_add_col('stocktransferitems', 'UnitCost', "decimal(18,4) NOT NULL DEFAULT 0");
CALL suite_add_col('stocktransfers', 'FromLocationId', "int NULL");
CALL suite_add_col('stocktransfers', 'ToLocationId', "int NULL");
CALL suite_add_col('stocktransfers', 'Status', "varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Received'");
CALL suite_add_col('stocktransfers', 'DispatchedBy', "varchar(200) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('stocktransfers', 'DispatchedAt', "datetime(6) NULL");
CALL suite_add_col('stocktransfers', 'ReceivedBy', "varchar(200) CHARACTER SET utf8mb4 NULL");
CALL suite_add_col('stocktransfers', 'ReceivedAt', "datetime(6) NULL");
CALL suite_add_col('stocktransfers', 'StockRequestId', "int NULL");
CALL suite_add_col('tenants', 'DefaultLanguage', "varchar(2) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'en'");
CALL suite_add_col('tillsessions', 'LocationId', "int NULL");

-- ── Indexes ──
CALL suite_add_index('productbatches', 'IX_ProductBatches_TenantId_LocationId_ProductId', '`TenantId`,`LocationId`,`ProductId`');
CALL suite_add_index('products', 'IX_Products_TenantId_ArticleNo', '`TenantId`,`ArticleNo`');
CALL suite_add_index('salepayments', 'IX_SalePayments_CustomerReceiptId', '`CustomerReceiptId`');
CALL suite_add_index('sales', 'IX_Sales_TenantId_CustomerId', '`TenantId`,`CustomerId`');
CALL suite_add_index('sales', 'IX_Sales_TenantId_LocationId_SaleDate', '`TenantId`,`LocationId`,`SaleDate`');
CALL suite_add_index('stocktransferitems', 'IX_StockTransferItems_ReceivedBatchId', '`ReceivedBatchId`');

SET FOREIGN_KEY_CHECKS = 1;

-- =====================================================================
-- Backfill: existing data into the new structure (each step only touches rows not done yet)
-- =====================================================================

-- 1. Locations from the old store and warehouse lists.
INSERT INTO `locations` (`TenantId`, `Code`, `Name`, `Type`, `IsDefault`, `SellsOnline`, `IsActive`, `CreatedAt`)
SELECT s.TenantId, UPPER(LEFT(s.Value, 20)), s.Label, 'Branch', 0, 0, 1, UTC_TIMESTAMP(6)
FROM `stores` s
WHERE s.Value <> ''
  AND NOT EXISTS (SELECT 1 FROM `locations` l WHERE l.TenantId = s.TenantId AND l.Code = UPPER(LEFT(s.Value, 20)));

INSERT INTO `locations` (`TenantId`, `Code`, `Name`, `Type`, `IsDefault`, `SellsOnline`, `IsActive`, `CreatedAt`)
SELECT w.TenantId, UPPER(LEFT(w.Value, 20)), w.Label, 'Warehouse', 0, 0, 1, UTC_TIMESTAMP(6)
FROM `warehouses` w
WHERE w.Value <> ''
  AND NOT EXISTS (SELECT 1 FROM `locations` l WHERE l.TenantId = w.TenantId AND l.Code = UPPER(LEFT(w.Value, 20)));

-- Every tenant gets a "Main Branch" default location if it has none.
INSERT INTO `locations` (`TenantId`, `Code`, `Name`, `Type`, `IsDefault`, `SellsOnline`, `IsActive`, `CreatedAt`)
SELECT t.Id, 'MAIN', 'Main Branch', 'Branch', 1, 1, 1, UTC_TIMESTAMP(6)
FROM `tenants` t
WHERE NOT EXISTS (SELECT 1 FROM `locations` l WHERE l.TenantId = t.Id AND l.Code = 'MAIN')
  AND NOT EXISTS (SELECT 1 FROM `locations` l WHERE l.TenantId = t.Id AND l.IsDefault = 1);

-- Tenants whose locations all came from the old lists: their first branch becomes the default.
UPDATE `locations` l
JOIN (SELECT TenantId, MIN(Id) AS Id FROM `locations` WHERE Type <> 'Warehouse' GROUP BY TenantId) f ON f.Id = l.Id
SET l.IsDefault = 1, l.SellsOnline = 1
WHERE NOT EXISTS (SELECT 1 FROM (SELECT TenantId FROM `locations` WHERE IsDefault = 1) d WHERE d.TenantId = l.TenantId);

-- Helper: default location per tenant.
DROP TEMPORARY TABLE IF EXISTS tmp_default_location;
CREATE TEMPORARY TABLE tmp_default_location AS
SELECT TenantId, MIN(Id) AS LocationId FROM `locations` WHERE IsDefault = 1 GROUP BY TenantId;

-- 2. Batches: the location named in Store, else the default.
UPDATE `productbatches` b
LEFT JOIN `locations` l ON l.TenantId = b.TenantId AND (l.Code = UPPER(b.Store) COLLATE utf8mb4_general_ci OR l.Name = b.Store COLLATE utf8mb4_general_ci)
JOIN tmp_default_location d ON d.TenantId = b.TenantId
SET b.LocationId = COALESCE(l.Id, d.LocationId)
WHERE b.LocationId IS NULL;

-- 3. Documents: sales, purchases, tills, returns, adjustments.
UPDATE `sales` s
LEFT JOIN `locations` l ON l.TenantId = s.TenantId AND (l.Code = UPPER(s.Store) COLLATE utf8mb4_general_ci OR l.Name = s.Store COLLATE utf8mb4_general_ci)
JOIN tmp_default_location d ON d.TenantId = s.TenantId
SET s.LocationId = COALESCE(l.Id, d.LocationId)
WHERE s.LocationId IS NULL;

UPDATE `sales` SET `Channel` = 'Web' WHERE `Source` = 'online' AND `Channel` = 'POS';

UPDATE `purchases` p JOIN tmp_default_location d ON d.TenantId = p.TenantId
SET p.LocationId = d.LocationId WHERE p.LocationId IS NULL;

UPDATE `tillsessions` t
LEFT JOIN `locations` l ON l.TenantId = t.TenantId AND (l.Code = UPPER(t.Store) COLLATE utf8mb4_general_ci OR l.Name = t.Store COLLATE utf8mb4_general_ci)
JOIN tmp_default_location d ON d.TenantId = t.TenantId
SET t.LocationId = COALESCE(l.Id, d.LocationId)
WHERE t.LocationId IS NULL;

UPDATE `purchasereturns` r JOIN tmp_default_location d ON d.TenantId = r.TenantId
SET r.LocationId = d.LocationId WHERE r.LocationId IS NULL;

UPDATE `stockadjustments` a
LEFT JOIN `productbatches` b ON b.Id = a.BatchId
JOIN tmp_default_location d ON d.TenantId = a.TenantId
SET a.LocationId = COALESCE(b.LocationId, d.LocationId)
WHERE a.LocationId IS NULL;

-- 4. Old transfers: link the from/to names to locations (they never moved stock, so they stay as records).
UPDATE `stocktransfers` t
LEFT JOIN `locations` lf ON lf.TenantId = t.TenantId AND (lf.Code = UPPER(t.WarehouseFrom) COLLATE utf8mb4_general_ci OR lf.Name = t.WarehouseFrom COLLATE utf8mb4_general_ci)
LEFT JOIN `locations` lt ON lt.TenantId = t.TenantId AND (lt.Code = UPPER(t.WarehouseTo) COLLATE utf8mb4_general_ci OR lt.Name = t.WarehouseTo COLLATE utf8mb4_general_ci)
SET t.FromLocationId = lf.Id, t.ToLocationId = lt.Id
WHERE t.FromLocationId IS NULL AND t.ToLocationId IS NULL;

-- 5. Start the stock ledger: one opening movement per batch that holds stock and has no history yet.
INSERT INTO `stockmovements` (`TenantId`, `ProductId`, `VariantId`, `LocationId`, `BatchId`, `Quantity`, `UnitCost`, `MovementType`, `SourceType`, `Reference`, `CreatedBy`, `CreatedAt`)
SELECT b.TenantId, b.ProductId, b.VariantId, b.LocationId, b.Id, b.Quantity, b.PurchasePrice, 'Opening', 'Migration', 'Balance at go-live', 'Migration', UTC_TIMESTAMP(6)
FROM `productbatches` b
WHERE b.Quantity <> 0
  AND NOT EXISTS (SELECT 1 FROM `stockmovements` m WHERE m.BatchId = b.Id);

-- 6. Users keep working at the default branch (not restricted).
UPDATE `parties` p JOIN tmp_default_location d ON d.TenantId = p.TenantId
SET p.LocationId = d.LocationId
WHERE p.LocationId IS NULL AND p.Role NOT IN ('Customer', 'Supplier', 'Biller', 'Employee') AND p.PasswordHash IS NOT NULL;

DROP TEMPORARY TABLE IF EXISTS tmp_default_location;

DROP PROCEDURE IF EXISTS suite_add_col;
DROP PROCEDURE IF EXISTS suite_add_index;
