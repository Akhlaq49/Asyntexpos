using ReactPosApi.Models;

namespace ReactPosApi.Data;

/// <summary>
/// Database object names (constitution, "Database naming"): segments of exactly 4 upper-case letters or digits joined
/// by "_" - MODULE_ENTITY for tables (ACCT_DETL), MODULE_VIEW_NAME for views, SYST_PROC_NAME for procedures.
/// Modules: SYST system, SECU security, PRTY parties, INVT inventory, SALE sales, PRCH purchases, FINC finance,
/// ACCT accounting, CATL product catalogue. Columns keep their names. Every name the code uses comes from here.
/// </summary>
public static class DbNames
{
    /// <summary>Exactly 4 upper-case letters / digits per segment, at least two segments</summary>
    public const string Pattern = "^[A-Z0-9]{4}(_[A-Z0-9]{4})+$";

    // ── SYST system ──
    public const string SystTnnt = "SYST_TNNT", SystMenu = "SYST_MENU", SystForm = "SYST_FORM", SystTerm = "SYST_TERM";
    // ── SECU security ──
    public const string SecuRole = "SECU_ROLE", SecuPerm = "SECU_PERM";
    // ── PRTY parties (users, customers, suppliers) ──
    public const string PrtyMast = "PRTY_MAST";
    // ── INVT inventory ──
    public const string InvtProd = "INVT_PROD", InvtPimg = "INVT_PIMG", InvtCatg = "INVT_CATG", InvtBrnd = "INVT_BRND",
        InvtUnit = "INVT_UNIT", InvtWrnt = "INVT_WRNT", InvtStck = "INVT_STCK", InvtPprc = "INVT_PPRC";
    // ── SALE sales ──
    public const string SaleHead = "SALE_HEAD", SaleItem = "SALE_ITEM", SalePymt = "SALE_PYMT", SaleRtrn = "SALE_RTRN",
        SaleRtit = "SALE_RTIT", SaleQuot = "SALE_QUOT", SaleQtit = "SALE_QTIT", SaleQapr = "SALE_QAPR";
    // ── PRCH purchases ──
    public const string PrchHead = "PRCH_HEAD", PrchItem = "PRCH_ITEM", PrchPymt = "PRCH_PYMT", PrchOrdr = "PRCH_ORDR",
        PrchOrit = "PRCH_ORIT", PrchRtrn = "PRCH_RTRN", PrchRtit = "PRCH_RTIT", PrchRfnd = "PRCH_RFND";
    // ── FINC finance ──
    public const string FincExpn = "FINC_EXPN", FincExct = "FINC_EXCT", FincEntr = "FINC_ENTR", FincTaxt = "FINC_TAXT";
    // ── ACCT accounting ──
    public const string AcctChrt = "ACCT_CHRT", AcctJrnl = "ACCT_JRNL", AcctDetl = "ACCT_DETL", AcctStng = "ACCT_STNG";
    public const string AcctViewUnbl = "ACCT_VIEW_UNBL", AcctViewBaln = "ACCT_VIEW_BALN";
    // ── CATL product catalogue ──
    public const string CatlCatg = "CATL_CATG", CatlFmly = "CATL_FMLY", CatlSize = "CATL_SIZE", CatlProd = "CATL_PROD",
        CatlTier = "CATL_TIER", CatlFmtr = "CATL_FMTR", CatlColr = "CATL_COLR", CatlAlis = "CATL_ALIS", CatlPkln = "CATL_PKLN",
        CatlRule = "CATL_RULE", CatlImpf = "CATL_IMPF", CatlUnmt = "CATL_UNMT";
    public const string CatlViewSugg = "CATL_VIEW_SUGG", CatlViewPkqt = "CATL_VIEW_PKQT", CatlViewTrcf = "CATL_VIEW_TRCF";
    // ── procedures ──
    public const string SystProcDlco = "SYST_PROC_DLCO";

