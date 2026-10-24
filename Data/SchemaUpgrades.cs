using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace ReactPosApi.Data;

/// <summary>
/// EnsureCreated() only builds a brand-new database, so tables added later are created here
/// (idempotent - safe to run on every start, on local and live databases).
/// </summary>
public static class SchemaUpgrades
{
    public static void Apply(AppDbContext db)
    {
        AddPurchaseOrders(db);
        AddQuotationSaleLink(db);
        AddTermsConditions(db);
        AddTaxTypes(db);
        AddPurchaseOrderRevisionsAndPayments(db);
        BackfillQuotationReferences(db);
        AddSalesReturnSaleLink(db);
        // product colour and model
        if (!HasColumn(db, "Products", "Color"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Products` ADD COLUMN `Color` varchar(100) NULL");
        if (!HasColumn(db, "Products", "Model"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Products` ADD COLUMN `Model` varchar(150) NULL");
        // POS Orders moved out of the "Sales" dropdown: its permission key is now "Sales/POS Orders"
        db.Database.ExecuteSqlRaw("UPDATE IGNORE `RolePermissions` SET `MenuKey` = 'Sales/POS Orders' WHERE `MenuKey` = 'Sales/Sales'");
        db.Database.ExecuteSqlRaw("DELETE FROM `RolePermissions` WHERE `MenuKey` = 'Sales/Sales'");
        db.Database.ExecuteSqlRaw("UPDATE IGNORE `TenantMenuConfigs` SET `MenuKey` = 'Sales/POS Orders' WHERE `MenuKey` = 'Sales/Sales'");
        db.Database.ExecuteSqlRaw("DELETE FROM `TenantMenuConfigs` WHERE `MenuKey` = 'Sales/Sales'");
        // customers saved with a blank email before it became optional: NULL, so they don't clash on the unique email index
        db.Database.ExecuteSqlRaw("UPDATE `Parties` SET `Email` = NULL WHERE `Role` = 'Customer' AND `Email` = ''");
    }

    private static bool HasColumn(AppDbContext db, string table, string column) =>
        db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS " +
            $"WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = '{table.ToLower()}' AND COLUMN_NAME = '{column}'")
            .AsEnumerable().First() > 0;

    /// <summary>Terms & conditions printed on quotations / purchase orders: one row per tenant, filled with the default text</summary>
    private static void AddTermsConditions(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `TermsConditions` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Content` longtext NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `UpdatedBy` varchar(150) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_TermsConditions_TenantId` (`TenantId`)
) CHARACTER SET utf8mb4");

        db.Database.ExecuteSqlRaw(
            "INSERT INTO `TermsConditions` (`TenantId`, `Content`, `UpdatedAt`) " +
            "SELECT t.`Id`, {0}, UTC_TIMESTAMP(6) FROM `Tenants` t " +
            "WHERE NOT EXISTS (SELECT 1 FROM `TermsConditions` tc WHERE tc.`TenantId` = t.`Id`)",
            Models.DefaultTermsConditions.Text.Trim());
    }

