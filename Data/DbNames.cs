using ReactPosApi.Models;

namespace ReactPosApi.Data;

/// <summary>
/// Database object names (constitution, "Database naming"): segments of exactly 4 upper-case letters or digits joined
/// by "_" - MODULE_ENTITY for tables (ACCT_DETL), MODULE_VIEW_NAME for views, SYST_PROC_NAME for procedures.
/// Modules: SYST system, SECU security, PRTY parties, INVT inventory, SALE sales, PRCH purchases, FINC finance,
/// ACCT accounting. Columns keep their names. Every name the code uses comes from here.
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
        SaleRtit = "SALE_RTIT";
    // ── PRCH purchases ──
    public const string PrchHead = "PRCH_HEAD", PrchItem = "PRCH_ITEM", PrchPymt = "PRCH_PYMT", PrchOrdr = "PRCH_ORDR",
        PrchOrit = "PRCH_ORIT", PrchRtrn = "PRCH_RTRN", PrchRtit = "PRCH_RTIT", PrchRfnd = "PRCH_RFND";
    // ── FINC finance ──
    public const string FincExpn = "FINC_EXPN", FincExct = "FINC_EXCT", FincEntr = "FINC_ENTR", FincTaxt = "FINC_TAXT";
    // ── ACCT accounting ──
    public const string AcctChrt = "ACCT_CHRT", AcctJrnl = "ACCT_JRNL", AcctDetl = "ACCT_DETL", AcctStng = "ACCT_STNG",
        AcctJvhd = "ACCT_JVHD", AcctJvln = "ACCT_JVLN";
    public const string AcctViewUnbl = "ACCT_VIEW_UNBL", AcctViewBaln = "ACCT_VIEW_BALN";
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
        [typeof(SalesReturnItem)] = SaleRtit,
        [typeof(Purchase)] = PrchHead, [typeof(PurchaseItem)] = PrchItem, [typeof(PurchasePayment)] = PrchPymt, [typeof(PurchaseOrder)] = PrchOrdr,
        [typeof(PurchaseOrderItem)] = PrchOrit, [typeof(PurchaseReturn)] = PrchRtrn, [typeof(PurchaseReturnItem)] = PrchRtit, [typeof(PurchaseReturnRefund)] = PrchRfnd,
        [typeof(Expense)] = FincExpn, [typeof(ExpenseCategory)] = FincExct, [typeof(FinanceEntry)] = FincEntr, [typeof(TaxType)] = FincTaxt,
        [typeof(LedgerAccount)] = AcctChrt, [typeof(JournalEntry)] = AcctJrnl, [typeof(JournalLine)] = AcctDetl, [typeof(AccountingSettings)] = AcctStng,
        [typeof(JournalVoucher)] = AcctJvhd, [typeof(JournalVoucherLine)] = AcctJvln,
    };
}
