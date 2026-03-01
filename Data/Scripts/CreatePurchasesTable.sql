-- SQL Script to Create Purchases Table
-- Execute this script directly in your SQL Server database

CREATE TABLE [Purchases] (
    [Id] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
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
    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create unique index on Reference
CREATE UNIQUE INDEX [IX_Purchases_Reference] ON [Purchases] ([Reference]);

-- Create index for queries
CREATE INDEX [IX_Purchases_Date] ON [Purchases] ([Date]);
CREATE INDEX [IX_Purchases_PaymentStatus] ON [Purchases] ([PaymentStatus]);
