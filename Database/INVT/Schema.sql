-- ============================================================================================================
-- INVT: inventory additions EF can't describe. Microsoft SQL Server (T-SQL). Safe to rerun.
-- Run by the backend on start (Data/DatabaseSetup.cs) after Database/SYST/Procedures.sql; the tables themselves
-- are made from the model (EnsureCreated).
--
--   INVT_PPRC   the purchase price of each inventory product (one row per product), set on Inventory > Purchase
--               Prices; purchase orders take their unit cost from it.
--               Until someone enters it, it is the product's sale price (no margin) and follows it - Services/INVT/PurchasePriceService.cs
-- ============================================================================================================

-- products: (TenantId, Id) is unique, so company tables can reference it
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UX_Products_Tenant_Id' AND [object_id] = OBJECT_ID('INVT_PROD'))
  CREATE UNIQUE INDEX [UX_Products_Tenant_Id] ON [INVT_PROD] ([TenantId], [Id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = 'CK_INVT_PPRC_Price')
  ALTER TABLE [INVT_PPRC] ADD CONSTRAINT [CK_INVT_PPRC_Price] CHECK ([PurchasePrice] >= 0);
GO

-- every product has its purchase price: the ones without get their sale price (not entered yet: UpdatedBy = 'system')
INSERT INTO [INVT_PPRC] ([TenantId], [ProductId], [PurchasePrice], [UpdatedAt], [UpdatedBy])
SELECT p.[TenantId], p.[Id], CASE WHEN p.[Price] > 0 THEN p.[Price] ELSE 0 END, SYSUTCDATETIME(), 'system'
FROM [INVT_PROD] p
WHERE NOT EXISTS (SELECT 1 FROM [INVT_PPRC] x WHERE x.[TenantId] = p.[TenantId] AND x.[ProductId] = p.[Id]);
GO
