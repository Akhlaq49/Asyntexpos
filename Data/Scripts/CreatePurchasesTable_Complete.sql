-- SQL Script: Create Purchases Table
-- Description: Creates the Purchases table for managing purchase orders
-- Date: 2026-02-27
-- Execute this script in SQL Server Management Studio against your database

-- ========================================
-- 1. CREATE PURCHASES TABLE
-- ========================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Purchases')
BEGIN
    CREATE TABLE [dbo].[Purchases] (
        [Id] INT NOT NULL IDENTITY(1,1),
        [SupplierName] NVARCHAR(100) NOT NULL,
        [SupplierRef] NVARCHAR(50),
        [Reference] NVARCHAR(50) NOT NULL,
        [Date] DATETIME2 NOT NULL,
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Received',
        [PaymentStatus] NVARCHAR(30) NOT NULL DEFAULT 'Unpaid',
        [Total] DECIMAL(18,2) NOT NULL,
        [Paid] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Notes] NVARCHAR(500),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        
        CONSTRAINT [PK_Purchases] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    
    PRINT 'Table [Purchases] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [Purchases] already exists';
END

-- ========================================
-- 2. CREATE INDEXES
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Purchases_Reference' AND object_id = OBJECT_ID('[dbo].[Purchases]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Purchases_Reference] ON [dbo].[Purchases] ([Reference] ASC);
    PRINT 'Index [IX_Purchases_Reference] created successfully';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Purchases_Date' AND object_id = OBJECT_ID('[dbo].[Purchases]'))
BEGIN
    CREATE INDEX [IX_Purchases_Date] ON [dbo].[Purchases] ([Date] ASC);
    PRINT 'Index [IX_Purchases_Date] created successfully';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Purchases_PaymentStatus' AND object_id = OBJECT_ID('[dbo].[Purchases]'))
BEGIN
    CREATE INDEX [IX_Purchases_PaymentStatus] ON [dbo].[Purchases] ([PaymentStatus] ASC);
    PRINT 'Index [IX_Purchases_PaymentStatus] created successfully';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Purchases_SupplierName' AND object_id = OBJECT_ID('[dbo].[Purchases]'))
BEGIN
    CREATE INDEX [IX_Purchases_SupplierName] ON [dbo].[Purchases] ([SupplierName] ASC);
    PRINT 'Index [IX_Purchases_SupplierName] created successfully';
END

-- ========================================
-- 3. INSERT SAMPLE DATA (Optional)
-- ========================================
-- Uncomment the lines below to insert sample data for testing

/*
INSERT INTO [dbo].[Purchases] (
    [SupplierName],
    [SupplierRef],
    [Reference],
    [Date],
    [Status],
    [PaymentStatus],
    [Total],
    [Paid],
    [Notes],
    [CreatedAt],
    [UpdatedAt]
) VALUES
    ('Electro Mart', 'EM001', 'PT001', '2024-12-24', 'Received', 'Paid', 1000.00, 1000.00, 'Delivered on time', GETUTCDATE(), GETUTCDATE()),
    ('Quantum Gadgets', 'QG001', 'PT002', '2024-12-10', 'Pending', 'Unpaid', 1500.00, 0.00, 'Awaiting delivery', GETUTCDATE(), GETUTCDATE()),
    ('Prime Bazaar', 'PB001', 'PT003', '2024-11-27', 'Received', 'Paid', 1500.00, 1800.00, 'Overpaid', GETUTCDATE(), GETUTCDATE()),
    ('Gadget World', 'GW001', 'PT004', '2024-11-18', 'Ordered', 'Overdue', 2000.00, 1000.00, 'Payment due', GETUTCDATE(), GETUTCDATE()),
    ('Volt Vault', 'VV001', 'PT005', '2024-11-06', 'Received', 'Paid', 800.00, 800.00, 'Complete delivery', GETUTCDATE(), GETUTCDATE());

PRINT 'Sample data inserted successfully';
*/

-- ========================================
-- 4. VERIFICATION
-- ========================================
-- Run this to verify the table structure
-- SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Purchases]');
-- SELECT * FROM [dbo].[Purchases];

PRINT 'Script execution completed successfully!';
