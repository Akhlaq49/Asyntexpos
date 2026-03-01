-- ============================================
-- ReactPOS Database Schema - SQL Server
-- Database-First Approach
-- ============================================

CREATE DATABASE ReactPosDb;
GO

USE ReactPosDb;
GO

-- ============================================
-- Lookup Tables
-- ============================================

CREATE TABLE Stores (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Value       NVARCHAR(100) NOT NULL,
    Label       NVARCHAR(200) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE Warehouses (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Value       NVARCHAR(100) NOT NULL,
    Label       NVARCHAR(200) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE Brands (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Value       NVARCHAR(100) NOT NULL,
    Label       NVARCHAR(200) NOT NULL,
    Image       NVARCHAR(500) NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'active',
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE VariantAttributes (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    [Values]    NVARCHAR(MAX) NOT NULL DEFAULT '',
    Status      NVARCHAR(20) NOT NULL DEFAULT 'active',
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE Warranties (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Duration    INT NOT NULL DEFAULT 0,
    Period      NVARCHAR(20) NOT NULL DEFAULT 'Month',
    Status      NVARCHAR(20) NOT NULL DEFAULT 'active',
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE StockEntries (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Warehouse   NVARCHAR(200) NOT NULL,
    Store       NVARCHAR(200) NOT NULL,
    ProductId   INT NOT NULL,
    Person      NVARCHAR(200) NOT NULL DEFAULT '',
    Quantity    INT NOT NULL DEFAULT 0,
    Date        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE StockAdjustments (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    Warehouse        NVARCHAR(200) NOT NULL,
    Store            NVARCHAR(200) NOT NULL,
    ProductId        INT NOT NULL,
    ReferenceNumber  NVARCHAR(100) NOT NULL DEFAULT '',
    Person           NVARCHAR(200) NOT NULL DEFAULT '',
    Quantity         INT NOT NULL DEFAULT 0,
    Notes            NVARCHAR(MAX) NULL,
    Date             DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE Units (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Value       NVARCHAR(50)  NOT NULL,
    Label       NVARCHAR(100) NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'active',
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- ============================================
-- Categories & Sub Categories
-- ============================================

CREATE TABLE Categories (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    Slug        NVARCHAR(200) NOT NULL,
    CreatedOn   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status      NVARCHAR(20) NOT NULL DEFAULT 'active'  -- active | inactive
);

CREATE TABLE SubCategories (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    SubCategoryName NVARCHAR(200) NOT NULL,
    CategoryId      INT NOT NULL,
    CategoryCode    NVARCHAR(100) NULL,
    Description     NVARCHAR(500) NULL,
    Image           NVARCHAR(500) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'active',  -- active | inactive
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_SubCategory_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);

-- ============================================
-- Products
-- ============================================

CREATE TABLE Products (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    Store               NVARCHAR(100) NULL,
    Warehouse           NVARCHAR(100) NULL,
    ProductName         NVARCHAR(300) NOT NULL,
    Slug                NVARCHAR(300) NULL,
    SKU                 NVARCHAR(100) NULL,
    SellingType         NVARCHAR(50)  NULL,
    Category            NVARCHAR(200) NULL,
    SubCategory         NVARCHAR(200) NULL,
    Brand               NVARCHAR(200) NULL,
    Unit                NVARCHAR(50)  NULL,
    BarcodeSymbology    NVARCHAR(50)  NULL,
    ItemBarcode         NVARCHAR(200) NULL,
    Description         NVARCHAR(MAX) NULL,
    ProductType         NVARCHAR(20)  NOT NULL DEFAULT 'single', -- single | variable
    Quantity            INT           NOT NULL DEFAULT 0,
    Price               DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxType             NVARCHAR(50)  NULL,
    Tax                 NVARCHAR(50)  NULL,
    DiscountType        NVARCHAR(50)  NULL,
    DiscountValue       DECIMAL(18,2) NOT NULL DEFAULT 0,
    QuantityAlert       INT           NOT NULL DEFAULT 0,
    Warranty            NVARCHAR(200) NULL,
    Manufacturer        NVARCHAR(200) NULL,
    ManufacturedDate    NVARCHAR(50)  NULL,
    ExpiryDate          NVARCHAR(50)  NULL,
    CreatedAt           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE ProductImages (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ProductId   INT NOT NULL,
    ImagePath   NVARCHAR(500) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProductImage_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);

-- ============================================
-- Customers
-- ============================================

CREATE TABLE Customers (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    Phone       NVARCHAR(50)  NULL,
    Email       NVARCHAR(200) NULL,
    Address     NVARCHAR(500) NULL,
    City        NVARCHAR(100) NULL,
    Status      NVARCHAR(20)  NOT NULL DEFAULT 'active',  -- active | inactive
    CreatedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);

-- ============================================
-- Installment Plans & Repayment Schedule
-- ============================================

CREATE TABLE InstallmentPlans (
    Id                      INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId              INT NOT NULL,
    ProductId               INT NOT NULL,
    ProductPrice            DECIMAL(18,2) NOT NULL,
    DownPayment             DECIMAL(18,2) NOT NULL DEFAULT 0,
    FinancedAmount          DECIMAL(18,2) NOT NULL,
    InterestRate            DECIMAL(8,4)  NOT NULL DEFAULT 0,   -- annual %
    Tenure                  INT           NOT NULL,              -- months
    EmiAmount               DECIMAL(18,2) NOT NULL,
    TotalPayable            DECIMAL(18,2) NOT NULL,
    TotalInterest           DECIMAL(18,2) NOT NULL DEFAULT 0,
    StartDate               NVARCHAR(20)  NOT NULL,
    Status                  NVARCHAR(20)  NOT NULL DEFAULT 'active', -- active | completed | defaulted | cancelled
    PaidInstallments        INT           NOT NULL DEFAULT 0,
    RemainingInstallments   INT           NOT NULL,
    NextDueDate             NVARCHAR(20)  NULL,
    CreatedAt               DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_InstallmentPlan_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
    CONSTRAINT FK_InstallmentPlan_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

CREATE TABLE RepaymentEntries (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    PlanId          INT NOT NULL,
    InstallmentNo   INT NOT NULL,
    DueDate         NVARCHAR(20) NOT NULL,
    EmiAmount       DECIMAL(18,2) NOT NULL,
    Principal       DECIMAL(18,2) NOT NULL,
    Interest        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Balance         DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'upcoming', -- paid | due | overdue | upcoming
    PaidDate        NVARCHAR(20) NULL,
    ActualPaidAmount DECIMAL(18,2) NULL, -- Actual amount paid (can be more than EmiAmount for overpayments)
    CONSTRAINT FK_Repayment_Plan FOREIGN KEY (PlanId) REFERENCES InstallmentPlans(Id) ON DELETE CASCADE
);

-- Miscellaneous Register for Customer Balance Tracking
CREATE TABLE MiscellaneousRegister (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId      INT NOT NULL,
    TransactionType NVARCHAR(100) NOT NULL, -- Credit, Debit, Adjustment
    Amount          DECIMAL(18,2) NOT NULL,
    Description     NVARCHAR(500) NOT NULL,
    ReferenceId     NVARCHAR(50) NULL, -- Link to installment plan or payment ID
    ReferenceType   NVARCHAR(50) NOT NULL, -- InstallmentPayment, ManualAdjustment, etc.
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       NVARCHAR(100) NULL,
    CONSTRAINT FK_MiscRegister_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
);

-- Sales (Online Orders)
CREATE TABLE Sales (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Reference       NVARCHAR(50) NOT NULL,
    CustomerId      INT NULL,
    CustomerName    NVARCHAR(100) NOT NULL DEFAULT '',
    CustomerImage   NVARCHAR(255) NULL,
    Biller          NVARCHAR(100) NOT NULL DEFAULT 'Admin',
    GrandTotal      DECIMAL(18,2) NOT NULL DEFAULT 0,
    Paid            DECIMAL(18,2) NOT NULL DEFAULT 0,
    Due             DECIMAL(18,2) NOT NULL DEFAULT 0,
    OrderTax        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Discount        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Shipping        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status          NVARCHAR(30) NOT NULL DEFAULT 'Pending',
    PaymentStatus   NVARCHAR(30) NOT NULL DEFAULT 'Unpaid',
    Notes           NVARCHAR(500) NULL,
    Source          NVARCHAR(20) NOT NULL DEFAULT 'online',
    SaleDate        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Sales_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE SET NULL
);

CREATE TABLE SaleItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    SaleId          INT NOT NULL,
    ProductId       INT NOT NULL,
    ProductName     NVARCHAR(200) NOT NULL DEFAULT '',
    Quantity        INT NOT NULL DEFAULT 1,
    PurchasePrice   DECIMAL(18,2) NOT NULL DEFAULT 0,
    Discount        DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxPercent      DECIMAL(8,4) NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18,2) NOT NULL DEFAULT 0,
    UnitCost        DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalCost       DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_SaleItems_Sales FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE
);

CREATE TABLE SalePayments (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    SaleId          INT NOT NULL,
    Reference       NVARCHAR(100) NOT NULL DEFAULT '',
    ReceivedAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    PayingAmount    DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaymentType     NVARCHAR(50) NOT NULL DEFAULT 'Cash',
    Description     NVARCHAR(500) NULL,
    PaymentDate     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_SalePayments_Sales FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE
);

-- ============================================
-- Seed Data - Lookup Tables
-- ============================================

INSERT INTO Stores (Value, Label) VALUES
('electro-mart', 'Electro Mart'),
('quantum-gadgets', 'Quantum Gadgets'),
('gadget-world', 'Gadget World'),
('volt-vault', 'Volt Vault'),
('elite-retail', 'Elite Retail'),
('prime-mart', 'Prime Mart'),
('neotech-store', 'NeoTech Store');

INSERT INTO Warehouses (Value, Label) VALUES
('lavish', 'Lavish Warehouse'),
('quaint', 'Quaint Warehouse'),
('traditional', 'Traditional Warehouse'),
('cool', 'Cool Warehouse'),
('overflow', 'Overflow Warehouse'),
('nova', 'Nova Storage Hub'),
('retail', 'Retail Supply Hub'),
('edgeware', 'EdgeWare Solutions');

INSERT INTO Brands (Value, Label) VALUES
('lenovo', 'Lenevo'),
('beats', 'Beats'),
('nike', 'Nike'),
('apple', 'Apple'),
('amazon', 'Amazon'),
('woodmart', 'Woodmart');

-- Invoices
CREATE TABLE Invoices (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceNo       NVARCHAR(50) NOT NULL,
    CustomerId      INT NULL,
    CustomerName    NVARCHAR(100) NOT NULL DEFAULT '',
    CustomerImage   NVARCHAR(255) NULL,
    CustomerAddress NVARCHAR(200) NULL,
    CustomerEmail   NVARCHAR(100) NULL,
    CustomerPhone   NVARCHAR(50) NULL,
    FromName        NVARCHAR(100) NOT NULL DEFAULT '',
    FromAddress     NVARCHAR(200) NULL,
    FromEmail       NVARCHAR(100) NULL,
    FromPhone       NVARCHAR(50) NULL,
    InvoiceFor      NVARCHAR(200) NULL,
    SubTotal        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Discount        DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountPercent DECIMAL(8,4) NOT NULL DEFAULT 0,
    Tax             DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxPercent      DECIMAL(8,4) NOT NULL DEFAULT 0,
    TotalAmount     DECIMAL(18,2) NOT NULL DEFAULT 0,
    Paid            DECIMAL(18,2) NOT NULL DEFAULT 0,
    AmountDue       DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status          NVARCHAR(30) NOT NULL DEFAULT 'Unpaid',
    Notes           NVARCHAR(500) NULL,
    Terms           NVARCHAR(500) NULL,
    DueDate         DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE SET NULL
);

CREATE TABLE InvoiceItems (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId   INT NOT NULL,
    Description NVARCHAR(200) NOT NULL DEFAULT '',
    Quantity    INT NOT NULL DEFAULT 1,
    Cost        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Discount    DECIMAL(18,2) NOT NULL DEFAULT 0,
    Total       DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE
);

INSERT INTO Units (Value, Label) VALUES
('kg', 'Kg'),
('pcs', 'Pcs'),
('l', 'L'),
('dz', 'dz'),
('bx', 'bx');

-- Seed Categories
INSERT INTO Categories (Name, Slug, Status) VALUES
('Computers', 'computers', 'active'),
('Electronics', 'electronics', 'active'),
('Shoe', 'shoe', 'active'),
('Cosmetics', 'cosmetics', 'active'),
('Groceries', 'groceries', 'active'),
('Furniture', 'furniture', 'active'),
('Bags', 'bags', 'active'),
('Phone', 'phone', 'active');

-- Seed Customers
INSERT INTO Customers (Name, Phone, Email, Address, City, Status) VALUES
('Ahmed Khan', '0300-1234567', 'ahmed@example.com', '123 Main St, Karachi', 'Karachi', 'active'),
('Sara Ali', '0321-9876543', 'sara@example.com', '45 Garden Ave, Lahore', 'Lahore', 'active'),
('Bilal Ahmed', '0333-5551234', 'bilal@example.com', '78 City Rd, Islamabad', 'Islamabad', 'active'),
('Fatima Noor', '0345-7778899', 'fatima@example.com', '12 Lake View, Faisalabad', 'Faisalabad', 'active'),
('Usman Tariq', '0312-4445566', 'usman@example.com', '90 Block B, Multan', 'Multan', 'active'),
('Ayesha Malik', '0301-2223344', 'ayesha@example.com', '56 Park Lane, Rawalpindi', 'Rawalpindi', 'active'),
('Hassan Raza', '0334-6667788', 'hassan@example.com', '33 Hill Top, Peshawar', 'Peshawar', 'active'),
('Zainab Hussain', '0315-8889900', 'zainab@example.com', '67 River View, Quetta', 'Quetta', 'active'),
('Ali Abbas', '0322-1112233', 'ali@example.com', '101 Market Rd, Sialkot', 'Sialkot', 'active'),
('Maria Sharif', '0346-3334455', 'maria@example.com', '22 Mall Rd, Gujranwala', 'Gujranwala', 'active');

GO
