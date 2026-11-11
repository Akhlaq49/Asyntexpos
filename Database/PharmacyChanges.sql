-- =====================================================================
-- Pharmacy changes (branch feature/pharma-chnages)
--
-- Safe to run more than once. Works on MySQL 8+ and MariaDB 10.5+.
-- The app uses EnsureCreated (no EF migrations), so existing databases
-- need this script; a brand-new empty database gets the tables from EF.
--
-- Run:  mysql -u <user> -p <database> < Database/PharmacyChanges.sql
-- Take a backup first (Database/Backup-Database.ps1).
-- =====================================================================

DROP PROCEDURE IF EXISTS pharma_add_col;
DROP PROCEDURE IF EXISTS pharma_add_index;

DELIMITER $$
CREATE PROCEDURE pharma_add_col(IN t VARCHAR(64), IN c VARCHAR(64), IN def TEXT)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                   WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(t) AND COLUMN_NAME = c) THEN
        SET @ddl = CONCAT('ALTER TABLE `', t, '` ADD COLUMN `', c, '` ', def);
        PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END$$

CREATE PROCEDURE pharma_add_index(IN t VARCHAR(64), IN idx VARCHAR(64), IN cols TEXT)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS
                   WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(t) AND INDEX_NAME = idx) THEN
        SET @ddl = CONCAT('CREATE INDEX `', idx, '` ON `', t, '` (', cols, ')');
        PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

-- ── Products: medicine details, trade price, pack sizes ──
CALL pharma_add_col('products', 'GenericName',        'varchar(200) NULL');
CALL pharma_add_col('products', 'Strength',           'varchar(100) NULL');
CALL pharma_add_col('products', 'DosageForm',         'varchar(50) NULL');
CALL pharma_add_col('products', 'RackLocation',       'varchar(100) NULL');
CALL pharma_add_col('products', 'IsPrescriptionOnly', 'tinyint(1) NOT NULL DEFAULT 0');
CALL pharma_add_col('products', 'IsControlled',       'tinyint(1) NOT NULL DEFAULT 0');
CALL pharma_add_col('products', 'PurchasePrice',      'decimal(18,4) NOT NULL DEFAULT 0');
CALL pharma_add_col('products', 'UnitsPerStrip',      'int NOT NULL DEFAULT 1');
CALL pharma_add_col('products', 'StripsPerBox',       'int NOT NULL DEFAULT 1');
CALL pharma_add_col('products', 'RefillDays',         'int NOT NULL DEFAULT 0');
CALL pharma_add_index('products', 'IX_Products_TenantId_GenericName', '`TenantId`, `GenericName`');

-- ── Sales: branch, till, payment method, prescription, FBR ──
CALL pharma_add_col('sales', 'Store',             'varchar(100) NULL');
CALL pharma_add_col('sales', 'TillSessionId',     'int NULL');
CALL pharma_add_col('sales', 'CouponCode',        'varchar(50) NULL');
CALL pharma_add_col('sales', 'PaymentMethod',     'varchar(30) NULL');
CALL pharma_add_col('sales', 'PatientName',       'varchar(200) NULL');
CALL pharma_add_col('sales', 'DoctorName',        'varchar(200) NULL');
CALL pharma_add_col('sales', 'PrescriptionImage', 'varchar(500) NULL');
CALL pharma_add_col('sales', 'FbrInvoiceNumber',  'varchar(100) NULL');
CALL pharma_add_col('sales', 'FbrStatus',         'varchar(20) NULL');
CALL pharma_add_index('sales', 'IX_Sales_TillSessionId', '`TillSessionId`');

CALL pharma_add_col('saleitems', 'CostPrice',    'decimal(18,4) NOT NULL DEFAULT 0');
CALL pharma_add_col('saleitems', 'SaleUnit',     'varchar(20) NOT NULL DEFAULT ''Unit''');
CALL pharma_add_col('saleitems', 'UnitQuantity', 'int NOT NULL DEFAULT 0');
CALL pharma_add_col('saleitems', 'BatchInfo',    'varchar(500) NULL');