    /// <summary>Sales returns can belong to a POS sale; their lines point at the product and sale line</summary>
    private static void AddSalesReturnSaleLink(AppDbContext db)
    {
        if (!HasColumn(db, "SalesReturns", "SaleId"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `SalesReturns` ADD COLUMN `SaleId` int NULL, ADD COLUMN `SaleReference` varchar(50) NULL, ADD INDEX `IX_SalesReturns_SaleId` (`SaleId`)");
        if (!HasColumn(db, "SalesReturnItems", "ProductId"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `SalesReturnItems` ADD COLUMN `ProductId` int NULL, ADD COLUMN `SaleItemId` int NULL");
    }

    /// <summary>
    /// Quotations saved without a reference get the next QT-0001, QT-0002 ... (per company, oldest first);
    /// their revisions are renamed to match (QT-0003_R1 ...).
    /// </summary>
    private static void BackfillQuotationReferences(AppDbContext db)
    {
        var all = db.Quotations.IgnoreQueryFilters().ToList();
        var changed = false;
        foreach (var company in all.GroupBy(q => q.TenantId))
        {
            var max = company.Select(q => Regex.Match(q.Reference ?? "", @"^QT-(\d+)$")).Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
            foreach (var original in company.Where(q => q.RootQuotationId == null && string.IsNullOrWhiteSpace(q.Reference)).OrderBy(q => q.Id))
            {
                original.Reference = $"QT-{++max:D4}";
                foreach (var revision in company.Where(q => q.RootQuotationId == original.Id))
                    revision.Reference = $"{original.Reference}_R{revision.RevisionNo}";
                changed = true;
            }
        }
        if (changed) db.SaveChanges();
    }

    /// <summary>Purchase order revisions (PO-0001_R1 ...) and the payment / refund records of purchases</summary>
    private static void AddPurchaseOrderRevisionsAndPayments(AppDbContext db)
    {
        if (!HasColumn(db, "PurchaseOrders", "RevisionNo"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `PurchaseOrders` ADD COLUMN `RootPurchaseOrderId` int NULL, ADD COLUMN `RevisionNo` int NOT NULL DEFAULT 0, ADD COLUMN `SupersededById` int NULL, ADD INDEX `IX_PurchaseOrders_RootPurchaseOrderId` (`RootPurchaseOrderId`)");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PurchasePayments` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PurchaseId` int NOT NULL,
  `PurchaseOrderId` int NULL,
  `Type` varchar(20) NOT NULL,
  `Amount` decimal(18,2) NOT NULL,
  `Method` varchar(30) NOT NULL,
  `Reference` varchar(60) NULL,
  `Date` datetime(6) NOT NULL,
  `Notes` varchar(500) NULL,
  `CreatedBy` varchar(150) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_PurchasePayments_PurchaseId` (`PurchaseId`),
  KEY `IX_PurchasePayments_PurchaseOrderId` (`PurchaseOrderId`),
  CONSTRAINT `FK_PurchasePayments_Purchases_PurchaseId` FOREIGN KEY (`PurchaseId`) REFERENCES `Purchases` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4");

        // purchases paid before payments were recorded one by one: one opening record each, so the history adds up
        db.Database.ExecuteSqlRaw(@"
INSERT INTO `PurchasePayments` (`TenantId`, `PurchaseId`, `PurchaseOrderId`, `Type`, `Amount`, `Method`, `Reference`, `Date`, `Notes`, `CreatedAt`)
SELECT p.`TenantId`, p.`Id`, p.`PurchaseOrderId`, 'Payment', p.`Paid`, 'Not recorded', LEFT(CONCAT(p.`Reference`, '-P1'), 60), p.`Date`,
       'Paid when the purchase was recorded', UTC_TIMESTAMP(6)
FROM `Purchases` p
WHERE p.`Paid` > 0 AND NOT EXISTS (SELECT 1 FROM `PurchasePayments` x WHERE x.`PurchaseId` = p.`Id`)");
    }

    /// <summary>Tax types + the taxes applied to products, quotations, purchase orders and sales</summary>
    private static void AddTaxTypes(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `TaxTypes` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(100) NOT NULL,
  `Description` varchar(300) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_TaxTypes_TenantId_Name` (`TenantId`, `Name`)
) CHARACTER SET utf8mb4");

        // quotation revisions (QT-005 -> QT-005_R1 -> QT-005_R2)
        if (!HasColumn(db, "Quotations", "RevisionNo"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Quotations` ADD COLUMN `RootQuotationId` int NULL, ADD COLUMN `RevisionNo` int NOT NULL DEFAULT 0, ADD COLUMN `SupersededById` int NULL, ADD INDEX `IX_Quotations_RootQuotationId` (`RootQuotationId`)");

        // terms & conditions edited for a single quotation
        if (!HasColumn(db, "Quotations", "TermsContent"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Quotations` ADD COLUMN `TermsContent` longtext NULL");

        foreach (var table in new[] { "Products", "Quotations", "PurchaseOrders", "Sales" })
            if (!HasColumn(db, table, "TaxesJson"))
                db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` ADD COLUMN `TaxesJson` longtext NULL");
    }

    /// <summary>Quotations converted into a sale keep a link to it</summary>
    private static void AddQuotationSaleLink(AppDbContext db)
    {
        if (!HasColumn(db, "Quotations", "SaleId"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Quotations` ADD COLUMN `SaleId` int NULL, ADD COLUMN `SaleReference` varchar(50) NULL");
    }

    private static void AddPurchaseOrders(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PurchaseOrders` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PoNumber` varchar(30) NOT NULL,
  `SupplierId` int NULL,
  `SupplierName` varchar(150) NOT NULL,
  `SupplierPhone` varchar(30) NULL,
  `OrderDate` datetime(6) NOT NULL,
  `ExpectedDate` datetime(6) NULL,
  `Status` varchar(30) NOT NULL DEFAULT 'Draft',
  `SubTotal` decimal(18,2) NOT NULL,
  `OrderTax` decimal(18,2) NOT NULL,
  `Discount` decimal(18,2) NOT NULL,
  `Shipping` decimal(18,2) NOT NULL,
  `Total` decimal(18,2) NOT NULL,
  `Notes` varchar(500) NULL,
  `CreatedBy` varchar(150) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_PurchaseOrders_TenantId_PoNumber` (`TenantId`, `PoNumber`)
) CHARACTER SET utf8mb4");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PurchaseOrderItems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PurchaseOrderId` int NOT NULL,
  `ProductId` int NOT NULL,
  `ProductName` varchar(200) NOT NULL,
  `Sku` varchar(100) NULL,
  `Quantity` int NOT NULL,
  `ReceivedQuantity` int NOT NULL,
  `UnitCost` decimal(18,2) NOT NULL,
  `Discount` decimal(18,2) NOT NULL,
  `TaxPercent` decimal(18,2) NOT NULL,
  `TaxAmount` decimal(18,2) NOT NULL,
  `TotalCost` decimal(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_PurchaseOrderItems_PurchaseOrderId` (`PurchaseOrderId`),
  KEY `IX_PurchaseOrderItems_ProductId` (`ProductId`),
  CONSTRAINT `FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId` FOREIGN KEY (`PurchaseOrderId`) REFERENCES `PurchaseOrders` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_PurchaseOrderItems_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4");

        // Purchases created by receiving a purchase order point back to it
        var hasColumn = db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'purchases' AND COLUMN_NAME = 'PurchaseOrderId'")
            .AsEnumerable().First() > 0;
        if (!hasColumn)
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE `Purchases` ADD COLUMN `PurchaseOrderId` int NULL, ADD INDEX `IX_Purchases_PurchaseOrderId` (`PurchaseOrderId`)");
        }
    }
}
