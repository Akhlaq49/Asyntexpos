# SQL Scripts for Purchases Table

This folder contains SQL scripts for managing the Purchases table in the ReactPOS database.

## Files

### 1. **CreatePurchasesTable_Complete.sql** (Main Script)
Complete script to create the Purchases table with all necessary columns and indexes.

**Features:**
- Creates the Purchases table if it doesn't exist
- Automatically creates required indexes:
  - `IX_Purchases_Reference` (Unique index for reference)
  - `IX_Purchases_Date` (For date-based queries)
  - `IX_Purchases_PaymentStatus` (For payment status filtering)
  - `IX_Purchases_SupplierName` (For supplier searches)
- Includes optional sample data (commented out - uncomment if needed)
- Includes verification queries

**How to Execute:**

1. Open **SQL Server Management Studio (SSMS)**
2. Connect to your ReactPOS database
3. Open the script file
4. Click **Execute** or press **F5**

### 2. **CreatePurchasesTable.sql** (Basic Script)
Minimal version with just the table and essential indexes.

### 3. **DropPurchasesTable.sql** (Cleanup Script)
Use this to drop the Purchases table (for development/testing only).

**⚠️ Warning:** This will delete all data in the Purchases table. Back up your data first!

## Table Structure

```sql
Purchases (
    Id INT (Primary Key, Auto-increment)
    SupplierName NVARCHAR(100) - Supplier name
    SupplierRef NVARCHAR(50) - Supplier reference number
    Reference NVARCHAR(50) - Purchase reference/order number (Unique)
    Date DATETIME2 - Purchase date
    Status NVARCHAR(30) - Purchase status (Received, Pending, Ordered, Cancelled)
    PaymentStatus NVARCHAR(30) - Payment status (Paid, Unpaid, Partial, Overdue)
    Total DECIMAL(18,2) - Total purchase amount
    Paid DECIMAL(18,2) - Amount paid
    Notes NVARCHAR(500) - Additional notes
    CreatedAt DATETIME2 - Creation timestamp
    UpdatedAt DATETIME2 - Last update timestamp
)
```

## Indexes

| Index Name | Columns | Type | Purpose |
|-----------|---------|------|---------|
| PK_Purchases | Id | Primary Key | Unique identifier |
| IX_Purchases_Reference | Reference | Unique | Ensure unique references |
| IX_Purchases_Date | Date | Regular | Optimize date queries |
| IX_Purchases_PaymentStatus | PaymentStatus | Regular | Optimize status filters |
| IX_Purchases_SupplierName | SupplierName | Regular | Optimize supplier searches |

## Sample Data

The script includes optional sample data. To insert it:

1. Uncomment the section labeled "INSERT SAMPLE DATA"
2. Run the script
3. The sample will include 5 sample purchases from different suppliers

## Verification

After running the script, verify the table was created:

```sql
-- View table structure
SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Purchases]');

-- View all purchases
SELECT * FROM [dbo].[Purchases];

-- View indexes
SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('[dbo].[Purchases]');
```

## Important Notes

- ✅ The script is idempotent - it can be run multiple times safely
- ✅ Existing data will not be affected if the table already exists
- ✅ All timestamps default to UTC (GETUTCDATE())
- ⚠️ Always test in a development environment first
- 💾 Back up your database before running cleanup scripts

## Integration with API

The API endpoints are already configured to work with this table:

- `GET /api/purchases` - Get all purchases
- `GET /api/purchases/{id}` - Get single purchase
- `POST /api/purchases` - Create new purchase
- `PUT /api/purchases/{id}` - Update purchase
- `DELETE /api/purchases/{id}` - Delete purchase

## Troubleshooting

**Error: "There is already an object named 'Purchases'"**
- The table already exists. Use the complete script which checks for existence.

**Error: "Cannot insert duplicate key in unique index"**
- A reference number already exists. Use a unique reference.

**Error: "Login failed for user"**
- Verify your SQL Server credentials and database access permissions.

## Support

For issues or questions about these scripts, check the ReactPOS API documentation or contact support.