    /// <summary>The table of every entity of the model</summary>
    public static readonly IReadOnlyDictionary<Type, string> Tables = new Dictionary<Type, string>
    {
        [typeof(Tenant)] = SystTnnt, [typeof(TenantMenuConfig)] = SystMenu, [typeof(FormFieldConfig)] = SystForm, [typeof(TermsConditions)] = SystTerm,
        [typeof(Role)] = SecuRole, [typeof(RolePermission)] = SecuPerm,
        [typeof(Party)] = PrtyMast,
        [typeof(Product)] = InvtProd, [typeof(ProductImage)] = InvtPimg, [typeof(Category)] = InvtCatg, [typeof(Brand)] = InvtBrnd,
        [typeof(Unit)] = InvtUnit, [typeof(Warranty)] = InvtWrnt, [typeof(StockEntry)] = InvtStck, [typeof(ProductPurchasePrice)] = InvtPprc,
        [typeof(Sale)] = SaleHead, [typeof(SaleItem)] = SaleItem, [typeof(SalePayment)] = SalePymt, [typeof(SalesReturn)] = SaleRtrn,
        [typeof(SalesReturnItem)] = SaleRtit, [typeof(Quotation)] = SaleQuot, [typeof(QuotationItem)] = SaleQtit, [typeof(QuotationApproval)] = SaleQapr,
        [typeof(Purchase)] = PrchHead, [typeof(PurchaseItem)] = PrchItem, [typeof(PurchasePayment)] = PrchPymt, [typeof(PurchaseOrder)] = PrchOrdr,
        [typeof(PurchaseOrderItem)] = PrchOrit, [typeof(PurchaseReturn)] = PrchRtrn, [typeof(PurchaseReturnItem)] = PrchRtit, [typeof(PurchaseReturnRefund)] = PrchRfnd,
        [typeof(Expense)] = FincExpn, [typeof(ExpenseCategory)] = FincExct, [typeof(FinanceEntry)] = FincEntr, [typeof(TaxType)] = FincTaxt,
        [typeof(LedgerAccount)] = AcctChrt, [typeof(JournalEntry)] = AcctJrnl, [typeof(JournalLine)] = AcctDetl, [typeof(AccountingSettings)] = AcctStng,
        [typeof(SolutionGroup)] = CatlCatg, [typeof(SolutionProductLine)] = CatlFmly, [typeof(PackageSize)] = CatlSize, [typeof(CatalogProduct)] = CatlProd,
        [typeof(CatalogTier)] = CatlTier, [typeof(CatalogFamilyTier)] = CatlFmtr, [typeof(CatalogColor)] = CatlColr, [typeof(CatalogAlias)] = CatlAlis,
        [typeof(CatalogPackageLine)] = CatlPkln, [typeof(CatalogDrawingRuleItem)] = CatlRule, [typeof(CatalogImportProfile)] = CatlImpf,
        [typeof(CatalogUnmatched)] = CatlUnmt,
    };

    /// <summary>Table names before the naming convention (used only to stop the app on a database not yet migrated);
    /// tables made after it (INVT_PPRC) have none</summary>
    public static readonly IReadOnlyDictionary<string, string> Legacy = new Dictionary<string, string>
    {
        ["Tenants"] = SystTnnt, ["TenantMenuConfigs"] = SystMenu, ["FormFieldConfigs"] = SystForm, ["TermsConditions"] = SystTerm,
        ["Roles"] = SecuRole, ["RolePermissions"] = SecuPerm, ["Parties"] = PrtyMast,
        ["Products"] = InvtProd, ["ProductImages"] = InvtPimg, ["Categories"] = InvtCatg, ["Brands"] = InvtBrnd, ["Units"] = InvtUnit,
        ["Warranties"] = InvtWrnt, ["StockEntries"] = InvtStck,
        ["Sales"] = SaleHead, ["SaleItems"] = SaleItem, ["SalePayments"] = SalePymt, ["SalesReturns"] = SaleRtrn, ["SalesReturnItems"] = SaleRtit,
        ["Quotations"] = SaleQuot, ["QuotationItems"] = SaleQtit, ["QuotationApprovals"] = SaleQapr,
        ["Purchases"] = PrchHead, ["PurchaseItems"] = PrchItem, ["PurchasePayments"] = PrchPymt, ["PurchaseOrders"] = PrchOrdr,
        ["PurchaseOrderItems"] = PrchOrit, ["PurchaseReturns"] = PrchRtrn, ["PurchaseReturnItems"] = PrchRtit, ["PurchaseReturnRefunds"] = PrchRfnd,
        ["Expenses"] = FincExpn, ["ExpenseCategories"] = FincExct, ["FinanceEntries"] = FincEntr, ["TaxTypes"] = FincTaxt,
        ["LedgerAccounts"] = AcctChrt, ["JournalEntries"] = AcctJrnl, ["JournalLines"] = AcctDetl, ["AccountingSettings"] = AcctStng,
        ["SolutionGroups"] = CatlCatg, ["SolutionProductLines"] = CatlFmly, ["PackageSizes"] = CatlSize, ["CatalogProducts"] = CatlProd,
        ["CatalogTiers"] = CatlTier, ["CatalogFamilyTiers"] = CatlFmtr, ["CatalogColors"] = CatlColr, ["CatalogAliases"] = CatlAlis,
        ["CatalogPackageLines"] = CatlPkln, ["CatalogDrawingRuleItems"] = CatlRule, ["CatalogImportProfiles"] = CatlImpf, ["CatalogUnmatched"] = CatlUnmt,
    };
}
