-- ============================================================================================================
-- SYST: system procedures. Microsoft SQL Server (T-SQL). Safe to rerun.
-- Run by the backend on start (Data/DatabaseSetup.cs) before Database/INVT and Database/ACCT.
-- Batches are separated by lines holding only GO (Data/SqlScript.cs).
-- Names: constitution "Database naming" - 4-letter segments joined by "_".
-- ============================================================================================================

-- ── delete a company and everything it owns ─────────────────────────────────────────────────────────────────
-- every table with a TenantId column, then the company; one transaction. Not called by the app - run by hand:
--   EXEC SYST_PROC_DLCO @CompanyId = 2;
CREATE OR ALTER PROCEDURE SYST_PROC_DLCO @CompanyId int
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;
  IF NOT EXISTS (SELECT 1 FROM [SYST_TNNT] WHERE [Id] = @CompanyId)
    THROW 50001, 'No such company', 1;

  DECLARE @tables TABLE ([Name] sysname);
  INSERT INTO @tables ([Name])
  SELECT t.[name] FROM sys.tables t JOIN sys.columns c ON c.[object_id] = t.[object_id] AND c.[name] = 'TenantId';

  DECLARE @sql nvarchar(max);
  BEGIN TRANSACTION;
    -- the company's own rows reference each other; all of them go
    SELECT @sql = STRING_AGG(CAST(N'ALTER TABLE ' + QUOTENAME([Name]) + N' NOCHECK CONSTRAINT ALL;' AS nvarchar(max)), N' ') FROM @tables;
    EXEC sp_executesql @sql;
    SELECT @sql = STRING_AGG(CAST(N'DELETE FROM ' + QUOTENAME([Name]) + N' WHERE [TenantId] = @id;' AS nvarchar(max)), N' ') FROM @tables;
    EXEC sp_executesql @sql, N'@id int', @id = @CompanyId;
    SELECT @sql = STRING_AGG(CAST(N'ALTER TABLE ' + QUOTENAME([Name]) + N' WITH CHECK CHECK CONSTRAINT ALL;' AS nvarchar(max)), N' ') FROM @tables;
    EXEC sp_executesql @sql;
    DELETE FROM [SYST_TNNT] WHERE [Id] = @CompanyId;
  COMMIT;
END
GO
