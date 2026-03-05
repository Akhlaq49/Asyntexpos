namespace ReactPosApi.Services;

/// <summary>
/// Provides the database schema description for the LLM agent to understand table structures.
/// </summary>
public static class AgentDbSchema
{
    public static string GetSchemaDescription() => @"
=== DATABASE SCHEMA (MySQL) ===
All tables have a TenantId column for multi-tenancy. Always filter by TenantId = @TenantId.

TABLE: Parties (Unified people table - customers, employees, guarantors, suppliers, admins)
  Id INT PK, TenantId INT, FullName VARCHAR, LastName VARCHAR, Email VARCHAR, Phone VARCHAR,
  PhoneWork VARCHAR, SO VARCHAR (Son/Daughter of), Cnic VARCHAR,
  Address VARCHAR, City VARCHAR, State VARCHAR, Country VARCHAR, ZipCode VARCHAR,
  Role VARCHAR (Admin|Manager|User|Customer|Guarantor|Supplier|Biller|Store|Warehouse|Employee),
  Status VARCHAR (active|inactive), IsActive BIT,
  Picture VARCHAR, Code VARCHAR, CompanyName VARCHAR, ContactPerson VARCHAR,
  DepartmentId INT FK->Departments, DesignationId INT FK->Designations, ShiftId INT FK->Shifts,
  DateOfJoining DATETIME, BasicSalary DECIMAL, EmployeeId VARCHAR,
  PasswordHash VARCHAR, UserName VARCHAR, CreatedAt DATETIME

TABLE: Products
  Id INT PK, TenantId INT, Store VARCHAR, Warehouse VARCHAR, ProductName VARCHAR, Slug VARCHAR,
  SKU VARCHAR, Category VARCHAR, SubCategory VARCHAR, Brand VARCHAR, Unit VARCHAR,
  ItemBarcode VARCHAR, Description TEXT, ProductType VARCHAR (single|variable),
  Quantity INT, Price DECIMAL, QuantityAlert INT, Warranty VARCHAR,
  Tax DECIMAL, DiscountType VARCHAR, DiscountValue DECIMAL,
  ExpiryDate VARCHAR, ManufacturedDate VARCHAR, CreatedAt DATETIME

TABLE: ProductImages
  Id INT PK, ProductId INT FK->Products, ImagePath VARCHAR

TABLE: InstallmentPlans
  Id INT PK, TenantId INT, CustomerId INT FK->Parties, ProductId INT FK->Products,
  ProductPrice DECIMAL, FinanceAmount DECIMAL, DownPayment DECIMAL,
  FinancedAmount DECIMAL, InterestRate DECIMAL (annual %), Tenure INT (months),
  EmiAmount DECIMAL, TotalPayable DECIMAL, TotalInterest DECIMAL,
  StartDate VARCHAR, Status VARCHAR (active|completed|cancelled|defaulted),
  PaidInstallments INT, RemainingInstallments INT, NextDueDate VARCHAR,
  CreatedAt DATETIME

TABLE: RepaymentEntries (Installment schedule)
  Id INT PK, TenantId INT, PlanId INT FK->InstallmentPlans (CASCADE),
  InstallmentNo INT, DueDate VARCHAR, EmiAmount DECIMAL, Principal DECIMAL,
  Interest DECIMAL, Balance DECIMAL,
  Status VARCHAR (upcoming|paid|due|overdue|partial),
  PaidDate VARCHAR, ActualPaidAmount DECIMAL, MiscAdjustedAmount DECIMAL

TABLE: PlanGuarantors (Links guarantors to installment plans)
  Id INT PK, TenantId INT, PlanId INT FK->InstallmentPlans (CASCADE),
  PartyId INT FK->Parties, Relationship VARCHAR

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

TABLE: FinanceIncomes
  Id INT PK, TenantId INT, Reference VARCHAR, IncomeName VARCHAR,
  IncomeCategoryId INT FK->IncomeCategories, Description TEXT,
  Date DATETIME, Amount DECIMAL, Status VARCHAR

TABLE: IncomeCategories
  Id INT PK, TenantId INT, Name VARCHAR, Description VARCHAR, Status VARCHAR

TABLE: BankAccounts
  Id INT PK, TenantId INT, AccountTitle VARCHAR, BankName VARCHAR,
  AccountNumber VARCHAR, Branch VARCHAR, Balance DECIMAL,
  AccountTypeId INT FK->AccountTypes, Status VARCHAR

TABLE: Categories
  Id INT PK, TenantId INT, Name VARCHAR, Slug VARCHAR, Description VARCHAR,
  Image VARCHAR, Status VARCHAR

TABLE: SubCategories
  Id INT PK, TenantId INT, Name VARCHAR, Slug VARCHAR, CategoryId INT FK->Categories

TABLE: MiscellaneousRegisters
  Id INT PK, TenantId INT, CustomerId INT FK->Parties, PlanId INT FK->InstallmentPlans,
  Type VARCHAR, Amount DECIMAL, Description TEXT, CreatedAt DATETIME

TABLE: Departments
  Id INT PK, TenantId INT, Name VARCHAR, HODId INT FK->Parties, Status VARCHAR

TABLE: Designations
  Id INT PK, TenantId INT, Name VARCHAR, DepartmentId INT FK->Departments

TABLE: Attendances
  Id INT PK, TenantId INT, EmployeeId INT FK->Parties, Date DATETIME,
  Status VARCHAR, ClockIn TIME, ClockOut TIME, Production VARCHAR,
  BreakTime VARCHAR, Overtime VARCHAR, TotalHours VARCHAR

TABLE: Payrolls
  Id INT PK, TenantId INT, EmployeeId INT FK->Parties, Month VARCHAR,
  BasicSalary DECIMAL, Allowances DECIMAL, Deductions DECIMAL,
  NetSalary DECIMAL, Status VARCHAR, PaidOn DATETIME

TABLE: Leaves
  Id INT PK, TenantId INT, EmployeeId INT FK->Parties,
  LeaveTypeId INT FK->LeaveTypes, StartDate DATETIME, EndDate DATETIME,
  Reason VARCHAR, Status VARCHAR

TABLE: Holidays
  Id INT PK, TenantId INT, Name VARCHAR, Date DATETIME, Description VARCHAR

TABLE: Shifts
  Id INT PK, TenantId INT, Name VARCHAR, StartTime VARCHAR, EndTime VARCHAR, Status VARCHAR

=== KEY RELATIONSHIPS ===
- Parties.Id -> InstallmentPlans.CustomerId (Customer)
- Parties.Id -> PlanGuarantors.PartyId (Guarantor, Role='Guarantor' or 'Customer')
- InstallmentPlans.Id -> RepaymentEntries.PlanId
- InstallmentPlans.Id -> PlanGuarantors.PlanId
- InstallmentPlans.ProductId -> Products.Id
- Sales.CustomerId -> Parties.Id
- Purchases.SupplierId -> Parties.Id

=== IMPORTANT RULES ===
1. ALWAYS include WHERE TenantId = @TenantId in every query
2. Use LEFT JOIN when data might not exist (e.g. guarantors)
3. For today's date use CURDATE() (MySQL)
4. String dates are stored as 'yyyy-MM-dd' format in varchar columns
5. Only generate SELECT queries - never INSERT/UPDATE/DELETE
6. Limit results to prevent huge responses (use LIMIT)
7. PAID AMOUNT RULE for RepaymentEntries: When calculating the paid amount of an installment entry:
   - If Status = 'paid' then the paid amount is EmiAmount
   - If Status = 'partial' then the paid amount is (COALESCE(ActualPaidAmount,0) + COALESCE(MiscAdjustedAmount,0))
   Use this SQL pattern: CASE WHEN re.Status='paid' THEN re.EmiAmount WHEN re.Status='partial' THEN COALESCE(re.ActualPaidAmount,0)+COALESCE(re.MiscAdjustedAmount,0) ELSE 0 END AS PaidAmount
";
}
