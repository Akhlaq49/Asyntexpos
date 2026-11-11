using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Data;

public class AppDbContext : DbContext
{
    private readonly int _tenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantId = tenantProvider.TenantId;
    }

    /// <summary>
    /// Exposed for EF Core query filter parameterization.
    /// EF Core re-evaluates this property per query from the current DbContext instance.
    /// </summary>
    public int CurrentTenantId => _tenantId;

    // Tenants (not tenant-scoped — global table)
    public DbSet<Tenant> Tenants => Set<Tenant>();

    // Lookup tables
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<VariantAttribute> VariantAttributes => Set<VariantAttribute>();
    public DbSet<Warranty> Warranties => Set<Warranty>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    // Inventory
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    // Parties (unified: Admin, Manager, User, Customer, Guarantor)
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PlanGuarantor> PlanGuarantors => Set<PlanGuarantor>();
    public DbSet<MiscellaneousRegister> MiscellaneousRegisters => Set<MiscellaneousRegister>();

    // Installments
    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<RepaymentEntry> RepaymentEntries => Set<RepaymentEntry>();
    public DbSet<PlanMedia> PlanMedia => Set<PlanMedia>();

    // Online storefront
    public DbSet<PartyAddress> PartyAddresses => Set<PartyAddress>();

    // Role Permissions
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Form Field Configurations
    public DbSet<FormFieldConfig> FormFieldConfigs => Set<FormFieldConfig>();

    // Tenant Menu Configuration (hidden menus per tenant)
    public DbSet<TenantMenuConfig> TenantMenuConfigs => Set<TenantMenuConfig>();

    // HRM
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    // Finance & Accounting
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<IncomeCategory> IncomeCategories => Set<IncomeCategory>();
    public DbSet<FinanceIncome> FinanceIncomes => Set<FinanceIncome>();
    public DbSet<AccountType> AccountTypes => Set<AccountType>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    // SMS / Notifications
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<SmsWhitelistedNumber> SmsWhitelistedNumbers => Set<SmsWhitelistedNumber>();

    // Web Content (dynamic storefront sections)
    public DbSet<WebContent> WebContents => Set<WebContent>();

    // Pharmacy
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<SaleItemBatch> SaleItemBatches => Set<SaleItemBatch>();
    public DbSet<HeldBill> HeldBills => Set<HeldBill>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<TillSession> TillSessions => Set<TillSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefillReminder> RefillReminders => Set<RefillReminder>();
    public DbSet<PharmacySettings> PharmacySettings => Set<PharmacySettings>();

    // ── Locations, ledger, credit, catalogue, channels, variants, manufacturing, restaurant ──
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<LocationStockLevel> LocationStockLevels => Set<LocationStockLevel>();
    public DbSet<StockRequest> StockRequests => Set<StockRequest>();
    public DbSet<StockRequestItem> StockRequestItems => Set<StockRequestItem>();
    public DbSet<PriceLevel> PriceLevels => Set<PriceLevel>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<CustomerReceipt> CustomerReceipts => Set<CustomerReceipt>();
    public DbSet<CustomerLedgerEntry> CustomerLedgerEntries => Set<CustomerLedgerEntry>();
    public DbSet<MedicineMaster> MedicineMasters => Set<MedicineMaster>();
    public DbSet<DrugInteraction> DrugInteractions => Set<DrugInteraction>();
    public DbSet<ProductTranslation> ProductTranslations => Set<ProductTranslation>();
    public DbSet<ChannelConnection> ChannelConnections => Set<ChannelConnection>();
    public DbSet<ChannelProductLink> ChannelProductLinks => Set<ChannelProductLink>();
    public DbSet<ChannelOrder> ChannelOrders => Set<ChannelOrder>();
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
    public DbSet<VariantAttributeValue> VariantAttributeValues => Set<VariantAttributeValue>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantValue> ProductVariantValues => Set<ProductVariantValue>();
    public DbSet<BillOfMaterials> BillsOfMaterials => Set<BillOfMaterials>();
    public DbSet<BomItem> BomItems => Set<BomItem>();
    public DbSet<ManufacturingOrder> ManufacturingOrders => Set<ManufacturingOrder>();
    public DbSet<ManufacturingOrderItem> ManufacturingOrderItems => Set<ManufacturingOrderItem>();
    public DbSet<MoItemBatch> MoItemBatches => Set<MoItemBatch>();
    public DbSet<WorkCentre> WorkCentres => Set<WorkCentre>();
    public DbSet<BomOperation> BomOperations => Set<BomOperation>();
    public DbSet<MoOperation> MoOperations => Set<MoOperation>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<Modifier> Modifiers => Set<Modifier>();
    public DbSet<ProductModifierGroup> ProductModifierGroups => Set<ProductModifierGroup>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<KitchenStation> KitchenStations => Set<KitchenStation>();
    public DbSet<KitchenTicket> KitchenTickets => Set<KitchenTicket>();
    public DbSet<KitchenTicketItem> KitchenTicketItems => Set<KitchenTicketItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique();
            e.Property(c => c.Status).HasDefaultValue("active");
        });

        // SubCategory -> Category
        modelBuilder.Entity<SubCategory>(e =>
        {
            e.HasOne(sc => sc.Category)
             .WithMany(c => c.SubCategories)
             .HasForeignKey(sc => sc.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
            e.Property(sc => sc.Status).HasDefaultValue("active");
        });

        // Product
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasColumnType("decimal(18,2)");
            e.Property(p => p.DiscountValue).HasColumnType("decimal(18,2)");
            e.Property(p => p.ProductType).HasDefaultValue("single");
        });

        // ProductImage -> Product
        modelBuilder.Entity<ProductImage>(e =>
        {
            e.HasOne(pi => pi.Product)
             .WithMany(p => p.Images)
             .HasForeignKey(pi => pi.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Party (unified: Admin, Manager, User, Customer, Guarantor, Supplier, Biller, Store, Warehouse, Employee)
        modelBuilder.Entity<Party>(e =>
        {
            e.HasIndex(p => new { p.TenantId, p.Email, p.Role })
             .IsUnique()
             .HasDatabaseName("IX_Parties_Tenant_Email_Role");
            e.Property(p => p.Role).HasDefaultValue("Customer");
            e.Property(p => p.Status).HasDefaultValue("active");
            e.Property(p => p.IsActive).HasDefaultValue(true);
            e.Property(p => p.BasicSalary).HasColumnType("decimal(18,2)");

            e.HasOne(p => p.Department).WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Designation).WithMany().HasForeignKey(p => p.DesignationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Shift).WithMany().HasForeignKey(p => p.ShiftId).OnDelete(DeleteBehavior.SetNull);
        });

        // Department
        modelBuilder.Entity<Department>(e =>
        {
            e.HasOne(d => d.HOD).WithMany().HasForeignKey(d => d.HODId).OnDelete(DeleteBehavior.SetNull);
            e.Property(d => d.Status).HasDefaultValue("active");
            e.Property(d => d.IsActive).HasDefaultValue(true);
        });

        // Designation -> Department
        modelBuilder.Entity<Designation>(e =>
        {
            e.HasOne(d => d.Department).WithMany(dept => dept.Designations).HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.Property(d => d.Status).HasDefaultValue("active");
            e.Property(d => d.IsActive).HasDefaultValue(true);
        });

        // Shift
        modelBuilder.Entity<Shift>(e =>
        {
            e.Property(s => s.Status).HasDefaultValue("active");
            e.Property(s => s.IsActive).HasDefaultValue(true);
        });

        // LeaveType
        modelBuilder.Entity<LeaveType>(e =>
        {
            e.Property(lt => lt.Status).HasDefaultValue("active");
            e.Property(lt => lt.IsActive).HasDefaultValue(true);
        });

        // Leave
        modelBuilder.Entity<Leave>(e =>
        {
            e.HasOne(l => l.Employee).WithMany().HasForeignKey(l => l.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.LeaveType).WithMany(lt => lt.Leaves).HasForeignKey(l => l.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.ApprovedBy).WithMany().HasForeignKey(l => l.ApprovedById).OnDelete(DeleteBehavior.SetNull);
            e.Property(l => l.Days).HasColumnType("decimal(5,1)");
        });

        // Holiday
        modelBuilder.Entity<Holiday>(e =>
        {
            e.Property(h => h.Status).HasDefaultValue("active");
            e.Property(h => h.IsActive).HasDefaultValue(true);
        });

        // Payroll
        modelBuilder.Entity<Payroll>(e =>
        {
            e.HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        // Attendance
        modelBuilder.Entity<Attendance>(e =>
        {
            e.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.TenantId, a.EmployeeId, a.Date }).IsUnique();
        });

        // InstallmentPlan
        modelBuilder.Entity<InstallmentPlan>(e =>
        {
            e.HasOne(ip => ip.Customer)
             .WithMany()
             .HasForeignKey(ip => ip.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ip => ip.Product)
             .WithMany()
             .HasForeignKey(ip => ip.ProductId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(p => p.ProductPrice).HasColumnType("decimal(18,2)");
            e.Property(p => p.FinanceAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.DownPayment).HasColumnType("decimal(18,2)");
            e.Property(p => p.FinancedAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.InterestRate).HasColumnType("decimal(8,4)");
            e.Property(p => p.EmiAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.TotalPayable).HasColumnType("decimal(18,2)");
            e.Property(p => p.TotalInterest).HasColumnType("decimal(18,2)");
            e.Property(p => p.Status).HasDefaultValue("active");
        });

        // RepaymentEntry -> InstallmentPlan
        modelBuilder.Entity<RepaymentEntry>(e =>
        {
            e.HasOne(r => r.Plan)
             .WithMany(p => p.Schedule)
             .HasForeignKey(r => r.PlanId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.EmiAmount).HasColumnType("decimal(18,2)");
            e.Property(r => r.Principal).HasColumnType("decimal(18,2)");
            e.Property(r => r.Interest).HasColumnType("decimal(18,2)");
            e.Property(r => r.Balance).HasColumnType("decimal(18,2)");
            e.Property(r => r.Status).HasDefaultValue("upcoming");
        });

        // PlanGuarantor -> InstallmentPlan + Party
        modelBuilder.Entity<PlanGuarantor>(e =>
        {
            e.HasOne(pg => pg.Plan)
             .WithMany(p => p.PlanGuarantors)
             .HasForeignKey(pg => pg.PlanId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pg => pg.Party)
             .WithMany(p => p.GuaranteedPlans)
             .HasForeignKey(pg => pg.PartyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // PlanMedia -> InstallmentPlan
        modelBuilder.Entity<PlanMedia>(e =>
        {
            e.HasOne(pm => pm.Plan)
             .WithMany(p => p.Media)
             .HasForeignKey(pm => pm.PlanId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // StockTransferItem -> StockTransfer
        modelBuilder.Entity<StockTransferItem>(e =>
        {
            e.HasOne(i => i.StockTransfer)
             .WithMany(t => t.Items)
             .HasForeignKey(i => i.StockTransferId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // PartyAddress -> Party
        modelBuilder.Entity<PartyAddress>(e =>
        {
            e.HasOne(a => a.Party)
             .WithMany(p => p.Addresses)
             .HasForeignKey(a => a.PartyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Sale
        modelBuilder.Entity<Sale>(e =>
        {
            e.HasOne(s => s.Customer)
             .WithMany()
             .HasForeignKey(s => s.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.BillingAddress)
             .WithMany()
             .HasForeignKey(s => s.BillingAddressId)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(s => s.ShippingAddress)
             .WithMany()
             .HasForeignKey(s => s.ShippingAddressId)
             .OnDelete(DeleteBehavior.NoAction);
            e.Property(s => s.GrandTotal).HasColumnType("decimal(18,2)");
            e.Property(s => s.Paid).HasColumnType("decimal(18,2)");
            e.Property(s => s.Due).HasColumnType("decimal(18,2)");
            e.Property(s => s.OrderTax).HasColumnType("decimal(18,2)");
            e.Property(s => s.Discount).HasColumnType("decimal(18,2)");
            e.Property(s => s.Shipping).HasColumnType("decimal(18,2)");
        });

        // SaleItem -> Sale
        modelBuilder.Entity<SaleItem>(e =>
        {
            e.HasOne(i => i.Sale)
             .WithMany(s => s.Items)
             .HasForeignKey(i => i.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(i => i.PurchasePrice).HasColumnType("decimal(18,2)");
            e.Property(i => i.Discount).HasColumnType("decimal(18,2)");
            e.Property(i => i.TaxPercent).HasColumnType("decimal(8,4)");
            e.Property(i => i.TaxAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.UnitCost).HasColumnType("decimal(18,2)");
            e.Property(i => i.TotalCost).HasColumnType("decimal(18,2)");
        });

        // SalePayment -> Sale
        modelBuilder.Entity<SalePayment>(e =>
        {
            e.HasOne(p => p.Sale)
             .WithMany(s => s.Payments)
             .HasForeignKey(p => p.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.ReceivedAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.PayingAmount).HasColumnType("decimal(18,2)");
        });

        // Invoice
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasOne(i => i.Customer)
             .WithMany()
             .HasForeignKey(i => i.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
            e.Property(i => i.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(i => i.Discount).HasColumnType("decimal(18,2)");
            e.Property(i => i.DiscountPercent).HasColumnType("decimal(8,4)");
            e.Property(i => i.Tax).HasColumnType("decimal(18,2)");
            e.Property(i => i.TaxPercent).HasColumnType("decimal(8,4)");
            e.Property(i => i.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.Paid).HasColumnType("decimal(18,2)");
            e.Property(i => i.AmountDue).HasColumnType("decimal(18,2)");
        });

        // InvoiceItem -> Invoice
        modelBuilder.Entity<InvoiceItem>(e =>
        {
            e.HasOne(i => i.Invoice)
             .WithMany(inv => inv.Items)
             .HasForeignKey(i => i.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(i => i.Cost).HasColumnType("decimal(18,2)");
            e.Property(i => i.Discount).HasColumnType("decimal(18,2)");
            e.Property(i => i.Total).HasColumnType("decimal(18,2)");
        });

        // SalesReturn
        modelBuilder.Entity<SalesReturn>(e =>
        {
            e.HasOne(r => r.Customer)
             .WithMany()
             .HasForeignKey(r => r.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
            e.Property(r => r.OrderTax).HasColumnType("decimal(18,2)");
            e.Property(r => r.Discount).HasColumnType("decimal(18,2)");
            e.Property(r => r.Shipping).HasColumnType("decimal(18,2)");
            e.Property(r => r.GrandTotal).HasColumnType("decimal(18,2)");
            e.Property(r => r.Paid).HasColumnType("decimal(18,2)");
            e.Property(r => r.Due).HasColumnType("decimal(18,2)");
        });

        // SalesReturnItem -> SalesReturn
        modelBuilder.Entity<SalesReturnItem>(e =>
        {
            e.HasOne(i => i.SalesReturn)
             .WithMany(r => r.Items)
             .HasForeignKey(i => i.SalesReturnId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(i => i.NetUnitPrice).HasColumnType("decimal(18,2)");
            e.Property(i => i.Discount).HasColumnType("decimal(18,2)");
            e.Property(i => i.TaxPercent).HasColumnType("decimal(8,4)");
            e.Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
        });

        // Quotation
        modelBuilder.Entity<Quotation>(e =>
        {
            e.HasOne(q => q.Customer)
             .WithMany()
             .HasForeignKey(q => q.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
            e.Property(q => q.OrderTax).HasColumnType("decimal(18,2)");
            e.Property(q => q.Discount).HasColumnType("decimal(18,2)");
            e.Property(q => q.Shipping).HasColumnType("decimal(18,2)");
            e.Property(q => q.GrandTotal).HasColumnType("decimal(18,2)");
        });

        // QuotationItem -> Quotation
        modelBuilder.Entity<QuotationItem>(e =>
        {
            e.HasOne(i => i.Quotation)
             .WithMany(q => q.Items)
             .HasForeignKey(i => i.QuotationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(i => i.PurchasePrice).HasColumnType("decimal(18,2)");
            e.Property(i => i.Discount).HasColumnType("decimal(18,2)");
            e.Property(i => i.TaxPercent).HasColumnType("decimal(8,4)");
            e.Property(i => i.TaxAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.UnitCost).HasColumnType("decimal(18,2)");
            e.Property(i => i.TotalCost).HasColumnType("decimal(18,2)");
        });

        // PurchaseItem -> Purchase & Product (FK for inventory sync)
        modelBuilder.Entity<PurchaseItem>(e =>
        {
            e.HasOne(pi => pi.Purchase)
             .WithMany(p => p.Items)
             .HasForeignKey(pi => pi.PurchaseId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pi => pi.Product)
             .WithMany()
             .HasForeignKey(pi => pi.ProductId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // Coupon
        modelBuilder.Entity<Coupon>(e =>
        {
            e.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
            e.Property(c => c.Discount).HasColumnType("decimal(18,2)");
        });

        // RolePermission
        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasIndex(rp => new { rp.TenantId, rp.Role, rp.MenuKey }).IsUnique();
        });

        // FormFieldConfig
        modelBuilder.Entity<FormFieldConfig>(e =>
        {
            e.HasIndex(f => new { f.TenantId, f.FormName, f.FieldName }).IsUnique();
            e.Property(f => f.IsVisible).HasDefaultValue(true);
        });

        // TenantMenuConfig
        modelBuilder.Entity<TenantMenuConfig>(e =>
        {
            e.HasIndex(c => new { c.TenantId, c.MenuKey }).IsUnique();
        });

        // Finance & Accounting
        modelBuilder.Entity<Expense>(e =>
        {
            e.HasOne(x => x.ExpenseCategory).WithMany(c => c.Expenses).HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<FinanceIncome>(e =>
        {
            e.HasOne(x => x.IncomeCategory).WithMany(c => c.Incomes).HasForeignKey(x => x.IncomeCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<BankAccount>(e =>
        {
            e.HasOne(x => x.AccountType).WithMany(t => t.BankAccounts).HasForeignKey(x => x.AccountTypeId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OpeningBalance).HasColumnType("decimal(18,2)");
        });

        // WebContent
        modelBuilder.Entity<WebContent>(e =>
        {
            e.HasIndex(w => new { w.TenantId, w.Section, w.SortOrder });
            e.Property(w => w.Status).HasDefaultValue("active");
        });

        // ── Pharmacy ──
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.PurchasePrice).HasColumnType("decimal(18,4)");
            e.Property(p => p.UnitsPerStrip).HasDefaultValue(1);
            e.Property(p => p.StripsPerBox).HasDefaultValue(1);
            e.HasIndex(p => new { p.TenantId, p.GenericName });
        });
        modelBuilder.Entity<SaleItem>(e =>
        {
            e.Property(i => i.CostPrice).HasColumnType("decimal(18,4)");
            e.Property(i => i.SaleUnit).HasDefaultValue("Unit");
        });
        modelBuilder.Entity<SalesReturnItem>(e => e.Property(i => i.Condition).HasDefaultValue("Saleable"));
        modelBuilder.Entity<ProductBatch>(e =>
        {
            e.HasOne(b => b.Product).WithMany().HasForeignKey(b => b.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(b => new { b.TenantId, b.ProductId, b.ExpiryDate });
            e.HasIndex(b => new { b.TenantId, b.ExpiryDate });
        });
        modelBuilder.Entity<SaleItemBatch>(e => e.HasIndex(x => new { x.TenantId, x.SaleItemId }));
        modelBuilder.Entity<PurchaseReturnItem>(e =>
            e.HasOne(i => i.PurchaseReturn).WithMany(r => r.Items).HasForeignKey(i => i.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<AuditLog>(e => e.HasIndex(a => new { a.TenantId, a.CreatedAt }));
        modelBuilder.Entity<RefillReminder>(e => e.HasIndex(r => new { r.TenantId, r.SaleId, r.ProductId }).IsUnique());
        modelBuilder.Entity<PharmacySettings>(e => e.HasIndex(x => x.TenantId).IsUnique());

        // ── Locations and stock ledger ──
        modelBuilder.Entity<Location>(e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        modelBuilder.Entity<ProductBatch>(e => e.HasIndex(b => new { b.TenantId, b.LocationId, b.ProductId }));
        modelBuilder.Entity<StockMovement>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId });
            e.HasIndex(x => new { x.TenantId, x.LocationId, x.CreatedAt });
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<LocationStockLevel>(e => e.HasIndex(x => new { x.TenantId, x.ProductId, x.LocationId }).IsUnique());
        modelBuilder.Entity<StockRequestItem>(e =>
            e.HasOne(i => i.StockRequest).WithMany(r => r.Items).HasForeignKey(i => i.StockRequestId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<StockTransfer>(e => e.Property(t => t.Status).HasDefaultValue("Received"));
        modelBuilder.Entity<StockTransferItem>(e =>
            e.HasOne(i => i.ReceivedBatch).WithMany().HasForeignKey(i => i.ReceivedBatchId).OnDelete(DeleteBehavior.SetNull));

        // ── Wholesale and credit ──
        modelBuilder.Entity<ProductPrice>(e => e.HasIndex(x => new { x.TenantId, x.ProductId, x.VariantId, x.PriceLevelId, x.SaleUnit, x.MinQty }).IsUnique());
        modelBuilder.Entity<CustomerReceipt>(e => e.HasIndex(x => new { x.TenantId, x.CustomerId }));
        modelBuilder.Entity<CustomerLedgerEntry>(e => e.HasIndex(x => new { x.TenantId, x.CustomerId, x.EntryDate }));
        modelBuilder.Entity<SalePayment>(e => e.HasIndex(x => x.CustomerReceiptId));
        modelBuilder.Entity<Sale>(e =>
        {
            e.Property(x => x.Channel).HasDefaultValue("POS");
            e.HasIndex(x => new { x.TenantId, x.CustomerId });
            e.HasIndex(x => new { x.TenantId, x.LocationId, x.SaleDate });
        });
        modelBuilder.Entity<Party>(e => e.Property(x => x.Language).HasDefaultValue("en"));

        // ── Catalogue and translations ──
        modelBuilder.Entity<MedicineMaster>(e =>
        {
            e.HasIndex(x => x.BrandName);
            e.HasIndex(x => x.GenericName);
            e.HasIndex(x => x.Barcode);
        });
        modelBuilder.Entity<DrugInteraction>(e => e.HasIndex(x => new { x.GenericA, x.GenericB }));
        modelBuilder.Entity<ProductTranslation>(e => e.HasIndex(x => new { x.TenantId, x.ProductId, x.Lang }).IsUnique());
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.SellOnline).HasDefaultValue(true);
            e.HasIndex(p => new { p.TenantId, p.ArticleNo });
        });

        // ── Channels ──
        modelBuilder.Entity<ChannelProductLink>(e => e.HasIndex(x => new { x.TenantId, x.ConnectionId, x.ProductId, x.VariantId }).IsUnique());
        modelBuilder.Entity<ChannelOrder>(e => e.HasIndex(x => new { x.TenantId, x.ConnectionId, x.ExternalOrderId }).IsUnique());
        modelBuilder.Entity<SyncOutbox>(e => e.HasIndex(x => new { x.Status, x.CreatedAt }));

        // ── Variants ──
        modelBuilder.Entity<VariantAttributeValue>(e => e.HasIndex(x => new { x.TenantId, x.VariantAttributeId, x.Value }).IsUnique());
        modelBuilder.Entity<ProductVariant>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Barcode });
            e.HasIndex(x => new { x.TenantId, x.ProductId });
        });
        modelBuilder.Entity<ProductVariantValue>(e => e.HasIndex(x => new { x.VariantId, x.AttributeValueId }).IsUnique());

        // ── Manufacturing (tables created by the manufacturing branch) ──
        modelBuilder.Entity<BomItem>(e =>
            e.HasOne(i => i.Bom).WithMany(b => b.Items).HasForeignKey(i => i.BomId).HasConstraintName("FK_BomItem_Bom").OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<ManufacturingOrder>(e =>
            e.HasOne(o => o.Bom).WithMany().HasForeignKey(o => o.BomId).HasConstraintName("FK_MO_BOM").OnDelete(DeleteBehavior.Restrict));
        modelBuilder.Entity<ManufacturingOrderItem>(e =>
            e.HasOne(i => i.ManufacturingOrder).WithMany(o => o.Items).HasForeignKey(i => i.ManufacturingOrderId).HasConstraintName("FK_MOItem_MO").OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<BomOperation>(e =>
            e.HasOne(o => o.Bom).WithMany(b => b.Operations).HasForeignKey(o => o.BomId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<MoOperation>(e =>
            e.HasOne(o => o.Order).WithMany(m => m.Operations).HasForeignKey(o => o.OrderId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<MoItemBatch>(e => e.HasIndex(x => x.MoItemId));

        // ── Restaurant ──
        modelBuilder.Entity<RecipeItem>(e =>
            e.HasOne(i => i.Recipe).WithMany(r => r.Items).HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<Recipe>(e => e.HasIndex(x => new { x.TenantId, x.ProductId }).IsUnique());
        modelBuilder.Entity<Modifier>(e =>
            e.HasOne(m => m.ModifierGroup).WithMany(g => g.Modifiers).HasForeignKey(m => m.ModifierGroupId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<ProductModifierGroup>(e => e.HasIndex(x => new { x.TenantId, x.ProductId, x.ModifierGroupId }).IsUnique());
        modelBuilder.Entity<KitchenTicketItem>(e =>
            e.HasOne(i => i.Ticket).WithMany(t => t.Items).HasForeignKey(i => i.TicketId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<KitchenTicket>(e => e.HasIndex(x => new { x.TenantId, x.StationId, x.Status }));

        // ═══════════════════════════════════════════════════════════
        // MULTI-TENANCY: Apply global query filters dynamically
        // ═══════════════════════════════════════════════════════════
        // For every entity implementing ITenantEntity, add a query filter:
        //   e => e.TenantId == this.CurrentTenantId
        // EF Core parameterizes the DbContext property reference, so the
        // filter evaluates CurrentTenantId from the CURRENT DbContext instance
        // on every query, even though the model is cached.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var currentTenantId = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentTenantId)
            );
            var filter = Expression.Lambda(
                Expression.Equal(tenantIdProperty, currentTenantId),
                parameter
            );
            entityType.SetQueryFilter(filter);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MULTI-TENANCY: Auto-set TenantId on new entities
    // ═══════════════════════════════════════════════════════════
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetTenantIdOnNewEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        SetTenantIdOnNewEntities();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void SetTenantIdOnNewEntities()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == 0 && _tenantId > 0)
            {
                entry.Entity.TenantId = _tenantId;
            }
        }
    }
}
