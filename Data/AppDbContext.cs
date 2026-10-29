using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Data;

public class AppDbContext : DbContext
{
    private readonly int _tenantId;
    private readonly IHttpContextAccessor? _http;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider, IHttpContextAccessor? http = null)
        : base(options)
    {
        _tenantId = tenantProvider.TenantId;
        _http = http;
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
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<QuotationApproval> QuotationApprovals => Set<QuotationApproval>();

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

    // Solution packages (calculation engine data)
    public DbSet<SolutionGroup> SolutionGroups => Set<SolutionGroup>();
    public DbSet<SolutionProductLine> SolutionProductLines => Set<SolutionProductLine>();
    public DbSet<PackageSize> PackageSizes => Set<PackageSize>();
    public DbSet<PackageItem> PackageItems => Set<PackageItem>();
    public DbSet<PackageQuantity> PackageQuantities => Set<PackageQuantity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ═══════════════════════════════════════════════════════════
        // SOFT DELETE: IsDeleted / DeletedAt / DeletedBy on every table except settings (shadow properties).
        // Declared first: the unique indexes below include DeletedKey.
        // ═══════════════════════════════════════════════════════════
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => !t.IsOwned() && SoftDelete.Applies(t.ClrType)).ToList())
        {
            var b = modelBuilder.Entity(entityType.ClrType);
            b.Property<bool>(SoftDelete.IsDeleted);
            b.Property<DateTime?>(SoftDelete.DeletedAt);
            b.Property<string?>(SoftDelete.DeletedBy).HasMaxLength(150);
            if (SoftDelete.UniqueKeyTypes.Contains(entityType.ClrType))
                b.Property<int>(SoftDelete.DeletedKey);
        }

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(nameof(Category.TenantId), nameof(Category.Slug), SoftDelete.DeletedKey).IsUnique().HasDatabaseName("UX_Categories_TenantId_Slug");
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
            e.HasIndex(nameof(Party.TenantId), nameof(Party.Email), nameof(Party.Role), SoftDelete.DeletedKey)
             .IsUnique()
             .HasDatabaseName("UX_Parties_TenantId_Email_Role");
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
        modelBuilder.Entity<TaxType>(e => e.HasIndex(nameof(TaxType.TenantId), nameof(TaxType.Name), SoftDelete.DeletedKey).IsUnique().HasDatabaseName("UX_TaxTypes_TenantId_Name"));
        modelBuilder.Entity<Role>(e => e.HasIndex(nameof(Role.TenantId), nameof(Role.Name), SoftDelete.DeletedKey).IsUnique().HasDatabaseName("UX_Roles_TenantId_Name"));
        modelBuilder.Entity<QuotationApproval>(e => { e.HasIndex(a => a.QuotationId); e.HasIndex(a => new { a.TenantId, a.Status }); });
        modelBuilder.Entity<PurchasePayment>(e => { e.HasIndex(p => p.PurchaseId); e.HasIndex(p => p.PurchaseOrderId); });
        // goods sent back to the supplier from a received purchase
        modelBuilder.Entity<PurchaseReturn>(e =>
        {
            e.HasIndex(nameof(PurchaseReturn.TenantId), nameof(PurchaseReturn.Reference), SoftDelete.DeletedKey).IsUnique().HasDatabaseName("UX_PurchaseReturns_TenantId_Reference");
            e.HasIndex(r => r.PurchaseId);
            e.HasMany(r => r.Items).WithOne(i => i.PurchaseReturn).HasForeignKey(i => i.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Refunds).WithOne(i => i.PurchaseReturn).HasForeignKey(i => i.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.HasIndex(nameof(PurchaseOrder.TenantId), nameof(PurchaseOrder.PoNumber), SoftDelete.DeletedKey).IsUnique().HasDatabaseName("UX_PurchaseOrders_TenantId_PoNumber");
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

        // Solution packages: categorization lines under groups; package quantities per item and building size
        modelBuilder.Entity<SolutionProductLine>(e => e.HasOne(l => l.Group).WithMany(g => g.ProductLines).HasForeignKey(l => l.GroupId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<PackageItem>(e => e.HasOne(i => i.ProductLine).WithMany().HasForeignKey(i => i.ProductLineId).OnDelete(DeleteBehavior.SetNull));
        modelBuilder.Entity<PackageQuantity>(e =>
        {
            e.HasOne(q => q.PackageItem).WithMany(i => i.Quantities).HasForeignKey(q => q.PackageItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(q => q.PackageSize).WithMany().HasForeignKey(q => q.PackageSizeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(q => new { q.PackageItemId, q.PackageSizeId }).IsUnique();
        });

        // Finance & Accounting
        modelBuilder.Entity<Expense>(e =>
        {
            e.HasOne(x => x.ExpenseCategory).WithMany(c => c.Expenses).HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        // ═══════════════════════════════════════════════════════════
        // MULTI-TENANCY + SOFT DELETE: Apply global query filters dynamically
        //   tenant tables:            e => e.TenantId == CurrentTenantId && !e.IsDeleted
        //   soft-deleted, no tenant:  e => !e.IsDeleted
        // ═══════════════════════════════════════════════════════════
        // For every entity implementing ITenantEntity, add a query filter:
        //   e => e.TenantId == this.CurrentTenantId
        // EF Core parameterizes the DbContext property reference, so the
        // filter evaluates CurrentTenantId from the CURRENT DbContext instance
        // on every query, even though the model is cached.
        var efProperty = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(bool));
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isTenant = typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType);
            var isSoft = !entityType.IsOwned() && SoftDelete.Applies(entityType.ClrType);
            if (!isTenant && !isSoft)
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            Expression? body = null;
            if (isTenant)
            {
                var tenantIdProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var currentTenantId = Expression.Property(
                    Expression.Constant(this),
                    nameof(CurrentTenantId)
                );
                body = Expression.Equal(tenantIdProperty, currentTenantId);
            }
            if (isSoft)
            {
                var notDeleted = Expression.Not(Expression.Call(efProperty, parameter, Expression.Constant(SoftDelete.IsDeleted)));
                body = body == null ? notDeleted : Expression.AndAlso(body, notDeleted);
            }
            entityType.SetQueryFilter(Expression.Lambda(body!, parameter));
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MULTI-TENANCY: Auto-set TenantId on new entities
    // ═══════════════════════════════════════════════════════════
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetTenantIdOnNewEntities();
        ApplySoftDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        SetTenantIdOnNewEntities();
        ApplySoftDeletes();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════
    // SOFT DELETE: a removed record is marked deleted instead (and so are the records that
    // a real delete would have removed with it - sale lines, payments ...)
    // ═══════════════════════════════════════════════════════════
    private void ApplySoftDeletes()
    {
        var done = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = DeletedSoftEntries(done);
        if (pending.Count == 0) return;

        // 1) follow the relationships like a real delete would
        while (pending.Count > 0)
        {
            foreach (var entry in pending)
            {
                done.Add(entry.Entity);
                foreach (var fk in entry.Metadata.GetReferencingForeignKeys())
                {
                    if (!IsSoft(fk.DeclaringEntityType)) continue;   // settings tables are not linked to business records
                    switch (fk.DeleteBehavior)
                    {
                        case DeleteBehavior.Cascade:
                        case DeleteBehavior.ClientCascade:
                            foreach (var dependent in LoadDependents(entry, fk))
                            {
                                var d = Entry(dependent);
                                if (d.State is EntityState.Unchanged or EntityState.Modified) d.State = EntityState.Deleted;
                            }
                            break;
                        case DeleteBehavior.Restrict:
                        case DeleteBehavior.NoAction:
                        case DeleteBehavior.ClientNoAction:
                            // a real delete would be refused by the database while something still uses the record
                            if (LoadDependents(entry, fk).Any(x => Entry(x).State is not (EntityState.Deleted or EntityState.Detached)))
                                throw new DbUpdateException($"This {entry.Metadata.ClrType.Name} is still used by {fk.DeclaringEntityType.ClrType.Name} records and can't be deleted.");
                            break;
                        // SetNull / ClientSetNull: the record is only hidden, so the links to it can stay as they are
                    }
                }
            }
            pending = DeletedSoftEntries(done);
        }

        // 2) turn every delete into an update that only marks the record
        var now = DateTime.UtcNow;
        var by = _http?.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
        if (by is { Length: > 150 }) by = by[..150];
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted && IsSoft(e.Metadata)).ToList())
        {
            entry.State = EntityState.Modified;
            foreach (var p in entry.Properties) p.IsModified = false;   // the row stays as it was
            SetMark(entry, SoftDelete.IsDeleted, true);
            SetMark(entry, SoftDelete.DeletedAt, now);
            SetMark(entry, SoftDelete.DeletedBy, by ?? "system");
            var pk = entry.Metadata.FindPrimaryKey();
            if (entry.Metadata.FindProperty(SoftDelete.DeletedKey) != null && pk?.Properties.Count == 1
                && entry.Property(pk.Properties[0].Name).CurrentValue is int id)
                SetMark(entry, SoftDelete.DeletedKey, id);
        }
    }

    private static void SetMark(EntityEntry entry, string property, object value)
    {
        var p = entry.Property(property);
        p.CurrentValue = value;
        p.IsModified = true;
    }

    private static bool IsSoft(IReadOnlyEntityType t) => !t.IsOwned() && SoftDelete.Applies(t.ClrType) && t.FindProperty(SoftDelete.IsDeleted) != null;

    private List<EntityEntry> DeletedSoftEntries(HashSet<object> done) =>
        ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted && IsSoft(e.Metadata) && !done.Contains(e.Entity)).ToList();

    /// <summary>The live records that point at this one through the given relationship (loaded if needed)</summary>
    private List<object> LoadDependents(EntityEntry principal, IForeignKey fk)
    {
        var nav = fk.PrincipalToDependent;
        if (nav != null)
        {
            if (nav.IsCollection)
            {
                var c = principal.Collection(nav.Name);
                if (!c.IsLoaded) c.Load();
                return (c.CurrentValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
            }
            var r = principal.Reference(nav.Name);
            if (!r.IsLoaded) r.Load();
            return r.CurrentValue == null ? new List<object>() : new List<object> { r.CurrentValue };
        }
        // no navigation: query the dependent table by its foreign key
        if (fk.Properties.Count != 1 || fk.PrincipalKey.Properties.Count != 1) return new List<object>();
        var keyValue = principal.Property(fk.PrincipalKey.Properties[0].Name).CurrentValue;
        if (keyValue == null) return new List<object>();
        var clr = fk.DeclaringEntityType.ClrType;
        var setMethod = typeof(DbContext).GetMethods().First(m => m.Name == nameof(Set) && m.IsGenericMethod && m.GetParameters().Length == 0).MakeGenericMethod(clr);
        var set = (IQueryable)setMethod.Invoke(this, null)!;
        var param = Expression.Parameter(clr, "d");
        var propType = fk.Properties[0].ClrType;
        var efProp = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(propType);
        var cmp = Expression.Equal(Expression.Call(efProp, param, Expression.Constant(fk.Properties[0].Name)), Expression.Constant(keyValue, propType));
        var where = Expression.Call(typeof(Queryable), nameof(Queryable.Where), new[] { clr }, set.Expression, Expression.Lambda(cmp, param));
        return set.Provider.CreateQuery(where).Cast<object>().ToList();
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