-- ── Sales returns: link to the bill, refund method, condition per line ──
CALL pharma_add_col('salesreturns', 'SaleId',       'int NULL');
CALL pharma_add_col('salesreturns', 'RefundMethod', 'varchar(30) NULL');
CALL pharma_add_col('salesreturns', 'StockApplied', 'tinyint(1) NOT NULL DEFAULT 0');
CALL pharma_add_index('salesreturns', 'IX_SalesReturns_SaleId', '`SaleId`');

CALL pharma_add_col('salesreturnitems', 'ProductId',  'int NULL');
CALL pharma_add_col('salesreturnitems', 'SaleItemId', 'int NULL');
CALL pharma_add_col('salesreturnitems', 'Condition',  'varchar(20) NOT NULL DEFAULT ''Saleable''');

-- ── Purchases: batch, expiry, bonus quantity, purchase unit ──
CALL pharma_add_col('purchaseitems', 'BatchNo',          'varchar(100) NULL');
CALL pharma_add_col('purchaseitems', 'ExpiryDate',       'datetime(6) NULL');
CALL pharma_add_col('purchaseitems', 'ManufacturedDate', 'datetime(6) NULL');
CALL pharma_add_col('purchaseitems', 'BonusQuantity',    'decimal(18,2) NOT NULL DEFAULT 0');
CALL pharma_add_col('purchaseitems', 'PurchaseUnit',     'varchar(20) NULL DEFAULT ''Unit''');

-- ── Stock adjustments: reason and batch ──
CALL pharma_add_col('stockadjustments', 'Reason',  'varchar(50) NULL');
CALL pharma_add_col('stockadjustments', 'BatchId', 'int NULL');

