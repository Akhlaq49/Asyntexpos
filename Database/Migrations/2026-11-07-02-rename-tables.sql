-- ============================================================================================================
-- 2026-11-07-02  Rename every table to the naming convention (constitution "Database naming"): MODULE_ENTITY,
-- 4 upper-case letters per segment.  MySQL 8 / MariaDB 10.5.
-- Run by a person (never by the app), AFTER 2026-11-07-01-drop-unused-tables.sql and BEFORE starting the new
-- backend (it refuses to start on old names, and recreates the views and procedures on start):
--   mysql <db> < 2026-11-07-02-rename-tables.sql
-- One RENAME TABLE statement: all tables are renamed together or none is. Foreign keys, indexes and data move
-- with their tables. Undo: 2026-11-07-02-rollback-rename-tables.sql.
-- (On a server with lower_case_table_names = 1 the names are stored in lower case; queries work either way.)
-- ============================================================================================================

RENAME TABLE
  `LedgerAccounts` TO `ACCT_CHRT`,
  `JournalLines` TO `ACCT_DETL`,
  `JournalEntries` TO `ACCT_JRNL`,
  `AccountingSettings` TO `ACCT_STNG`,
  `CatalogAliases` TO `CATL_ALIS`,
  `SolutionGroups` TO `CATL_CATG`,
  `CatalogColors` TO `CATL_COLR`,
  `SolutionProductLines` TO `CATL_FMLY`,
  `CatalogFamilyTiers` TO `CATL_FMTR`,
  `CatalogImportProfiles` TO `CATL_IMPF`,
  `CatalogPackageLines` TO `CATL_PKLN`,
  `CatalogProducts` TO `CATL_PROD`,
  `CatalogDrawingRuleItems` TO `CATL_RULE`,
  `PackageSizes` TO `CATL_SIZE`,
  `CatalogTiers` TO `CATL_TIER`,
  `CatalogUnmatched` TO `CATL_UNMT`,
  `FinanceEntries` TO `FINC_ENTR`,
  `ExpenseCategories` TO `FINC_EXCT`,
  `Expenses` TO `FINC_EXPN`,
  `TaxTypes` TO `FINC_TAXT`,
  `Brands` TO `INVT_BRND`,
  `Categories` TO `INVT_CATG`,
  `ProductImages` TO `INVT_PIMG`,
  `Products` TO `INVT_PROD`,
  `StockEntries` TO `INVT_STCK`,
  `Units` TO `INVT_UNIT`,
  `Warranties` TO `INVT_WRNT`,
  `Purchases` TO `PRCH_HEAD`,
  `PurchaseItems` TO `PRCH_ITEM`,
  `PurchaseOrders` TO `PRCH_ORDR`,
  `PurchaseOrderItems` TO `PRCH_ORIT`,
  `PurchasePayments` TO `PRCH_PYMT`,
  `PurchaseReturnRefunds` TO `PRCH_RFND`,
  `PurchaseReturnItems` TO `PRCH_RTIT`,
  `PurchaseReturns` TO `PRCH_RTRN`,
  `Parties` TO `PRTY_MAST`,
  `Sales` TO `SALE_HEAD`,
  `SaleItems` TO `SALE_ITEM`,
  `SalePayments` TO `SALE_PYMT`,
  `QuotationApprovals` TO `SALE_QAPR`,
  `QuotationItems` TO `SALE_QTIT`,
  `Quotations` TO `SALE_QUOT`,
  `SalesReturnItems` TO `SALE_RTIT`,
  `SalesReturns` TO `SALE_RTRN`,
  `RolePermissions` TO `SECU_PERM`,
  `Roles` TO `SECU_ROLE`,
  `FormFieldConfigs` TO `SYST_FORM`,
  `TenantMenuConfigs` TO `SYST_MENU`,
  `TermsConditions` TO `SYST_TERM`,
  `Tenants` TO `SYST_TNNT`;
