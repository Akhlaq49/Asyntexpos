-- ============================================================
-- Sale/Order Unification Migration
-- Adds OrderNumber, BillingAddressId, ShippingAddressId to Sales table
-- so that ALL sales (POS + Online) are stored in ONE table
-- distinguished by the Source column ("pos" / "online")
-- ============================================================

-- 1. Add new columns to Sales table
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE Sales ADD OrderNumber NVARCHAR(50) NULL;
    PRINT 'Added OrderNumber column to Sales table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'BillingAddressId')
BEGIN
    ALTER TABLE Sales ADD BillingAddressId INT NULL;
    PRINT 'Added BillingAddressId column to Sales table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'ShippingAddressId')
BEGIN
    ALTER TABLE Sales ADD ShippingAddressId INT NULL;
    PRINT 'Added ShippingAddressId column to Sales table';
END
GO

-- 2. Add foreign key constraints for addresses
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Sales_BillingAddress')
BEGIN
    ALTER TABLE Sales ADD CONSTRAINT FK_Sales_BillingAddress
        FOREIGN KEY (BillingAddressId) REFERENCES PartyAddresses(Id)
        ON DELETE SET NULL;
    PRINT 'Added FK_Sales_BillingAddress';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Sales_ShippingAddress')
BEGIN
    ALTER TABLE Sales ADD CONSTRAINT FK_Sales_ShippingAddress
        FOREIGN KEY (ShippingAddressId) REFERENCES PartyAddresses(Id)
        ON DELETE NO ACTION;
    PRINT 'Added FK_Sales_ShippingAddress';
END
GO

-- 3. Migrate existing Orders into Sales (if any exist)
-- This copies data from the old Orders table into the unified Sales table
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Orders')
   AND EXISTS (SELECT 1 FROM Orders)
BEGIN
    PRINT 'Migrating existing Orders to Sales table...';

    -- Get the next SL reference number
    DECLARE @nextNum INT;
    SELECT @nextNum = ISNULL(MAX(CAST(SUBSTRING(Reference, 3, LEN(Reference)) AS INT)), 0) + 1
    FROM Sales WHERE Reference LIKE 'SL%';

    -- Insert each order as a sale
    DECLARE @orderId INT, @orderNumber NVARCHAR(50), @custId INT, @custName NVARCHAR(100),
            @payType NVARCHAR(50), @amount DECIMAL(18,2), @status NVARCHAR(30),
            @orderDate DATETIME, @createdAt DATETIME, @newSaleId INT,
            @billingAddrId INT, @shippingAddrId INT;

    DECLARE order_cursor CURSOR FOR
        SELECT o.Id, o.OrderNumber, o.CustomerId, o.CustomerName, o.PaymentType,
               o.Amount, o.Status, o.OrderDate, o.CreatedAt
        FROM Orders o
        WHERE NOT EXISTS (SELECT 1 FROM Sales s WHERE s.OrderNumber = o.OrderNumber);

    OPEN order_cursor;
    FETCH NEXT FROM order_cursor INTO @orderId, @orderNumber, @custId, @custName,
        @payType, @amount, @status, @orderDate, @createdAt;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Get addresses from OnlineOrderDetails if they exist
        SET @billingAddrId = NULL;
        SET @shippingAddrId = NULL;
        SELECT @billingAddrId = BillingAddressId, @shippingAddrId = ShippingAddressId
        FROM OnlineOrderDetails WHERE OrderId = @orderId;

        DECLARE @ref NVARCHAR(50) = 'SL' + RIGHT('000' + CAST(@nextNum AS NVARCHAR), 3);

        INSERT INTO Sales (Reference, OrderNumber, CustomerId, CustomerName,
            Biller, Source, GrandTotal, Paid, Due, OrderTax, Discount, Shipping,
            Status, PaymentStatus, Notes, BillingAddressId, ShippingAddressId,
            SaleDate, CreatedAt)
        VALUES (@ref, @orderNumber, @custId, @custName,
            'Online Store', 'online', @amount, 0, @amount, 0, 0, 0,
            @status, 'Unpaid', NULL, @billingAddrId, @shippingAddrId,
            @orderDate, @createdAt);

        SET @newSaleId = SCOPE_IDENTITY();

        -- Migrate order items to sale items
        INSERT INTO SaleItems (SaleId, ProductId, ProductName, Quantity,
            PurchasePrice, Discount, TaxPercent, TaxAmount, UnitCost, TotalCost)
        SELECT @newSaleId, oi.ProductId, oi.ProductName, oi.Quantity,
            oi.Price, 0, 0, 0, oi.Price, oi.Price * oi.Quantity
        FROM OrderItems oi WHERE oi.OrderId = @orderId;

        SET @nextNum = @nextNum + 1;

        FETCH NEXT FROM order_cursor INTO @orderId, @orderNumber, @custId, @custName,
            @payType, @amount, @status, @orderDate, @createdAt;
    END

    CLOSE order_cursor;
    DEALLOCATE order_cursor;

    PRINT 'Migration complete. Existing orders have been copied to Sales table.';
END
GO

PRINT '=== Sale/Order Unification migration complete ===';
PRINT 'All new POS sales: Source = "pos"';
PRINT 'All new Online sales: Source = "online"';
GO

-- 4. Drop the old tables (no longer used)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OnlineOrderDetails')
BEGIN
    DROP TABLE OnlineOrderDetails;
    PRINT 'Dropped table: OnlineOrderDetails';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OrderItems')
BEGIN
    DROP TABLE OrderItems;
    PRINT 'Dropped table: OrderItems';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Orders')
BEGIN
    DROP TABLE Orders;
    PRINT 'Dropped table: Orders';
END
GO

PRINT '=== Cleanup complete. Old Order tables removed. ===';
GO