-- ── New tables ──
CREATE TABLE IF NOT EXISTS `productbatches` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `ProductId` int NOT NULL,
    `BatchNo` varchar(100) NOT NULL,
    `ExpiryDate` datetime(6) NULL,
    `ManufacturedDate` datetime(6) NULL,
    `Quantity` int NOT NULL,
    `PurchasePrice` decimal(18,4) NOT NULL DEFAULT 0,
    `Supplier` varchar(200) NULL,
    `PurchaseId` int NULL,
    `PurchaseItemId` int NULL,
    `Store` varchar(100) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_ProductBatches_TenantId_ProductId_ExpiryDate` (`TenantId`, `ProductId`, `ExpiryDate`),
    KEY `IX_ProductBatches_TenantId_ExpiryDate` (`TenantId`, `ExpiryDate`),
    KEY `IX_ProductBatches_ProductId` (`ProductId`),
    CONSTRAINT `FK_ProductBatches_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `saleitembatches` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `SaleItemId` int NOT NULL,
    `BatchId` int NOT NULL,
    `Quantity` int NOT NULL,
    `UnitCost` decimal(18,4) NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    KEY `IX_SaleItemBatches_TenantId_SaleItemId` (`TenantId`, `SaleItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `heldbills` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `Label` varchar(100) NOT NULL,
    `CustomerId` int NULL,
    `CustomerName` varchar(200) NULL,
    `CartJson` longtext NOT NULL,
    `Total` decimal(18,2) NOT NULL DEFAULT 0,
    `UserName` varchar(100) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `purchasereturns` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `Reference` varchar(50) NOT NULL,
    `PurchaseId` int NULL,
    `SupplierName` varchar(200) NOT NULL,
    `Date` datetime(6) NOT NULL,
    `Total` decimal(18,2) NOT NULL DEFAULT 0,
    `Notes` varchar(500) NULL,
    `CreatedBy` varchar(100) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `purchasereturnitems` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `PurchaseReturnId` int NOT NULL,
    `ProductId` int NOT NULL,
    `ProductName` varchar(300) NOT NULL,
    `BatchId` int NULL,
    `BatchNo` varchar(100) NULL,
    `ExpiryDate` datetime(6) NULL,
    `Quantity` int NOT NULL,
    `UnitCost` decimal(18,4) NOT NULL DEFAULT 0,
    `Total` decimal(18,2) NOT NULL DEFAULT 0,
    `Reason` varchar(50) NOT NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_PurchaseReturnItems_PurchaseReturnId` (`PurchaseReturnId`),
    CONSTRAINT `FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId` FOREIGN KEY (`PurchaseReturnId`) REFERENCES `purchasereturns` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Supplier payments: the table may already exist (manufacturing module). Create it if missing, then add pharmacy columns.
CREATE TABLE IF NOT EXISTS `supplierpayments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `SupplierId` int NOT NULL DEFAULT 0,
    `Reference` varchar(100) NOT NULL DEFAULT '',
    `Amount` decimal(18,2) NOT NULL DEFAULT 0.00,
    `PaymentMethod` varchar(50) NOT NULL DEFAULT 'Cash',
    `Description` varchar(500) NULL,
    `PaymentDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `CreatedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CALL pharma_add_col('supplierpayments', 'SupplierName', 'varchar(200) NULL');
CALL pharma_add_col('supplierpayments', 'PurchaseId',   'int NULL');
CALL pharma_add_col('supplierpayments', 'CreatedBy',    'varchar(100) NULL');
CALL pharma_add_index('supplierpayments', 'IX_SupplierPayments_PurchaseId', '`PurchaseId`');

CREATE TABLE IF NOT EXISTS `tillsessions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `UserName` varchar(100) NOT NULL,
    `Store` varchar(100) NULL,
    `OpenedAt` datetime(6) NOT NULL,
    `ClosedAt` datetime(6) NULL,
    `OpeningCash` decimal(18,2) NOT NULL DEFAULT 0,
    `CashSales` decimal(18,2) NOT NULL DEFAULT 0,
    `CardSales` decimal(18,2) NOT NULL DEFAULT 0,
    `CreditSales` decimal(18,2) NOT NULL DEFAULT 0,
    `CashRefunds` decimal(18,2) NOT NULL DEFAULT 0,
    `ExpectedCash` decimal(18,2) NOT NULL DEFAULT 0,
    `CountedCash` decimal(18,2) NULL,
    `Notes` varchar(500) NULL,
    `Status` varchar(20) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `auditlogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `UserName` varchar(100) NOT NULL,
    `Action` varchar(50) NOT NULL,
    `EntityType` varchar(50) NOT NULL,
    `EntityId` varchar(50) NULL,
    `Details` longtext NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_AuditLogs_TenantId_CreatedAt` (`TenantId`, `CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `refillreminders` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `CustomerId` int NOT NULL,
    `ProductId` int NOT NULL,
    `SaleId` int NOT NULL,
    `DueDate` datetime(6) NOT NULL,
    `SentAt` datetime(6) NOT NULL,
    `Channel` varchar(20) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_RefillReminders_TenantId_SaleId_ProductId` (`TenantId`, `SaleId`, `ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `pharmacysettings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL DEFAULT 0,
    `PharmacyName` varchar(200) NOT NULL,
    `Address` varchar(500) NULL,
    `Phone` varchar(50) NULL,
    `DrugLicenceNo` varchar(100) NULL,
    `Ntn` varchar(50) NULL,
    `Strn` varchar(50) NULL,
    `ReceiptFooter` varchar(500) NULL,
    `DefaultTaxPercent` decimal(5,2) NOT NULL DEFAULT 0,
    `MaxCashierDiscountPercent` decimal(5,2) NOT NULL DEFAULT 10,
    `ExpiryAlertDays1` int NOT NULL DEFAULT 90,
    `ExpiryAlertDays2` int NOT NULL DEFAULT 60,
    `ExpiryAlertDays3` int NOT NULL DEFAULT 30,
    `RequirePrescription` tinyint(1) NOT NULL DEFAULT 1,
    `RefillRemindersEnabled` tinyint(1) NOT NULL DEFAULT 0,
    `FbrEnabled` tinyint(1) NOT NULL DEFAULT 0,
    `FbrPosId` varchar(50) NULL,
    `FbrToken` varchar(500) NULL,
    `FbrEnvironment` varchar(20) NOT NULL DEFAULT 'Sandbox',
    `UpdatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_PharmacySettings_TenantId` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- =====================================================================
-- Data backfill (each step skips rows it has already handled)
-- =====================================================================

-- 1. Trade price: take the latest purchase cost for products that have none.
UPDATE `products` p
JOIN (
    SELECT pi.ProductId, pi.UnitCost
    FROM `purchaseitems` pi
    JOIN (SELECT ProductId, MAX(Id) AS MaxId FROM `purchaseitems` WHERE ProductId IS NOT NULL AND UnitCost > 0 GROUP BY ProductId) last
      ON last.MaxId = pi.Id
) lc ON lc.ProductId = p.Id
SET p.PurchasePrice = lc.UnitCost
WHERE p.PurchasePrice = 0;

-- 2. Opening batch for every product that holds stock but has no batch yet.
--    The old single expiry date becomes the batch expiry.
INSERT INTO `productbatches` (`TenantId`, `ProductId`, `BatchNo`, `ExpiryDate`, `ManufacturedDate`, `Quantity`, `PurchasePrice`, `Supplier`, `Store`, `CreatedAt`, `UpdatedAt`)
SELECT p.TenantId, p.Id, 'OPENING',
       CASE WHEN p.ExpiryDate REGEXP '^[0-9]{4}-[0-9]{2}-[0-9]{2}' THEN STR_TO_DATE(LEFT(p.ExpiryDate, 10), '%Y-%m-%d') END,
       CASE WHEN p.ManufacturedDate REGEXP '^[0-9]{4}-[0-9]{2}-[0-9]{2}' THEN STR_TO_DATE(LEFT(p.ManufacturedDate, 10), '%Y-%m-%d') END,
       p.Quantity, p.PurchasePrice, NULL, p.Store, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM `products` p
WHERE p.Quantity > 0
  AND NOT EXISTS (SELECT 1 FROM `productbatches` b WHERE b.ProductId = p.Id);

-- 3. Payments made before payments were tracked: one row per purchase for the amount already paid.
INSERT INTO `supplierpayments` (`TenantId`, `SupplierId`, `SupplierName`, `PurchaseId`, `Reference`, `Amount`, `PaymentMethod`, `Description`, `PaymentDate`, `CreatedAt`, `CreatedBy`)
SELECT pu.TenantId,
       COALESCE((SELECT pa.Id FROM `parties` pa WHERE pa.TenantId = pu.TenantId AND pa.Role = 'Supplier'
                 AND (pa.FullName = pu.SupplierName OR pa.CompanyName = pu.SupplierName) LIMIT 1), 0),
       pu.SupplierName, pu.Id, pu.Reference, pu.Paid, 'Cash', 'Paid before payment tracking', pu.Date, UTC_TIMESTAMP(), 'Migration'
FROM `purchases` pu
WHERE pu.Paid > 0
  AND NOT EXISTS (SELECT 1 FROM `supplierpayments` sp WHERE sp.PurchaseId = pu.Id);

-- 4. Older bill lines: quantity in the sold unit equals the base quantity.
UPDATE `saleitems` SET `UnitQuantity` = `Quantity` WHERE `UnitQuantity` = 0;

-- 5. Counter bills: record how they were paid.
UPDATE `sales` s
SET s.PaymentMethod = COALESCE(
        (SELECT sp.PaymentType FROM `salepayments` sp WHERE sp.SaleId = s.Id ORDER BY sp.Id LIMIT 1),
        CASE WHEN s.Status = 'Pending' THEN 'Credit' END)
WHERE s.PaymentMethod IS NULL AND s.Source = 'pos';

-- 6. Older returns had no stock movement; mark them so editing or deleting them does not move stock either.
UPDATE `salesreturns` SET `StockApplied` = 0 WHERE `SaleId` IS NULL;

DROP PROCEDURE IF EXISTS pharma_add_col;
DROP PROCEDURE IF EXISTS pharma_add_index;
