-- ============================================================
-- MULTI-TENANCY MIGRATION SCRIPT
-- Run this against existing databases to add tenant support.
-- For NEW databases, EnsureCreated() handles schema automatically.
-- ============================================================

-- 1. Create the Tenants table
CREATE TABLE IF NOT EXISTS `Tenants` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Name` VARCHAR(200) NOT NULL,
    `Email` VARCHAR(200) NOT NULL,
    `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 2. Insert a default tenant for existing data
INSERT INTO `Tenants` (`Name`, `Email`, `IsActive`, `CreatedAt`)
SELECT 'System Admin', 'admin@reactpos.com', 1, UTC_TIMESTAMP()
WHERE NOT EXISTS (SELECT 1 FROM `Tenants` WHERE `Email` = 'admin@reactpos.com');

SET @DefaultTenantId = (SELECT `Id` FROM `Tenants` WHERE `Email` = 'admin@reactpos.com' LIMIT 1);

-- 3. Add TenantId column to ALL tenant-scoped tables
-- Each ALTER adds the column with default = @DefaultTenantId so existing rows are assigned

ALTER TABLE `Parties`              ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Stores`               ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Warehouses`           ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Brands`               ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Units`                ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `VariantAttributes`    ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Warranties`           ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Categories`           ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `SubCategories`        ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Products`             ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `ProductImages`        ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Sales`                ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `SaleItems`            ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `SalePayments`         ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Invoices`             ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `InvoiceItems`         ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `SalesReturns`         ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `SalesReturnItems`     ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Quotations`           ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `QuotationItems`       ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Coupons`              ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Purchases`            ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `PurchaseItems`        ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `StockEntries`         ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `StockAdjustments`     ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `StockTransfers`       ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `StockTransferItems`   ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `InstallmentPlans`     ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `RepaymentEntries`     ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `PlanGuarantors`       ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `MiscellaneousRegisters` ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `PartyAddresses`       ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `RolePermissions`      ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `FormFieldConfigs`     ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Departments`          ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Designations`         ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Shifts`               ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `LeaveTypes`           ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Leaves`               ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Holidays`             ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Payrolls`             ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Attendances`          ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `ExpenseCategories`    ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `Expenses`             ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `IncomeCategories`     ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `FinanceIncomes`       ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `AccountTypes`         ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;
ALTER TABLE `BankAccounts`         ADD COLUMN IF NOT EXISTS `TenantId` INT NOT NULL DEFAULT 0;

-- 4. Assign all existing data to the default tenant
UPDATE `Parties`               SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Stores`                SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Warehouses`            SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Brands`                SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Units`                 SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `VariantAttributes`     SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Warranties`            SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Categories`            SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `SubCategories`         SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Products`              SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `ProductImages`         SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Sales`                 SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `SaleItems`             SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `SalePayments`          SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Invoices`              SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `InvoiceItems`          SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `SalesReturns`          SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `SalesReturnItems`      SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Quotations`            SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `QuotationItems`        SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Coupons`               SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Purchases`             SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `PurchaseItems`         SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `StockEntries`          SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `StockAdjustments`      SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `StockTransfers`        SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `StockTransferItems`    SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `InstallmentPlans`      SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `RepaymentEntries`      SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `PlanGuarantors`        SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `MiscellaneousRegisters` SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `PartyAddresses`        SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `RolePermissions`       SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `FormFieldConfigs`      SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Departments`           SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Designations`          SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Shifts`                SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `LeaveTypes`            SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Leaves`                SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Holidays`              SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Payrolls`              SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Attendances`           SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `ExpenseCategories`     SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `Expenses`              SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `IncomeCategories`      SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `FinanceIncomes`        SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `AccountTypes`          SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;
UPDATE `BankAccounts`          SET `TenantId` = @DefaultTenantId WHERE `TenantId` = 0;

-- 5. Update unique indexes to include TenantId (drop old, create new)

-- Categories: Slug unique per tenant
ALTER TABLE `Categories` DROP INDEX IF EXISTS `IX_Categories_Slug`;
CREATE UNIQUE INDEX `IX_Categories_TenantId_Slug` ON `Categories` (`TenantId`, `Slug`);

-- Parties: Email+Role unique per tenant
ALTER TABLE `Parties` DROP INDEX IF EXISTS `IX_Parties_Email_Role`;
CREATE UNIQUE INDEX `IX_Parties_Tenant_Email_Role` ON `Parties` (`TenantId`, `Email`, `Role`);

-- Attendance: EmployeeId+Date unique per tenant
ALTER TABLE `Attendances` DROP INDEX IF EXISTS `IX_Attendances_EmployeeId_Date`;
CREATE UNIQUE INDEX `IX_Attendances_TenantId_EmployeeId_Date` ON `Attendances` (`TenantId`, `EmployeeId`, `Date`);

-- Coupons: Code unique per tenant
ALTER TABLE `Coupons` DROP INDEX IF EXISTS `IX_Coupons_Code`;
CREATE UNIQUE INDEX `IX_Coupons_TenantId_Code` ON `Coupons` (`TenantId`, `Code`);

-- RolePermissions: Role+MenuKey unique per tenant
ALTER TABLE `RolePermissions` DROP INDEX IF EXISTS `IX_RolePermissions_Role_MenuKey`;
CREATE UNIQUE INDEX `IX_RolePermissions_TenantId_Role_MenuKey` ON `RolePermissions` (`TenantId`, `Role`, `MenuKey`);

-- FormFieldConfigs: FormName+FieldName unique per tenant
ALTER TABLE `FormFieldConfigs` DROP INDEX IF EXISTS `IX_FormFieldConfigs_FormName_FieldName`;
CREATE UNIQUE INDEX `IX_FormFieldConfigs_TenantId_FormName_FieldName` ON `FormFieldConfigs` (`TenantId`, `FormName`, `FieldName`);

-- 6. Add indexes on TenantId for query performance
CREATE INDEX IF NOT EXISTS `IX_Parties_TenantId` ON `Parties` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_Products_TenantId` ON `Products` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_Sales_TenantId` ON `Sales` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_SaleItems_TenantId` ON `SaleItems` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_Purchases_TenantId` ON `Purchases` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_InstallmentPlans_TenantId` ON `InstallmentPlans` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_RepaymentEntries_TenantId` ON `RepaymentEntries` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_Expenses_TenantId` ON `Expenses` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_FinanceIncomes_TenantId` ON `FinanceIncomes` (`TenantId`);
CREATE INDEX IF NOT EXISTS `IX_Invoices_TenantId` ON `Invoices` (`TenantId`);

-- Done! All existing data is now assigned to the default tenant.
-- New registrations will create new tenants automatically.
