-- ============================================================
-- Finance & Accounting Module – SQL Migration Script
-- Run against your ReactPosApi database
-- ============================================================

-- 1. Expense Categories
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ExpenseCategories')
BEGIN
    CREATE TABLE [dbo].[ExpenseCategories] (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [Name]        NVARCHAR(200)  NOT NULL,
        [Description] NVARCHAR(500)  NOT NULL DEFAULT '',
        [Status]      NVARCHAR(20)   NOT NULL DEFAULT 'active',
        [CreatedOn]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_ExpenseCategories] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT 'Created table: ExpenseCategories';
END
GO

-- 2. Expenses
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Expenses')
BEGIN
    CREATE TABLE [dbo].[Expenses] (
        [Id]                INT            IDENTITY(1,1) NOT NULL,
        [Reference]         NVARCHAR(50)   NOT NULL,
        [ExpenseName]       NVARCHAR(200)  NOT NULL,
        [ExpenseCategoryId] INT            NOT NULL,
        [Description]       NVARCHAR(500)  NOT NULL DEFAULT '',
        [Date]              DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        [Amount]            DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [Status]            NVARCHAR(20)   NOT NULL DEFAULT 'active',
        [CreatedOn]         DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_Expenses] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Expenses_ExpenseCategories] FOREIGN KEY ([ExpenseCategoryId])
            REFERENCES [dbo].[ExpenseCategories]([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: Expenses';
END
GO

-- 3. Income Categories
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'IncomeCategories')
BEGIN
    CREATE TABLE [dbo].[IncomeCategories] (
        [Id]        INT            IDENTITY(1,1) NOT NULL,
        [Code]      NVARCHAR(50)   NOT NULL,
        [Name]      NVARCHAR(200)  NOT NULL,
        [CreatedOn] DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_IncomeCategories] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT 'Created table: IncomeCategories';
END
GO

-- 4. Finance Incomes
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FinanceIncomes')
BEGIN
    CREATE TABLE [dbo].[FinanceIncomes] (
        [Id]               INT            IDENTITY(1,1) NOT NULL,
        [Date]             DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        [Reference]        NVARCHAR(50)   NOT NULL,
        [Store]            NVARCHAR(200)  NOT NULL DEFAULT '',
        [IncomeCategoryId] INT            NOT NULL,
        [Notes]            NVARCHAR(1000) NOT NULL DEFAULT '',
        [Amount]           DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [Account]          NVARCHAR(200)  NOT NULL DEFAULT '',
        [CreatedOn]        DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_FinanceIncomes] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_FinanceIncomes_IncomeCategories] FOREIGN KEY ([IncomeCategoryId])
            REFERENCES [dbo].[IncomeCategories]([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: FinanceIncomes';
END
GO

-- 5. Account Types
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AccountTypes')
BEGIN
    CREATE TABLE [dbo].[AccountTypes] (
        [Id]        INT            IDENTITY(1,1) NOT NULL,
        [Name]      NVARCHAR(200)  NOT NULL,
        [Status]    NVARCHAR(20)   NOT NULL DEFAULT 'active',
        [CreatedOn] DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_AccountTypes] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT 'Created table: AccountTypes';
END
GO

-- 6. Bank Accounts
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BankAccounts')
BEGIN
    CREATE TABLE [dbo].[BankAccounts] (
        [Id]             INT            IDENTITY(1,1) NOT NULL,
        [HolderName]     NVARCHAR(200)  NOT NULL,
        [AccountNumber]  NVARCHAR(50)   NOT NULL,
        [BankName]       NVARCHAR(200)  NOT NULL,
        [Branch]         NVARCHAR(200)  NOT NULL DEFAULT '',
        [IFSC]           NVARCHAR(50)   NOT NULL DEFAULT '',
        [AccountTypeId]  INT            NOT NULL,
        [OpeningBalance] DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [Notes]          NVARCHAR(500)  NOT NULL DEFAULT '',
        [Status]         NVARCHAR(20)   NOT NULL DEFAULT 'active',
        [IsDefault]      BIT            NOT NULL DEFAULT 0,
        [CreatedOn]      DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_BankAccounts] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BankAccounts_AccountTypes] FOREIGN KEY ([AccountTypeId])
            REFERENCES [dbo].[AccountTypes]([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: BankAccounts';
END
GO

-- ============================================================
-- Optional: Seed default Account Types
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[AccountTypes])
BEGIN
    INSERT INTO [dbo].[AccountTypes] ([Name], [Status]) VALUES
        ('Savings',  'active'),
        ('Current',  'active'),
        ('Fixed Deposit', 'active');
    PRINT 'Seeded default Account Types';
END
GO

-- ============================================================
-- Optional: Seed default Expense Categories
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[ExpenseCategories])
BEGIN
    INSERT INTO [dbo].[ExpenseCategories] ([Name], [Description], [Status]) VALUES
        ('Rent',        'Office / store rent',            'active'),
        ('Utilities',   'Electricity, water, internet',   'active'),
        ('Salaries',    'Employee salaries and wages',    'active'),
        ('Supplies',    'Office and store supplies',      'active'),
        ('Marketing',   'Advertising and promotions',     'active'),
        ('Maintenance', 'Equipment and facility repairs', 'active');
    PRINT 'Seeded default Expense Categories';
END
GO

-- ============================================================
-- Optional: Seed default Income Categories
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[IncomeCategories])
BEGIN
    INSERT INTO [dbo].[IncomeCategories] ([Code], [Name]) VALUES
        ('IC-000001', 'Product Sales'),
        ('IC-000002', 'Service Revenue'),
        ('IC-000003', 'Interest Income'),
        ('IC-000004', 'Rental Income');
    PRINT 'Seeded default Income Categories';
END
GO

PRINT '============================';
PRINT 'Finance tables migration complete';
PRINT '============================';
