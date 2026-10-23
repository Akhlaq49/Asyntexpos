namespace ReactPosApi.Services;

/// <summary>
/// Provides the database schema description for the LLM agent to understand table structures.
/// </summary>
public static class AgentDbSchema
{
    public static string GetSchemaDescription() => @"
=== DATABASE SCHEMA (MySQL) ===
All tables have a TenantId column for multi-tenancy. Always filter by TenantId = @TenantId.

TABLE: Parties (Unified people table - customers, suppliers, users, admins)
  Id INT PK, TenantId INT, FullName VARCHAR, LastName VARCHAR, Email VARCHAR, Phone VARCHAR,
  PhoneWork VARCHAR, SO VARCHAR (Son/Daughter of), Cnic VARCHAR,
  Address VARCHAR, City VARCHAR, State VARCHAR, Country VARCHAR, ZipCode VARCHAR,
  Role VARCHAR (Admin|Manager|User|Customer|Guarantor|Supplier|Biller|Store|Warehouse|Employee),
  Status VARCHAR (active|inactive), IsActive BIT,
  Picture VARCHAR, Code VARCHAR, CompanyName VARCHAR, ContactPerson VARCHAR,
  PasswordHash VARCHAR, UserName VARCHAR, CreatedAt DATETIME

TABLE: Products
  Id INT PK, TenantId INT, ProductName VARCHAR, Slug VARCHAR,
  SKU VARCHAR, Category VARCHAR, Brand VARCHAR, Unit VARCHAR,
  ItemBarcode VARCHAR, Description TEXT, ProductType VARCHAR (single|variable),
  Quantity INT, Price DECIMAL, QuantityAlert INT, Warranty VARCHAR,
  Tax DECIMAL, DiscountType VARCHAR, DiscountValue DECIMAL,
  ExpiryDate VARCHAR, ManufacturedDate VARCHAR, CreatedAt DATETIME

TABLE: ProductImages
  Id INT PK, ProductId INT FK->Products, ImagePath VARCHAR

TABLE: Sales
  Id INT PK, TenantId INT, Reference VARCHAR, CustomerId INT FK->Parties,
  CustomerName VARCHAR, Biller VARCHAR, SaleDate DATETIME,
  GrandTotal DECIMAL, Paid DECIMAL, Due DECIMAL,
  OrderTax DECIMAL, Discount DECIMAL, Shipping DECIMAL,
  Status VARCHAR (Pending|Completed), PaymentStatus VARCHAR (Paid|Unpaid|Overdue|Partial),
  Source VARCHAR, Notes TEXT, CreatedAt DATETIME

TABLE: SaleItems
  Id INT PK, SaleId INT FK->Sales (CASCADE), ProductId INT,
  ProductName VARCHAR, Quantity DECIMAL, PurchasePrice DECIMAL,
  Discount DECIMAL, TaxPercent DECIMAL, TaxAmount DECIMAL,
  UnitCost DECIMAL, TotalCost DECIMAL

TABLE: SalePayments
  Id INT PK, SaleId INT FK->Sales (CASCADE), Reference VARCHAR,
  ReceivedAmount DECIMAL, PayingAmount DECIMAL,
  PaymentType VARCHAR, Description VARCHAR, PaymentDate DATETIME

TABLE: Purchases
  Id INT PK, TenantId INT, Reference VARCHAR, SupplierId INT,
  SupplierName VARCHAR, Date DATETIME, Total DECIMAL, Paid DECIMAL,
  Status VARCHAR (Pending|Received), PaymentStatus VARCHAR,
  Notes TEXT, CreatedAt DATETIME

TABLE: PurchaseItems
  Id INT PK, PurchaseId INT FK->Purchases, ProductId INT,
  ProductName VARCHAR, Quantity DECIMAL, TotalCost DECIMAL

TABLE: Invoices
  Id INT PK, TenantId INT, InvoiceNo VARCHAR, CustomerName VARCHAR,
  DueDate DATETIME, TotalAmount DECIMAL, Paid DECIMAL, AmountDue DECIMAL,
  Status VARCHAR, CreatedAt DATETIME

TABLE: Expenses
  Id INT PK, TenantId INT, Reference VARCHAR, ExpenseName VARCHAR,
  ExpenseCategoryId INT FK->ExpenseCategories, Description TEXT,
  Date DATETIME, Amount DECIMAL, Status VARCHAR, CreatedOn DATETIME

TABLE: ExpenseCategories
  Id INT PK, TenantId INT, Name VARCHAR, Description VARCHAR, Status VARCHAR

TABLE: Categories
  Id INT PK, TenantId INT, Name VARCHAR, Slug VARCHAR, Description VARCHAR,
  Image VARCHAR, Status VARCHAR

=== KEY RELATIONSHIPS ===
- Sales.CustomerId -> Parties.Id
- Purchases.SupplierId -> Parties.Id

=== IMPORTANT RULES ===
1. ALWAYS include WHERE TenantId = @TenantId in every query
2. Use LEFT JOIN when data might not exist
3. For today's date use CURDATE() (MySQL)
4. String dates are stored as 'yyyy-MM-dd' format in varchar columns
5. Only generate SELECT queries - never INSERT/UPDATE/DELETE
6. Limit results to prevent huge responses (use LIMIT)
";
}
