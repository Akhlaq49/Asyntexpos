-- ============================================
-- Manufacturing Management System Tables
-- ============================================

-- Bill of Materials (BOM) - defines recipes for finished products
CREATE TABLE BillOfMaterials (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TenantId        INT NOT NULL DEFAULT 0,
    Name            NVARCHAR(200) NOT NULL,
    FinishedProductId INT NOT NULL,       -- FK to Products (the output product)
    OutputQuantity  INT NOT NULL DEFAULT 1,
    LaborCost       DECIMAL(18,2) NOT NULL DEFAULT 0,
    OverheadCost    DECIMAL(18,2) NOT NULL DEFAULT 0,
    Notes           NVARCHAR(500) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'active',  -- active | inactive
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- BOM Line Items (raw materials needed)
CREATE TABLE BomItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TenantId        INT NOT NULL DEFAULT 0,
    BomId           INT NOT NULL,
    RawMaterialId   INT NOT NULL,        -- FK to Products (raw material)
    Quantity        DECIMAL(18,4) NOT NULL DEFAULT 1,
    UnitCost        DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_BomItem_Bom FOREIGN KEY (BomId) REFERENCES BillOfMaterials(Id) ON DELETE CASCADE
);

-- Manufacturing Orders (production runs)
CREATE TABLE ManufacturingOrders (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TenantId        INT NOT NULL DEFAULT 0,
    Reference       NVARCHAR(50) NOT NULL,
    BomId           INT NOT NULL,
    FinishedProductId INT NOT NULL,
    Quantity        INT NOT NULL DEFAULT 1,        -- how many units to produce
    TargetStoreId   INT NULL,                      -- finished goods destination store
    Status          NVARCHAR(30) NOT NULL DEFAULT 'Draft',  -- Draft | InProgress | Completed | Cancelled
    LaborCost       DECIMAL(18,2) NOT NULL DEFAULT 0,
    OverheadCost    DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalMaterialCost DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalCost       DECIMAL(18,2) NOT NULL DEFAULT 0,
    StartDate       DATETIME2 NULL,
    CompletionDate  DATETIME2 NULL,
    Notes           NVARCHAR(500) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Manufacturing Order consumed items (actual material consumption)
CREATE TABLE ManufacturingOrderItems (
    Id                      INT IDENTITY(1,1) PRIMARY KEY,
    TenantId                INT NOT NULL DEFAULT 0,
    ManufacturingOrderId    INT NOT NULL,
    RawMaterialId           INT NOT NULL,
    RequiredQuantity        DECIMAL(18,4) NOT NULL DEFAULT 0,
    ConsumedQuantity        DECIMAL(18,4) NOT NULL DEFAULT 0,
    UnitCost                DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalCost               DECIMAL(18,2) NOT NULL DEFAULT 0,
    SupplierId              INT NULL,           -- FK to Parties (Supplier)
    CONSTRAINT FK_MOItem_MO FOREIGN KEY (ManufacturingOrderId) REFERENCES ManufacturingOrders(Id) ON DELETE CASCADE
);

-- Supplier Ledger (payment tracking per supplier)
CREATE TABLE SupplierLedgerEntries (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TenantId        INT NOT NULL DEFAULT 0,
    SupplierId      INT NOT NULL,          -- FK to Parties (Supplier)
    TransactionType NVARCHAR(50) NOT NULL,  -- Purchase | Payment | Debit | Credit | Adjustment
    ReferenceType   NVARCHAR(50) NOT NULL,  -- Purchase | ManufacturingOrder | ManualPayment | Adjustment
    ReferenceId     INT NULL,               -- FK to source record
    Amount          DECIMAL(18,2) NOT NULL DEFAULT 0,
    RunningBalance  DECIMAL(18,2) NOT NULL DEFAULT 0,
    Description     NVARCHAR(500) NULL,
    Date            DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Supplier Payments
CREATE TABLE SupplierPayments (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TenantId        INT NOT NULL DEFAULT 0,
    SupplierId      INT NOT NULL,
    Reference       NVARCHAR(100) NOT NULL DEFAULT '',
    Amount          DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaymentMethod   NVARCHAR(50) NOT NULL DEFAULT 'Cash',
    Description     NVARCHAR(500) NULL,
    PaymentDate     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
