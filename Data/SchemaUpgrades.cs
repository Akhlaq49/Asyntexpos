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
