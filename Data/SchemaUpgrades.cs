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
        // soft delete columns first: every entity query below reads them (run again at the end for tables created here)
        AddSoftDeleteColumns(db);
        // columns read by the entity queries below (e.g. the quotation reference backfill) must exist first
        // installation, testing and commissioning charge on quotations (and the sales made from them)
        foreach (var table in new[] { "Quotations", "Sales" })
            if (!HasColumn(db, table, "InstallationCharges"))
                db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` ADD COLUMN `InstallationCharges` decimal(18,2) NOT NULL DEFAULT 0");
        // customer's building: type, size, construction stage
        if (!HasColumn(db, "Parties", "BuildingType"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Parties` ADD COLUMN `BuildingType` varchar(50) NULL, ADD COLUMN `BuildingSpace` varchar(50) NULL, ADD COLUMN `BuildingStage` varchar(50) NULL");
        // a part-paid sale is "Partial" (it used to be called "Overdue" straight away)
        db.Database.ExecuteSqlRaw("UPDATE `Sales` SET `PaymentStatus` = 'Partial' WHERE `PaymentStatus` = 'Overdue'");
        // quotations whose sale was deleted are free to convert again
        db.Database.ExecuteSqlRaw("UPDATE `Quotations` SET `SaleId` = NULL, `SaleReference` = NULL, `Status` = IF(`Status` = 'Converted', 'Pending', `Status`) WHERE `SaleId` IS NOT NULL AND `SaleId` NOT IN (SELECT `Id` FROM `Sales` WHERE `IsDeleted` = 0)");
        // a user frozen by an admin cannot sign in
        if (!HasColumn(db, "Parties", "IsFrozen"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Parties` ADD COLUMN `IsFrozen` tinyint(1) NOT NULL DEFAULT 0, ADD COLUMN `FrozenAt` datetime(6) NULL, ADD COLUMN `FrozenBy` varchar(150) NULL");
        // who made the quotation (staff only see their own)
        if (!HasColumn(db, "Quotations", "CreatedById"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Quotations` ADD COLUMN `CreatedById` int NULL, ADD COLUMN `CreatedByName` varchar(150) NULL, ADD INDEX `IX_Quotations_CreatedById` (`CreatedById`)");
        // staff approval of quotations
        if (!HasColumn(db, "Quotations", "ApprovalStatus"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `Quotations` ADD COLUMN `ApprovalStatus` varchar(20) NULL, ADD COLUMN `ApprovalSentTo` varchar(500) NULL, ADD COLUMN `ApprovalBy` varchar(150) NULL, ADD COLUMN `ApprovalAt` datetime(6) NULL, ADD COLUMN `ApprovalNote` varchar(500) NULL");
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `QuotationApprovals` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `QuotationId` int NOT NULL,
  `QuotationReference` varchar(50) NOT NULL,
  `CustomerName` varchar(150) NOT NULL,
  `GrandTotal` decimal(18,2) NOT NULL,
  `RequestedById` int NOT NULL,
  `RequestedByName` varchar(150) NOT NULL,
  `RequestedAt` datetime(6) NOT NULL,
  `Message` varchar(500) NULL,
  `RecipientIds` varchar(500) NOT NULL,
  `RecipientNames` varchar(500) NOT NULL,
  `Status` varchar(20) NOT NULL,
  `DecidedById` int NULL,
  `DecidedByName` varchar(150) NULL,
  `DecidedAt` datetime(6) NULL,
  `DecisionNote` varchar(500) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_QuotationApprovals_QuotationId` (`QuotationId`),
  KEY `IX_QuotationApprovals_TenantId_Status` (`TenantId`, `Status`)
) CHARACTER SET utf8mb4");
        AddPurchaseOrders(db);
        AddQuotationSaleLink(db);
        AddTermsConditions(db);
        AddTaxTypes(db);
        AddPurchaseOrderRevisionsAndPayments(db);
        BackfillQuotationReferences(db);
        AddSalesReturnSaleLink(db);
        AddPurchaseReturns(db);
        // staff roles per company (were hard-coded); every company gets the default list once
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `Roles` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(50) NOT NULL,
  `IsSystem` tinyint(1) NOT NULL DEFAULT 0,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Roles_TenantId_Name` (`TenantId`, `Name`)
) CHARACTER SET utf8mb4");
        foreach (var tenantId in db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToList())
            Services.RoleCatalog.EnsureDefaultsAsync(db, tenantId).GetAwaiter().GetResult();
        // purchase orders raised for a converted quotation (customer order)
        if (!HasColumn(db, "PurchaseOrders", "QuotationId"))
            db.Database.ExecuteSqlRaw("ALTER TABLE `PurchaseOrders` ADD COLUMN `QuotationId` int NULL, ADD COLUMN `QuotationReference` varchar(50) NULL, ADD COLUMN `SaleReference` varchar(50) NULL, ADD INDEX `IX_PurchaseOrders_QuotationId` (`QuotationId`)");
        // owner's capital / drawings, loans, fixed assets (for the balance sheet)
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `FinanceEntries` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Type` varchar(30) NOT NULL,
  `Name` varchar(150) NOT NULL,
  `Amount` decimal(18,2) NOT NULL,
  `Method` varchar(30) NOT NULL,
  `Date` datetime(6) NOT NULL,
  `Reference` varchar(60) NULL,
  `Notes` varchar(500) NULL,
  `CreatedBy` varchar(150) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_FinanceEntries_TenantId_Type` (`TenantId`, `Type`)
) CHARACTER SET utf8mb4");
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
        AddSolutionPackages(db);
        AddSoftDeleteColumns(db);
        AlignCollations(db);
        // every company gets the starting solution data once (after the soft delete columns: the seed reads them)
        foreach (var tenantId in db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToList())
            SolutionPackageDefaults.Ensure(db, tenantId);
    }

    /// <summary>
    /// Soft delete: IsDeleted / DeletedAt / DeletedBy on every table in the model except the settings tables,
    /// and DeletedKey (0 = live, the Id once deleted) in the unique indexes, so a deleted record never blocks
    /// a new one with the same email / number / name. Idempotent.
    /// </summary>
    private static void AddSoftDeleteColumns(AppDbContext db)
    {
        foreach (var et in db.Model.GetEntityTypes().Where(t => !t.IsOwned() && t.FindProperty(SoftDelete.IsDeleted) != null))
        {
            var table = et.GetTableName();
            if (table == null || !HasTable(db, table)) continue;   // created later in Apply - handled on the second run
            if (!HasColumn(db, table, SoftDelete.IsDeleted))
                db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` ADD COLUMN `IsDeleted` tinyint(1) NOT NULL DEFAULT 0, ADD COLUMN `DeletedAt` datetime(6) NULL, ADD COLUMN `DeletedBy` varchar(150) NULL");
            if (et.FindProperty(SoftDelete.DeletedKey) == null) continue;
            if (!HasColumn(db, table, SoftDelete.DeletedKey))
                db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` ADD COLUMN `DeletedKey` int NOT NULL DEFAULT 0");
            // unique indexes: (..columns) -> (..columns, DeletedKey); add the new one first (a foreign key may need an index on TenantId)
            foreach (var ix in et.GetIndexes().Where(i => i.IsUnique))
            {
                var cols = ix.Properties.Select(p => p.GetColumnName()).ToList();
                var name = ix.GetDatabaseName() ?? $"UX_{table}_{string.Join("_", cols)}";
                var existing = UniqueIndexes(db, table);
                if (!existing.Any(x => x.Columns.SequenceEqual(cols, StringComparer.OrdinalIgnoreCase)))
                    db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` ADD UNIQUE INDEX `{name}` ({string.Join(", ", cols.Select(c => $"`{c}`"))})");
                var old = cols.Where(c => !c.Equals(SoftDelete.DeletedKey, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var x in UniqueIndexes(db, table).Where(x => x.Columns.SequenceEqual(old, StringComparer.OrdinalIgnoreCase)))
                    db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` DROP INDEX `{x.Name}`");
            }
        }
    }

    private record UniqueIndex(string Name, List<string> Columns);

    private static List<UniqueIndex> UniqueIndexes(AppDbContext db, string table) =>
        db.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(INDEX_NAME, ':', GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)) AS `Value` FROM information_schema.STATISTICS " +
            $"WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = '{table.ToLower()}' AND NON_UNIQUE = 0 AND INDEX_NAME <> 'PRIMARY' GROUP BY INDEX_NAME")
            .AsEnumerable()
            .Select(v => { var i = v.IndexOf(':'); return new UniqueIndex(v[..i], v[(i + 1)..].Split(',').ToList()); })
            .ToList();

    private static bool HasTable(AppDbContext db, string table) =>
        db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.TABLES " +
            $"WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = '{table.ToLower()}'")
            .AsEnumerable().First() > 0;


    /// <summary>
    /// Tables created here get the server's default collation, which can differ from the tables EF created
    /// (comparing text across them then fails with "Illegal mix of collations") - give them the same one.
    /// </summary>
    private static void AlignCollations(AppDbContext db)
    {
        var target = db.Database.SqlQueryRaw<string>(
            "SELECT TABLE_COLLATION AS `Value` FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'parties'")
            .AsEnumerable().FirstOrDefault();
        if (string.IsNullOrEmpty(target) || !System.Text.RegularExpressions.Regex.IsMatch(target, "^[A-Za-z0-9_]+$")) return;
        var created = new[] { "PurchaseOrders", "PurchaseOrderItems", "TermsConditions", "TaxTypes", "PurchasePayments", "PurchaseReturns",
                              "PurchaseReturnItems", "PurchaseReturnRefunds", "FinanceEntries", "Roles", "QuotationApprovals",
                              "SolutionGroups", "SolutionProductLines", "PackageSizes", "PackageItems", "PackageQuantities" };
        var different = db.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME AS `Value` FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_COLLATION <> {0}", target)
            .AsEnumerable().ToList();
        foreach (var table in different.Where(t => created.Contains(t, StringComparer.OrdinalIgnoreCase)))
            db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` CONVERT TO CHARACTER SET utf8mb4 COLLATE {target}");
    }

    private static bool HasColumn(AppDbContext db, string table, string column) =>
        db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS " +
            $"WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = '{table.ToLower()}' AND COLUMN_NAME = '{column}'")
            .AsEnumerable().First() > 0;

    /// <summary>Solution packages: product categorization (groups, lines, Standard / Premium) and package quantities per building size</summary>
    private static void AddSolutionPackages(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `SolutionGroups` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(100) NOT NULL,
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_SolutionGroups_TenantId` (`TenantId`)
) CHARACTER SET utf8mb4");
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `SolutionProductLines` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `GroupId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `InStandard` tinyint(1) NOT NULL DEFAULT 0,
  `InPremium` tinyint(1) NOT NULL DEFAULT 0,
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_SolutionProductLines_GroupId` (`GroupId`),
  CONSTRAINT `FK_SolutionProductLines_SolutionGroups_GroupId` FOREIGN KEY (`GroupId`) REFERENCES `SolutionGroups` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4");
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PackageSizes` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(50) NOT NULL,
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_PackageSizes_TenantId` (`TenantId`)
) CHARACTER SET utf8mb4");
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PackageItems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `ProductLineId` int NULL,
  `ProductId` int NULL,
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_PackageItems_TenantId` (`TenantId`),
  KEY `IX_PackageItems_ProductLineId` (`ProductLineId`),
  CONSTRAINT `FK_PackageItems_SolutionProductLines_ProductLineId` FOREIGN KEY (`ProductLineId`) REFERENCES `SolutionProductLines` (`Id`) ON DELETE SET NULL
) CHARACTER SET utf8mb4");
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PackageQuantities` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PackageItemId` int NOT NULL,
  `PackageSizeId` int NOT NULL,
  `Quantity` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_PackageQuantities_PackageItemId_PackageSizeId` (`PackageItemId`, `PackageSizeId`),
  KEY `IX_PackageQuantities_PackageSizeId` (`PackageSizeId`),
  CONSTRAINT `FK_PackageQuantities_PackageItems_PackageItemId` FOREIGN KEY (`PackageItemId`) REFERENCES `PackageItems` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_PackageQuantities_PackageSizes_PackageSizeId` FOREIGN KEY (`PackageSizeId`) REFERENCES `PackageSizes` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4");
    }

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

    /// <summary>Goods sent back to the supplier from a received purchase, their items and the money refunded</summary>
    private static void AddPurchaseReturns(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PurchaseReturns` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Reference` varchar(30) NOT NULL,
  `PurchaseId` int NOT NULL,
  `PurchaseReference` varchar(50) NOT NULL,
  `PurchaseOrderId` int NULL,
  `SupplierName` varchar(150) NOT NULL,
  `ReturnDate` datetime(6) NOT NULL,
  `Total` decimal(18,2) NOT NULL,
  `Credited` decimal(18,2) NOT NULL,
  `Refunded` decimal(18,2) NOT NULL,
  `Notes` varchar(500) NULL,
  `CreatedBy` varchar(150) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_PurchaseReturns_TenantId_Reference` (`TenantId`, `Reference`),
  KEY `IX_PurchaseReturns_PurchaseId` (`PurchaseId`)
) CHARACTER SET utf8mb4");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PurchaseReturnItems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PurchaseReturnId` int NOT NULL,
  `PurchaseItemId` int NOT NULL,
  `ProductId` int NULL,
  `ProductName` varchar(100) NOT NULL,
  `Quantity` int NOT NULL,
  `UnitCost` decimal(18,2) NOT NULL,
  `Total` decimal(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_PurchaseReturnItems_PurchaseReturnId` (`PurchaseReturnId`),
  CONSTRAINT `FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId` FOREIGN KEY (`PurchaseReturnId`) REFERENCES `PurchaseReturns` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS `PurchaseReturnRefunds` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `PurchaseReturnId` int NOT NULL,
  `Amount` decimal(18,2) NOT NULL,
  `Method` varchar(30) NOT NULL,
  `Reference` varchar(60) NULL,
  `Date` datetime(6) NOT NULL,
  `Notes` varchar(500) NULL,
  `CreatedBy` varchar(150) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_PurchaseReturnRefunds_PurchaseReturnId` (`PurchaseReturnId`),
  CONSTRAINT `FK_PurchaseReturnRefunds_PurchaseReturns_PurchaseReturnId` FOREIGN KEY (`PurchaseReturnId`) REFERENCES `PurchaseReturns` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4");
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
