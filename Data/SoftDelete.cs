using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;

namespace ReactPosApi.Data;

/// <summary>
/// Soft delete: deleting a record only marks it (IsDeleted, DeletedAt, DeletedBy) and hides it everywhere;
/// nothing is removed from the database. The columns are shadow properties, so the model classes don't carry them.
/// </summary>
public static class SoftDelete
{
    public const string IsDeleted = "IsDeleted";
    public const string DeletedAt = "DeletedAt";
    public const string DeletedBy = "DeletedBy";
    /// <summary>0 while the record is live, its Id once deleted - part of the unique indexes,
    /// so a deleted record never blocks a new one with the same email / number / name</summary>
    public const string DeletedKey = "DeletedKey";

    /// <summary>Settings that are rewritten as a whole on every save: still really deleted</summary>
    public static readonly HashSet<Type> HardDeleteTypes = new()
    {
        typeof(Tenant), typeof(RolePermission), typeof(FormFieldConfig), typeof(TenantMenuConfig), typeof(TermsConditions),
    };

    /// <summary>Soft-deleted types that have a unique index (it gets DeletedKey added)</summary>
    public static readonly HashSet<Type> UniqueKeyTypes = new()
    {
        typeof(Category), typeof(Party), typeof(TaxType), typeof(Role), typeof(PurchaseReturn), typeof(PurchaseOrder),
    };

    public static bool Applies(Type clrType) => !HardDeleteTypes.Contains(clrType);

    /// <summary>Only records that are not deleted - for queries that use IgnoreQueryFilters() (which drops this filter too)</summary>
    public static IQueryable<T> NotDeleted<T>(this IQueryable<T> query) where T : class
        => query.Where(e => !EF.Property<bool>(e, IsDeleted));
}
