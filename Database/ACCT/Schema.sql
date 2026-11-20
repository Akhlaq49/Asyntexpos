-- ============================================================================================================
-- Double-entry ledger, per company (tenant). Microsoft SQL Server (T-SQL). Safe to rerun.
-- Run by the backend on start (Data/DatabaseSetup.cs) after Database/INVT/Schema.sql. The tables are made from
-- the model (EnsureCreated); this adds the composite keys, check constraints and views EF can't describe.
--
--   ACCT_CHRT      chart of accounts: a tree (ParentId); RootType Asset / Liability / Equity / Income / Expense;
--                       SystemKey = the account the posting rules use (receivable, stock, sales ...), unique per company
--   ACCT_JRNL      one per document (VoucherType, VoucherId), unique per company
--     └─ ACCT_DETL   AccountId, Debit, Credit, party (customer / supplier / lender) - debits = credits per entry
--   ACCT_STNG      lock date; RulesVersion / NeedsRebuild (the ledger is rebuilt from the documents)
--   ACCT_JVHD      journal voucher: an entry typed in by a person (reclass, correction, depreciation, accrual)
--     └─ ACCT_JVLN   AccountId, Debit or Credit, party - debits = credits per voucher
--
-- The entries are derived from the documents (Services/ACCT/LedgerPoster.cs); the journal voucher is the one
-- document whose accounts are chosen by hand - it is still posted like the others, so a rebuild keeps it.
-- References between company tables are composite keys (TenantId, x). Lines -> their entry / voucher keep the
-- model's own cascading key (a second cascading path is not allowed by SQL Server).
-- ============================================================================================================

