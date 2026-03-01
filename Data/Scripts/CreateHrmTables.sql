-- =========================================================
-- CreateHrmTables.sql
-- Creates all HRM-related tables: Departments, Designations,
-- Shifts, LeaveTypes, Leaves, Holidays, Payrolls.
-- Also adds employee-specific columns to Parties table.
-- Idempotent – safe to run multiple times.
-- =========================================================

-- ─── 1. Departments ───
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Departments') AND type = 'U')
BEGIN
    CREATE TABLE [Departments] (
        [Id]          INT IDENTITY(1,1) PRIMARY KEY,
        [Name]        NVARCHAR(200) NOT NULL,
        [HODId]       INT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Status]      NVARCHAR(50) NOT NULL DEFAULT 'active',
        [IsActive]    BIT NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- ─── 2. Designations ───
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Designations') AND type = 'U')
BEGIN
    CREATE TABLE [Designations] (
        [Id]           INT IDENTITY(1,1) PRIMARY KEY,
        [Name]         NVARCHAR(200) NOT NULL,
        [DepartmentId] INT NULL,
        [Description]  NVARCHAR(MAX) NULL,
        [Status]       NVARCHAR(50) NOT NULL DEFAULT 'active',
        [IsActive]     BIT NOT NULL DEFAULT 1,
        [CreatedAt]    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- ─── 3. Shifts ───
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Shifts') AND type = 'U')
BEGIN
    CREATE TABLE [Shifts] (
        [Id]        INT IDENTITY(1,1) PRIMARY KEY,
        [Name]      NVARCHAR(200) NOT NULL,
        [StartTime] NVARCHAR(20) NULL,
        [EndTime]   NVARCHAR(20) NULL,
        [WeekOff]   NVARCHAR(50) NULL,
        [Status]    NVARCHAR(50) NOT NULL DEFAULT 'active',
        [IsActive]  BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- ─── 4. LeaveTypes ───
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('LeaveTypes') AND type = 'U')
BEGIN
    CREATE TABLE [LeaveTypes] (
        [Id]        INT IDENTITY(1,1) PRIMARY KEY,
        [Name]      NVARCHAR(200) NOT NULL,
        [Quota]     INT NOT NULL DEFAULT 12,
        [Status]    NVARCHAR(50) NOT NULL DEFAULT 'active',
        [IsActive]  BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- ─── 5. Leaves ───
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Leaves') AND type = 'U')
BEGIN
    CREATE TABLE [Leaves] (
        [Id]           INT IDENTITY(1,1) PRIMARY KEY,
        [EmployeeId]   INT NOT NULL,
        [LeaveTypeId]  INT NOT NULL,
        [FromDate]     DATETIME2 NOT NULL,
        [ToDate]       DATETIME2 NOT NULL,
        [Days]         DECIMAL(5,1) NOT NULL DEFAULT 1,
        [DayType]      NVARCHAR(50) NOT NULL DEFAULT 'Full Day',
        [Reason]       NVARCHAR(MAX) NULL,
        [Status]       NVARCHAR(50) NOT NULL DEFAULT 'New',
        [ApprovedById] INT NULL,
        [CreatedAt]    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- ─── 6. Holidays ───
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Holidays') AND type = 'U')
BEGIN
    CREATE TABLE [Holidays] (
        [Id]          INT IDENTITY(1,1) PRIMARY KEY,
        [Title]       NVARCHAR(200) NOT NULL,
        [FromDate]    DATETIME2 NOT NULL,
        [ToDate]      DATETIME2 NOT NULL,
        [Days]        INT NOT NULL DEFAULT 1,
        [Description] NVARCHAR(MAX) NULL,
        [Status]      NVARCHAR(50) NOT NULL DEFAULT 'active',
        [IsActive]    BIT NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- ─── 7. Payrolls ───
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Payrolls') AND type = 'U')
BEGIN
    CREATE TABLE [Payrolls] (
        [Id]               INT IDENTITY(1,1) PRIMARY KEY,
        [EmployeeId]       INT NOT NULL,
        [BasicSalary]      DECIMAL(18,2) NOT NULL DEFAULT 0,
        [HRA]              DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Conveyance]       DECIMAL(18,2) NOT NULL DEFAULT 0,
        [MedicalAllowance] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Bonus]            DECIMAL(18,2) NOT NULL DEFAULT 0,
        [OtherAllowance]   DECIMAL(18,2) NOT NULL DEFAULT 0,
        [PF]               DECIMAL(18,2) NOT NULL DEFAULT 0,
        [ProfessionalTax]  DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TDS]              DECIMAL(18,2) NOT NULL DEFAULT 0,
        [LoanDeduction]    DECIMAL(18,2) NOT NULL DEFAULT 0,
        [OtherDeduction]   DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TotalAllowance]   DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TotalDeduction]   DECIMAL(18,2) NOT NULL DEFAULT 0,
        [NetSalary]        DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Status]           NVARCHAR(50) NOT NULL DEFAULT 'Unpaid',
        [Month]            INT NULL,
        [Year]             INT NULL,
        [CreatedAt]        DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- ─── 8. Add Employee-specific columns to Parties ───

-- DepartmentId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'DepartmentId')
BEGIN
    ALTER TABLE [Parties] ADD [DepartmentId] INT NULL;
END
GO

-- DesignationId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'DesignationId')
BEGIN
    ALTER TABLE [Parties] ADD [DesignationId] INT NULL;
