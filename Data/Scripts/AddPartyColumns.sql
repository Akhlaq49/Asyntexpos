-- =========================================================
-- AddPartyColumns.sql
-- Adds new columns to the Parties table for Supplier,
-- Biller, Store, and Warehouse support.
-- Idempotent – safe to run multiple times.
-- =========================================================

-- LastName
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'LastName')
BEGIN
    ALTER TABLE [Parties] ADD [LastName] NVARCHAR(200) NULL;
END
GO

-- PhoneWork
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'PhoneWork')
BEGIN
    ALTER TABLE [Parties] ADD [PhoneWork] NVARCHAR(50) NULL;
END
GO

-- State
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'State')
BEGIN
    ALTER TABLE [Parties] ADD [State] NVARCHAR(100) NULL;
END
GO

-- Country
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'Country')
BEGIN
    ALTER TABLE [Parties] ADD [Country] NVARCHAR(100) NULL;
END
GO

-- PostalCode
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'PostalCode')
BEGIN
    ALTER TABLE [Parties] ADD [PostalCode] NVARCHAR(20) NULL;
END
GO

-- Code (e.g. SU001, BI001, ST001, WH001)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'Code')
BEGIN
    ALTER TABLE [Parties] ADD [Code] NVARCHAR(50) NULL;
END
GO

-- CompanyName
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'CompanyName')
BEGIN
    ALTER TABLE [Parties] ADD [CompanyName] NVARCHAR(200) NULL;
END
GO

-- ContactPerson
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'ContactPerson')
BEGIN
    ALTER TABLE [Parties] ADD [ContactPerson] NVARCHAR(200) NULL;
END
GO

-- UserName (for Store role)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'UserName')
BEGIN
    ALTER TABLE [Parties] ADD [UserName] NVARCHAR(100) NULL;
END
GO

-- Optional: update the unique index on Email to be composite with Role
-- so the same email can exist across different roles.
-- First drop the old index if it exists, then create the new one.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Parties') AND name = 'IX_Parties_Email')
BEGIN
    DROP INDEX [IX_Parties_Email] ON [Parties];
END
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Parties_Email_Role]
ON [Parties] ([Email], [Role])
WHERE [Email] IS NOT NULL;
GO

PRINT 'AddPartyColumns.sql completed successfully.';
GO
