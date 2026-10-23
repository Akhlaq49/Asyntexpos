-- Purchases used to be copied into Expenses (category "Purchase / Inventory"):
--   * "Inventory - <product> (N units)"  -> opening stock entered on Create Product (no purchase record existed)
--   * "Purchase - <supplier>"            -> duplicate of a real purchase from the Purchases screen
-- The app now records opening stock as an "Opening Stock" purchase and no longer copies purchases into
-- Expenses. Run this ONCE per database to fix the old rows. Product stock quantities are not changed.

START TRANSACTION;

-- 1. Turn each opening-stock expense into an "Opening Stock" purchase
INSERT INTO Purchases (TenantId, SupplierName, SupplierRef, Reference, Date, Status, PaymentStatus,
                       OrderTax, Discount, Shipping, Total, Paid, Notes, CreatedAt, UpdatedAt)
SELECT e.TenantId, 'Opening Stock', NULL,
       LEFT(TRIM(REPLACE(IFNULL(e.Description, ''), 'Ref:', '')), 50),
       e.Date, 'Received', 'Paid', 0, 0, 0, e.Amount, e.Amount,
       CONCAT('Opening stock (moved from expense #', e.Id, ')'), UTC_TIMESTAMP(), UTC_TIMESTAMP()
FROM Expenses e
JOIN ExpenseCategories c ON c.Id = e.ExpenseCategoryId
WHERE c.Name = 'Purchase / Inventory' AND e.ExpenseName LIKE 'Inventory - %';

-- 2. Add a line item linked to the product (matched by SKU = the expense's "Ref:")
INSERT INTO PurchaseItems (TenantId, PurchaseId, ProductId, ProductName, Quantity, PurchasePrice,
                           Discount, TaxPercentage, TaxAmount, UnitCost, TotalCost, CreatedAt)
SELECT p.TenantId, p.Id, pr.Id, LEFT(pr.ProductName, 100), pr.Quantity,
       IF(pr.Quantity > 0, p.Total / pr.Quantity, p.Total), 0, 0, 0,
       IF(pr.Quantity > 0, p.Total / pr.Quantity, p.Total), p.Total, UTC_TIMESTAMP()
FROM Purchases p
JOIN Products pr ON pr.TenantId = p.TenantId AND pr.SKU = p.Reference
WHERE p.SupplierName = 'Opening Stock' AND p.Notes LIKE 'Opening stock (moved from expense #%'
  AND NOT EXISTS (SELECT 1 FROM PurchaseItems i WHERE i.PurchaseId = p.Id);

-- 3. Remove the purchase copies from Expenses (opening stock + duplicates of real purchases)
DELETE e FROM Expenses e
JOIN ExpenseCategories c ON c.Id = e.ExpenseCategoryId
WHERE c.Name = 'Purchase / Inventory'
  AND (e.ExpenseName LIKE 'Inventory - %' OR e.ExpenseName LIKE 'Purchase - %');

COMMIT;