END
GO

-- ShiftId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'ShiftId')
BEGIN
    ALTER TABLE [Parties] ADD [ShiftId] INT NULL;
END
GO

-- DateOfJoining
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'DateOfJoining')
BEGIN
    ALTER TABLE [Parties] ADD [DateOfJoining] DATETIME2 NULL;
END
GO

-- BasicSalary
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'BasicSalary')
BEGIN
    ALTER TABLE [Parties] ADD [BasicSalary] DECIMAL(18,2) NULL;
END
GO

-- EmployeeId (display ID like EMP001)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Parties') AND name = 'EmployeeId')
BEGIN
    ALTER TABLE [Parties] ADD [EmployeeId] NVARCHAR(50) NULL;
END
GO

-- ─── 9. Foreign Keys ───

-- Departments.HODId → Parties.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Departments_HOD_Parties')
BEGIN
    ALTER TABLE [Departments]
    ADD CONSTRAINT [FK_Departments_HOD_Parties]
    FOREIGN KEY ([HODId]) REFERENCES [Parties]([Id]) ON DELETE SET NULL;
END
GO

-- Designations.DepartmentId → Departments.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Designations_Department')
BEGIN
    ALTER TABLE [Designations]
    ADD CONSTRAINT [FK_Designations_Department]
    FOREIGN KEY ([DepartmentId]) REFERENCES [Departments]([Id]) ON DELETE SET NULL;
END
GO

-- Leaves.EmployeeId → Parties.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leaves_Employee_Parties')
BEGIN
    ALTER TABLE [Leaves]
    ADD CONSTRAINT [FK_Leaves_Employee_Parties]
    FOREIGN KEY ([EmployeeId]) REFERENCES [Parties]([Id]) ON DELETE NO ACTION;
END
GO

-- Leaves.LeaveTypeId → LeaveTypes.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leaves_LeaveType')
BEGIN
    ALTER TABLE [Leaves]
    ADD CONSTRAINT [FK_Leaves_LeaveType]
    FOREIGN KEY ([LeaveTypeId]) REFERENCES [LeaveTypes]([Id]) ON DELETE NO ACTION;
END
GO

-- Leaves.ApprovedById → Parties.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leaves_ApprovedBy_Parties')
BEGIN
    ALTER TABLE [Leaves]
    ADD CONSTRAINT [FK_Leaves_ApprovedBy_Parties]
    FOREIGN KEY ([ApprovedById]) REFERENCES [Parties]([Id]) ON DELETE SET NULL;
END
GO

-- Payrolls.EmployeeId → Parties.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Payrolls_Employee_Parties')
BEGIN
    ALTER TABLE [Payrolls]
    ADD CONSTRAINT [FK_Payrolls_Employee_Parties]
    FOREIGN KEY ([EmployeeId]) REFERENCES [Parties]([Id]) ON DELETE NO ACTION;
END
GO

-- Parties.DepartmentId → Departments.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Parties_Department')
BEGIN
    ALTER TABLE [Parties]
    ADD CONSTRAINT [FK_Parties_Department]
    FOREIGN KEY ([DepartmentId]) REFERENCES [Departments]([Id]) ON DELETE SET NULL;
END
GO

-- Parties.DesignationId → Designations.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Parties_Designation')
BEGIN
    ALTER TABLE [Parties]
    ADD CONSTRAINT [FK_Parties_Designation]
    FOREIGN KEY ([DesignationId]) REFERENCES [Designations]([Id]) ON DELETE SET NULL;
END
GO

-- Parties.ShiftId → Shifts.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Parties_Shift')
BEGIN
    ALTER TABLE [Parties]
    ADD CONSTRAINT [FK_Parties_Shift]
    FOREIGN KEY ([ShiftId]) REFERENCES [Shifts]([Id]) ON DELETE SET NULL;
END
GO

-- ═══════════════════════════════════════════════════════════
-- ATTENDANCES TABLE
-- ═══════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Attendances')
BEGIN
    CREATE TABLE [Attendances] (
        [Id]          INT IDENTITY(1,1) PRIMARY KEY,
        [EmployeeId]  INT NOT NULL,
        [Date]        DATE NOT NULL,
        [Status]      NVARCHAR(20) NOT NULL DEFAULT 'Present',
        [ClockIn]     NVARCHAR(MAX) NULL,
        [ClockOut]    NVARCHAR(MAX) NULL,
        [Production]  NVARCHAR(MAX) NULL,
        [BreakTime]   NVARCHAR(MAX) NULL,
        [Overtime]    NVARCHAR(MAX) NULL,
        [TotalHours]  NVARCHAR(MAX) NULL,
        [CreatedAt]   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Created Attendances table.';
END
GO

-- FK: Attendances.EmployeeId → Parties.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Attendances_Employee')
BEGIN
    ALTER TABLE [Attendances]
    ADD CONSTRAINT [FK_Attendances_Employee]
    FOREIGN KEY ([EmployeeId]) REFERENCES [Parties]([Id]) ON DELETE NO ACTION;
END
GO

-- Unique index: one attendance per employee per day
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendances_EmployeeId_Date')
BEGIN
    CREATE UNIQUE INDEX [IX_Attendances_EmployeeId_Date]
    ON [Attendances] ([EmployeeId], [Date]);
END
GO

PRINT 'HRM tables and foreign keys created successfully.';
GO
