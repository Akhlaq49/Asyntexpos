-- ============================================================================================================
-- Journal vouchers (entries posted by hand): two new tables, nothing existing is changed. Safe to rerun.
-- The same statements are in Database/ACCT/Schema.sql, which the backend runs on start; this file applies them
-- to a live database before the new backend is deployed. Needs ACCT_CHRT and SYST_PROC_ADCL (already there).
-- Rollback (only while the tables are empty): DROP TABLE `ACCT_JVLN`; DROP TABLE `ACCT_JVHD`;
-- ============================================================================================================

CREATE TABLE IF NOT EXISTS `ACCT_JVHD` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `Date` datetime(6) NOT NULL,
  `Reference` varchar(30) NOT NULL,
  `Description` varchar(300) NOT NULL,
  `ReversalOfId` int NULL,
  `CreatedBy` varchar(150) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeletedAt` datetime(6) NULL,
  `DeletedBy` varchar(150) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_JournalVouchers_TenantId_Id` (`TenantId`, `Id`),
  UNIQUE KEY `UX_JournalVouchers_TenantId_Reference` (`TenantId`, `Reference`),
  KEY `IX_JournalVouchers_Date` (`TenantId`, `Date`),
  KEY `IX_JournalVouchers_ReversalOf` (`TenantId`, `ReversalOfId`),
  CONSTRAINT `FK_JournalVouchers_ReversalOf` FOREIGN KEY (`TenantId`, `ReversalOfId`) REFERENCES `ACCT_JVHD` (`TenantId`, `Id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ACCT_JVLN` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TenantId` int NOT NULL,
  `JournalVoucherId` int NOT NULL,
  `LineNo` int NOT NULL,
  `AccountId` int NOT NULL,
  `Debit` decimal(18,2) NOT NULL DEFAULT 0,
  `Credit` decimal(18,2) NOT NULL DEFAULT 0,
  `PartyType` varchar(20) NULL,
  `PartyId` int NULL,
  `PartyName` varchar(200) NULL,
  `Note` varchar(300) NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeletedAt` datetime(6) NULL,
  `DeletedBy` varchar(150) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_JournalVoucherLines_Voucher` (`TenantId`, `JournalVoucherId`),
  KEY `IX_JournalVoucherLines_Account` (`TenantId`, `AccountId`),
  CONSTRAINT `FK_JournalVoucherLines_Voucher` FOREIGN KEY (`TenantId`, `JournalVoucherId`) REFERENCES `ACCT_JVHD` (`TenantId`, `Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_JournalVoucherLines_Account` FOREIGN KEY (`TenantId`, `AccountId`) REFERENCES `ACCT_CHRT` (`TenantId`, `Id`),
  CONSTRAINT `CK_JournalVoucherLines_Amounts` CHECK (`Debit` >= 0 AND `Credit` >= 0 AND (`Debit` > 0) <> (`Credit` > 0))
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CALL SYST_PROC_ADCL('ACCT_JVHD', 'IsDeleted', '`IsDeleted` tinyint(1) NOT NULL DEFAULT 0');
CALL SYST_PROC_ADCL('ACCT_JVHD', 'DeletedAt', '`DeletedAt` datetime(6) NULL');
CALL SYST_PROC_ADCL('ACCT_JVHD', 'DeletedBy', '`DeletedBy` varchar(150) NULL');
CALL SYST_PROC_ADCL('ACCT_JVLN', 'IsDeleted', '`IsDeleted` tinyint(1) NOT NULL DEFAULT 0');
CALL SYST_PROC_ADCL('ACCT_JVLN', 'DeletedAt', '`DeletedAt` datetime(6) NULL');
CALL SYST_PROC_ADCL('ACCT_JVLN', 'DeletedBy', '`DeletedBy` varchar(150) NULL');
