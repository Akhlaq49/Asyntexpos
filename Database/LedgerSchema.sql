-- ============================================================================================================
-- Double-entry ledger, per company (tenant). MySQL 8 / MariaDB 10.5. Safe to rerun.
-- Run by the backend on start (Data/SchemaUpgrades.cs) after CatalogSchema.sql.
--
--   LedgerAccounts      chart of accounts: a tree (ParentId); RootType Asset / Liability / Equity / Income / Expense;
--                       SystemKey = the account the posting rules use (receivable, stock, sales ...), unique per company
--   JournalEntries      one per document (VoucherType, VoucherId), unique per company
--     └─ JournalLines   AccountId, Debit, Credit, party (customer / supplier / lender) - debits = credits per entry
--   AccountingSettings  lock date; RulesVersion / NeedsRebuild (the ledger is rebuilt from the documents)
--
-- The entries are derived from the documents (Services/LedgerPoster.cs): nothing here is typed in by hand.
-- References between company tables are composite keys (TenantId, x), like the catalogue tables.
-- ============================================================================================================

CREATE TABLE IF NOT EXISTS `LedgerAccounts` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Code` varchar(20) NOT NULL,
  `Name` varchar(150) NOT NULL,
  `RootType` varchar(20) NOT NULL,
  `ParentId` int NULL,
  `IsGroup` tinyint(1) NOT NULL DEFAULT 0,
  `SystemKey` varchar(60) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_LedgerAccounts_TenantId_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_LedgerAccounts_TenantId_Code` (`TenantId`, `Code`),
  UNIQUE KEY `UX_LedgerAccounts_TenantId_SystemKey` (`TenantId`, `SystemKey`),
  KEY `IX_LedgerAccounts_Parent` (`TenantId`, `ParentId`),
  CONSTRAINT `FK_LedgerAccounts_Parent` FOREIGN KEY (`TenantId`, `ParentId`) REFERENCES `LedgerAccounts` (`TenantId`, `Id`),
  CONSTRAINT `CK_LedgerAccounts_RootType` CHECK (`RootType` IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense'))
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `JournalEntries` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Date` datetime(6) NOT NULL,
  `VoucherType` varchar(40) NOT NULL,
  `VoucherId` int NOT NULL,
  `Reference` varchar(100) NULL,
  `Description` varchar(300) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_JournalEntries_TenantId_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_JournalEntries_Voucher` (`TenantId`, `VoucherType`, `VoucherId`),
  KEY `IX_JournalEntries_Date` (`TenantId`, `Date`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `JournalLines` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `JournalEntryId` int NOT NULL,
  `AccountId` int NOT NULL,
  `Debit` decimal(18,2) NOT NULL DEFAULT 0,
  `Credit` decimal(18,2) NOT NULL DEFAULT 0,
  `PartyType` varchar(20) NULL,
  `PartyId` int NULL,
  `PartyName` varchar(200) NULL,
  `Note` varchar(300) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_JournalLines_Entry` (`TenantId`, `JournalEntryId`),
  KEY `IX_JournalLines_Account` (`TenantId`, `AccountId`),
  KEY `IX_JournalLines_Party` (`TenantId`, `PartyType`, `PartyName`),
  CONSTRAINT `FK_JournalLines_Entry` FOREIGN KEY (`TenantId`, `JournalEntryId`) REFERENCES `JournalEntries` (`TenantId`, `Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_JournalLines_Account` FOREIGN KEY (`TenantId`, `AccountId`) REFERENCES `LedgerAccounts` (`TenantId`, `Id`),
  CONSTRAINT `CK_JournalLines_Amounts` CHECK (`Debit` >= 0 AND `Credit` >= 0 AND NOT (`Debit` > 0 AND `Credit` > 0))
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AccountingSettings` (
  `TenantId` int NOT NULL,
  `LockDate` datetime(6) NULL,
  `RulesVersion` int NOT NULL DEFAULT 0,
  `NeedsRebuild` tinyint(1) NOT NULL DEFAULT 1,
  `RebuiltAt` datetime(6) NULL,
  `LastError` varchar(500) NULL,
  PRIMARY KEY (`TenantId`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- entries whose debits and credits differ (should always be empty)
CREATE OR REPLACE VIEW `v_unbalanced_journal` AS
SELECT e.`TenantId`, e.`Id` AS `JournalEntryId`, e.`VoucherType`, e.`VoucherId`, e.`Reference`,
       SUM(l.`Debit`) AS `Debit`, SUM(l.`Credit`) AS `Credit`
FROM `JournalEntries` e
JOIN `JournalLines` l ON l.`TenantId` = e.`TenantId` AND l.`JournalEntryId` = e.`Id`
GROUP BY e.`TenantId`, e.`Id`, e.`VoucherType`, e.`VoucherId`, e.`Reference`
HAVING SUM(l.`Debit`) <> SUM(l.`Credit`);

-- balance of every account (debit - credit), per company
CREATE OR REPLACE VIEW `v_account_balance` AS
SELECT a.`TenantId`, a.`Id` AS `AccountId`, a.`Code`, a.`Name`, a.`RootType`,
       COALESCE(SUM(l.`Debit`), 0) AS `Debit`, COALESCE(SUM(l.`Credit`), 0) AS `Credit`,
       COALESCE(SUM(l.`Debit`), 0) - COALESCE(SUM(l.`Credit`), 0) AS `Balance`
FROM `LedgerAccounts` a
LEFT JOIN `JournalLines` l ON l.`TenantId` = a.`TenantId` AND l.`AccountId` = a.`Id`
GROUP BY a.`TenantId`, a.`Id`, a.`Code`, a.`Name`, a.`RootType`;