-- ── ACCT_CHRT chart of accounts ──
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_LedgerAccounts_TenantId_Id' AND [object_id] = OBJECT_ID('ACCT_CHRT'))
  CREATE UNIQUE INDEX [UX_LedgerAccounts_TenantId_Id] ON [ACCT_CHRT] ([TenantId], [Id]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_LedgerAccounts_TenantId_Code' AND [object_id] = OBJECT_ID('ACCT_CHRT'))
  CREATE UNIQUE INDEX [UX_LedgerAccounts_TenantId_Code] ON [ACCT_CHRT] ([TenantId], [Code]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_LedgerAccounts_TenantId_SystemKey' AND [object_id] = OBJECT_ID('ACCT_CHRT'))
  CREATE UNIQUE INDEX [UX_LedgerAccounts_TenantId_SystemKey] ON [ACCT_CHRT] ([TenantId], [SystemKey]) WHERE [SystemKey] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_LedgerAccounts_Parent' AND [object_id] = OBJECT_ID('ACCT_CHRT'))
  CREATE INDEX [IX_LedgerAccounts_Parent] ON [ACCT_CHRT] ([TenantId], [ParentId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_LedgerAccounts_Parent')
  ALTER TABLE [ACCT_CHRT] ADD CONSTRAINT [FK_LedgerAccounts_Parent] FOREIGN KEY ([TenantId], [ParentId]) REFERENCES [ACCT_CHRT] ([TenantId], [Id]);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = 'CK_LedgerAccounts_RootType')
  ALTER TABLE [ACCT_CHRT] ADD CONSTRAINT [CK_LedgerAccounts_RootType] CHECK ([RootType] IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense'));
GO

-- ── ACCT_JRNL journal entries ──
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_JournalEntries_TenantId_Id' AND [object_id] = OBJECT_ID('ACCT_JRNL'))
  CREATE UNIQUE INDEX [UX_JournalEntries_TenantId_Id] ON [ACCT_JRNL] ([TenantId], [Id]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_JournalEntries_Voucher' AND [object_id] = OBJECT_ID('ACCT_JRNL'))
  CREATE UNIQUE INDEX [UX_JournalEntries_Voucher] ON [ACCT_JRNL] ([TenantId], [VoucherType], [VoucherId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalEntries_Date' AND [object_id] = OBJECT_ID('ACCT_JRNL'))
  CREATE INDEX [IX_JournalEntries_Date] ON [ACCT_JRNL] ([TenantId], [Date]);
GO

-- ── ACCT_DETL journal lines ──
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalLines_Entry' AND [object_id] = OBJECT_ID('ACCT_DETL'))
  CREATE INDEX [IX_JournalLines_Entry] ON [ACCT_DETL] ([TenantId], [JournalEntryId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalLines_Account' AND [object_id] = OBJECT_ID('ACCT_DETL'))
  CREATE INDEX [IX_JournalLines_Account] ON [ACCT_DETL] ([TenantId], [AccountId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalLines_Party' AND [object_id] = OBJECT_ID('ACCT_DETL'))
  CREATE INDEX [IX_JournalLines_Party] ON [ACCT_DETL] ([TenantId], [PartyType], [PartyName]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_JournalLines_Account')
  ALTER TABLE [ACCT_DETL] ADD CONSTRAINT [FK_JournalLines_Account] FOREIGN KEY ([TenantId], [AccountId]) REFERENCES [ACCT_CHRT] ([TenantId], [Id]);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = 'CK_JournalLines_Amounts')
  ALTER TABLE [ACCT_DETL] ADD CONSTRAINT [CK_JournalLines_Amounts] CHECK ([Debit] >= 0 AND [Credit] >= 0 AND NOT ([Debit] > 0 AND [Credit] > 0));
GO

-- ── ACCT_JVHD journal vouchers ──
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_JournalVouchers_TenantId_Id' AND [object_id] = OBJECT_ID('ACCT_JVHD'))
  CREATE UNIQUE INDEX [UX_JournalVouchers_TenantId_Id] ON [ACCT_JVHD] ([TenantId], [Id]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_JournalVouchers_TenantId_Reference' AND [object_id] = OBJECT_ID('ACCT_JVHD'))
  CREATE UNIQUE INDEX [UX_JournalVouchers_TenantId_Reference] ON [ACCT_JVHD] ([TenantId], [Reference]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalVouchers_Date' AND [object_id] = OBJECT_ID('ACCT_JVHD'))
  CREATE INDEX [IX_JournalVouchers_Date] ON [ACCT_JVHD] ([TenantId], [Date]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalVouchers_ReversalOf' AND [object_id] = OBJECT_ID('ACCT_JVHD'))
  CREATE INDEX [IX_JournalVouchers_ReversalOf] ON [ACCT_JVHD] ([TenantId], [ReversalOfId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_JournalVouchers_ReversalOf')
  ALTER TABLE [ACCT_JVHD] ADD CONSTRAINT [FK_JournalVouchers_ReversalOf] FOREIGN KEY ([TenantId], [ReversalOfId]) REFERENCES [ACCT_JVHD] ([TenantId], [Id]);
GO

-- ── ACCT_JVLN journal voucher lines ──
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalVoucherLines_Voucher' AND [object_id] = OBJECT_ID('ACCT_JVLN'))
  CREATE INDEX [IX_JournalVoucherLines_Voucher] ON [ACCT_JVLN] ([TenantId], [JournalVoucherId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_JournalVoucherLines_Account' AND [object_id] = OBJECT_ID('ACCT_JVLN'))
  CREATE INDEX [IX_JournalVoucherLines_Account] ON [ACCT_JVLN] ([TenantId], [AccountId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_JournalVoucherLines_Account')
  ALTER TABLE [ACCT_JVLN] ADD CONSTRAINT [FK_JournalVoucherLines_Account] FOREIGN KEY ([TenantId], [AccountId]) REFERENCES [ACCT_CHRT] ([TenantId], [Id]);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = 'CK_JournalVoucherLines_Amounts')
  ALTER TABLE [ACCT_JVLN] ADD CONSTRAINT [CK_JournalVoucherLines_Amounts]
    CHECK ([Debit] >= 0 AND [Credit] >= 0 AND (([Debit] > 0 AND [Credit] = 0) OR ([Credit] > 0 AND [Debit] = 0)));
GO

-- entries whose debits and credits differ (should always be empty)
CREATE OR ALTER VIEW [ACCT_VIEW_UNBL] AS
SELECT e.[TenantId], e.[Id] AS [JournalEntryId], e.[VoucherType], e.[VoucherId], e.[Reference],
       SUM(l.[Debit]) AS [Debit], SUM(l.[Credit]) AS [Credit]
FROM [ACCT_JRNL] e
JOIN [ACCT_DETL] l ON l.[TenantId] = e.[TenantId] AND l.[JournalEntryId] = e.[Id]
GROUP BY e.[TenantId], e.[Id], e.[VoucherType], e.[VoucherId], e.[Reference]
HAVING SUM(l.[Debit]) <> SUM(l.[Credit]);
GO

-- balance of every account (debit - credit), per company
CREATE OR ALTER VIEW [ACCT_VIEW_BALN] AS
SELECT a.[TenantId], a.[Id] AS [AccountId], a.[Code], a.[Name], a.[RootType],
       COALESCE(SUM(l.[Debit]), 0) AS [Debit], COALESCE(SUM(l.[Credit]), 0) AS [Credit],
       COALESCE(SUM(l.[Debit]), 0) - COALESCE(SUM(l.[Credit]), 0) AS [Balance]
FROM [ACCT_CHRT] a
LEFT JOIN [ACCT_DETL] l ON l.[TenantId] = a.[TenantId] AND l.[AccountId] = a.[Id]
GROUP BY a.[TenantId], a.[Id], a.[Code], a.[Name], a.[RootType];
GO
