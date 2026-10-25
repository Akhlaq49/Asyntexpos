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
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<VariantAttribute> VariantAttributes => Set<VariantAttribute>();
    public DbSet<Warranty> Warranties => Set<Warranty>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<TermsConditions> TermsConditions => Set<TermsConditions>();
    public DbSet<TaxType> TaxTypes => Set<TaxType>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
    public DbSet<PurchaseReturnRefund> PurchaseReturnRefunds => Set<PurchaseReturnRefund>();
    public DbSet<FinanceEntry> FinanceEntries => Set<FinanceEntry>();

    // Inventory
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    // Parties (unified: Admin, Manager, User, Customer, Supplier)
    public DbSet<Party> Parties => Set<Party>();

    // Customer addresses
    public DbSet<PartyAddress> PartyAddresses => Set<PartyAddress>();

    // Role Permissions
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Form Field Configurations
    public DbSet<FormFieldConfig> FormFieldConfigs => Set<FormFieldConfig>();

    // Tenant Menu Configuration (hidden menus per tenant)
    public DbSet<TenantMenuConfig> TenantMenuConfigs => Set<TenantMenuConfig>();

    // Finance & Accounting
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();

    // SMS / Notifications
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<SmsWhitelistedNumber> SmsWhitelistedNumbers => Set<SmsWhitelistedNumber>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique();
            e.Property(c => c.Status).HasDefaultValue("active");
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

        // PurchaseOrder -> items (receipts become Purchases with PurchaseOrderId)
        // one terms & conditions document per tenant
        modelBuilder.Entity<TermsConditions>(e => e.HasIndex(t => t.TenantId).IsUnique());
        modelBuilder.Entity<TaxType>(e => e.HasIndex(t => new { t.TenantId, t.Name }).IsUnique());
        modelBuilder.Entity<PurchasePayment>(e => { e.HasIndex(p => p.PurchaseId); e.HasIndex(p => p.PurchaseOrderId); });
        // goods sent back to the supplier from a received purchase
        modelBuilder.Entity<PurchaseReturn>(e =>
        {
            e.HasIndex(r => new { r.TenantId, r.Reference }).IsUnique();
            e.HasIndex(r => r.PurchaseId);
            e.HasMany(r => r.Items).WithOne(i => i.PurchaseReturn).HasForeignKey(i => i.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Refunds).WithOne(i => i.PurchaseReturn).HasForeignKey(i => i.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.HasIndex(po => new { po.TenantId, po.PoNumber }).IsUnique();
            e.Property(po => po.Status).HasDefaultValue(PurchaseOrderStatus.Draft);
        });
        modelBuilder.Entity<PurchaseOrderItem>(e =>
        {
            e.HasOne(i => i.PurchaseOrder).WithMany(po => po.Items).HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Purchase>(e => e.HasIndex(p => p.PurchaseOrderId));

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
