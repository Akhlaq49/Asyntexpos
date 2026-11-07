-- ============================================================================================================
-- Undo of 2026-11-07-02-rename-tables.sql (puts the old table names back). Run with the OLD backend only.
-- ============================================================================================================

DROP VIEW IF EXISTS `CATL_VIEW_SUGG`;
DROP VIEW IF EXISTS `CATL_VIEW_PKQT`;
DROP VIEW IF EXISTS `CATL_VIEW_TRCF`;
DROP VIEW IF EXISTS `ACCT_VIEW_UNBL`;
DROP VIEW IF EXISTS `ACCT_VIEW_BALN`;

DROP PROCEDURE IF EXISTS `SYST_PROC_DLCO`;
DROP PROCEDURE IF EXISTS `SYST_PROC_EXIF`;
DROP PROCEDURE IF EXISTS `SYST_PROC_ADCL`;
DROP PROCEDURE IF EXISTS `SYST_PROC_ADIX`;
DROP PROCEDURE IF EXISTS `SYST_PROC_ADFK`;

RENAME TABLE
  `ACCT_CHRT` TO `LedgerAccounts`,
  `ACCT_DETL` TO `JournalLines`,
  `ACCT_JRNL` TO `JournalEntries`,
  `ACCT_STNG` TO `AccountingSettings`,
  `CATL_ALIS` TO `CatalogAliases`,
  `CATL_CATG` TO `SolutionGroups`,
  `CATL_COLR` TO `CatalogColors`,
  `CATL_FMLY` TO `SolutionProductLines`,
  `CATL_FMTR` TO `CatalogFamilyTiers`,
  `CATL_IMPF` TO `CatalogImportProfiles`,
  `CATL_PKLN` TO `CatalogPackageLines`,
  `CATL_PROD` TO `CatalogProducts`,
  `CATL_RULE` TO `CatalogDrawingRuleItems`,
  `CATL_SIZE` TO `PackageSizes`,
  `CATL_TIER` TO `CatalogTiers`,
  `CATL_UNMT` TO `CatalogUnmatched`,
  `FINC_ENTR` TO `FinanceEntries`,
  `FINC_EXCT` TO `ExpenseCategories`,
  `FINC_EXPN` TO `Expenses`,
  `FINC_TAXT` TO `TaxTypes`,
  `INVT_BRND` TO `Brands`,
  `INVT_CATG` TO `Categories`,
  `INVT_PIMG` TO `ProductImages`,
  `INVT_PROD` TO `Products`,
  `INVT_STCK` TO `StockEntries`,
  `INVT_UNIT` TO `Units`,
  `INVT_WRNT` TO `Warranties`,
  `PRCH_HEAD` TO `Purchases`,
  `PRCH_ITEM` TO `PurchaseItems`,
  `PRCH_ORDR` TO `PurchaseOrders`,
  `PRCH_ORIT` TO `PurchaseOrderItems`,
  `PRCH_PYMT` TO `PurchasePayments`,
  `PRCH_RFND` TO `PurchaseReturnRefunds`,
  `PRCH_RTIT` TO `PurchaseReturnItems`,
  `PRCH_RTRN` TO `PurchaseReturns`,
  `PRTY_MAST` TO `Parties`,
  `SALE_HEAD` TO `Sales`,
  `SALE_ITEM` TO `SaleItems`,
  `SALE_PYMT` TO `SalePayments`,
  `SALE_QAPR` TO `QuotationApprovals`,
  `SALE_QTIT` TO `QuotationItems`,
  `SALE_QUOT` TO `Quotations`,
  `SALE_RTIT` TO `SalesReturnItems`,
  `SALE_RTRN` TO `SalesReturns`,
  `SECU_PERM` TO `RolePermissions`,
  `SECU_ROLE` TO `Roles`,
  `SYST_FORM` TO `FormFieldConfigs`,
  `SYST_MENU` TO `TenantMenuConfigs`,
  `SYST_TERM` TO `TermsConditions`,
  `SYST_TNNT` TO `Tenants`;
