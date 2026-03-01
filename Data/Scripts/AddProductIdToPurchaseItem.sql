-- ============================================================
-- SQL Script: Add ProductId FK to PurchaseItems for Inventory Sync
-- Description: Adds ProductId column to PurchaseItems table
--              linking each purchase line item to a Product
--              via foreign key for inventory synchronization.
-- Date: 2026-02-27
-- Execute in SQL Server Management Studio against your database
-- ============================================================

-- ========================================
-- STEP 1: Ensure Purchases table has all required columns
--         (OrderTax, Discount, Shipping were added in model)
-- ========================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Purchases' AND COLUMN_NAME = 'OrderTax'
)
BEGIN
    ALTER TABLE [dbo].[Purchases]
    ADD [OrderTax] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Column [OrderTax] added to [Purchases]';
END
ELSE
    PRINT 'Column [OrderTax] already exists on [Purchases]';

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Purchases' AND COLUMN_NAME = 'Discount'
)
BEGIN
    ALTER TABLE [dbo].[Purchases]
    ADD [Discount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Column [Discount] added to [Purchases]';
END
ELSE
    PRINT 'Column [Discount] already exists on [Purchases]';

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Purchases' AND COLUMN_NAME = 'Shipping'
)
BEGIN
    ALTER TABLE [dbo].[Purchases]
    ADD [Shipping] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Column [Shipping] added to [Purchases]';
END
ELSE
    PRINT 'Column [Shipping] already exists on [Purchases]';

-- ========================================
-- STEP 2: Create PurchaseItems table if it doesn't exist
-- ========================================

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PurchaseItems')
BEGIN
    CREATE TABLE [dbo].[PurchaseItems] (
        [Id]             INT            NOT NULL IDENTITY(1,1),
        [PurchaseId]     INT            NOT NULL,
        [ProductId]      INT            NULL,
        [ProductName]    NVARCHAR(100)  NOT NULL,
        [Quantity]       DECIMAL(18,2)  NOT NULL,
        [PurchasePrice]  DECIMAL(18,2)  NOT NULL,
        [Discount]       DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [TaxPercentage]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [TaxAmount]      DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [UnitCost]       DECIMAL(18,2)  NOT NULL,
        [TotalCost]      DECIMAL(18,2)  NOT NULL,
        [CreatedAt]      DATETIME2      NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_PurchaseItems] PRIMARY KEY CLUSTERED ([Id] ASC),

        CONSTRAINT [FK_PurchaseItems_Purchases_PurchaseId]
            FOREIGN KEY ([PurchaseId])
            REFERENCES [dbo].[Purchases] ([Id])
            ON DELETE CASCADE,

        CONSTRAINT [FK_PurchaseItems_Products_ProductId]
            FOREIGN KEY ([ProductId])
            REFERENCES [dbo].[Products] ([Id])
            ON DELETE SET NULL
    );

    PRINT 'Table [PurchaseItems] created successfully with ProductId FK';
END
ELSE
BEGIN
    PRINT 'Table [PurchaseItems] already exists';

    -- STEP 2a: Add ProductId column if it doesn't exist on existing table
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'PurchaseItems' AND COLUMN_NAME = 'ProductId'
    )
    BEGIN
        ALTER TABLE [dbo].[PurchaseItems]
        ADD [ProductId] INT NULL;

        PRINT 'Column [ProductId] added to [PurchaseItems]';
    END
    ELSE
        PRINT 'Column [ProductId] already exists on [PurchaseItems]';

    -- STEP 2b: Add FK constraint if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
        WHERE CONSTRAINT_NAME = 'FK_PurchaseItems_Products_ProductId'
    )
    BEGIN
        ALTER TABLE [dbo].[PurchaseItems]
        ADD CONSTRAINT [FK_PurchaseItems_Products_ProductId]
            FOREIGN KEY ([ProductId])
            REFERENCES [dbo].[Products] ([Id])
            ON DELETE SET NULL;

        PRINT 'FK constraint [FK_PurchaseItems_Products_ProductId] added';
    END
    ELSE
        PRINT 'FK constraint [FK_PurchaseItems_Products_ProductId] already exists';

    -- STEP 2c: Add FK to Purchases if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
        WHERE CONSTRAINT_NAME = 'FK_PurchaseItems_Purchases_PurchaseId'
    )
    BEGIN
        ALTER TABLE [dbo].[PurchaseItems]
        ADD CONSTRAINT [FK_PurchaseItems_Purchases_PurchaseId]
            FOREIGN KEY ([PurchaseId])
            REFERENCES [dbo].[Purchases] ([Id])
            ON DELETE CASCADE;

        PRINT 'FK constraint [FK_PurchaseItems_Purchases_PurchaseId] added';
    END
    ELSE
        PRINT 'FK constraint [FK_PurchaseItems_Purchases_PurchaseId] already exists';
END

-- ========================================
-- STEP 3: Create indexes for performance
-- ========================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseItems_PurchaseId' AND object_id = OBJECT_ID('[dbo].[PurchaseItems]'))
BEGIN
    CREATE INDEX [IX_PurchaseItems_PurchaseId] ON [dbo].[PurchaseItems] ([PurchaseId]);
    PRINT 'Index [IX_PurchaseItems_PurchaseId] created';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseItems_ProductId' AND object_id = OBJECT_ID('[dbo].[PurchaseItems]'))
BEGIN
    CREATE INDEX [IX_PurchaseItems_ProductId] ON [dbo].[PurchaseItems] ([ProductId]);
    PRINT 'Index [IX_PurchaseItems_ProductId] created';
END

-- ========================================
-- STEP 4: Verification queries
-- ========================================
-- Uncomment to verify after running:

/*
-- Check PurchaseItems columns
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'PurchaseItems'
ORDER BY ORDINAL_POSITION;

-- Check FK constraints
SELECT 
    fk.name AS FK_Name,
    tp.name AS Parent_Table,
    cp.name AS Parent_Column,
    tr.name AS Referenced_Table,
    cr.name AS Referenced_Column
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tp.name = 'PurchaseItems';

-- Check Purchases columns
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Purchases'
ORDER BY ORDINAL_POSITION;
*/

PRINT '======================================';
PRINT 'Script completed successfully!';
PRINT 'PurchaseItems.ProductId -> Products.Id (FK, ON DELETE SET NULL)';
PRINT 'PurchaseItems.PurchaseId -> Purchases.Id (FK, ON DELETE CASCADE)';
PRINT '======================================';
