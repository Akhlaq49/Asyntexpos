-- Stock entries (Manage Stock) now create a matching purchase worth product price x quantity
-- (Reference "STOCK-<entry id>", supplier "Stock Entry") so added stock counts in the dashboard's Total Purchase.
-- Run this ONCE per database to create the purchase for stock entries added before that change.
-- Safe to re-run: entries that already have their purchase are skipped. Product stock is not changed.

START TRANSACTION;

INSERT INTO Purchases (TenantId, SupplierName, SupplierRef, Reference, Date, Status, PaymentStatus,
                       OrderTax, Discount, Shipping, Total, Paid, Notes, CreatedAt, UpdatedAt)
SELECT s.TenantId, 'Stock Entry', NULL, CONCAT('STOCK-', s.Id), s.Date, 'Received', 'Paid', 0, 0, 0,
       s.Quantity * pr.Price, s.Quantity * pr.Price,
       LEFT(CONCAT('Stock added for ', pr.ProductName, IF(s.Person = '', '', CONCAT(' by ', s.Person))), 500),
       UTC_TIMESTAMP(), UTC_TIMESTAMP()
FROM StockEntries s
JOIN Products pr ON pr.Id = s.ProductId
WHERE NOT EXISTS (SELECT 1 FROM Purchases p
                  WHERE p.TenantId = s.TenantId AND p.SupplierName = 'Stock Entry' AND p.Reference = CONCAT('STOCK-', s.Id));

INSERT INTO PurchaseItems (TenantId, PurchaseId, ProductId, ProductName, Quantity, PurchasePrice,
                           Discount, TaxPercentage, TaxAmount, UnitCost, TotalCost, CreatedAt)
SELECT p.TenantId, p.Id, NULL, LEFT(pr.ProductName, 100), s.Quantity, pr.Price, 0, 0, 0, pr.Price, p.Total, UTC_TIMESTAMP()
FROM Purchases p
JOIN StockEntries s ON CONCAT('STOCK-', s.Id) = p.Reference AND s.TenantId = p.TenantId
JOIN Products pr ON pr.Id = s.ProductId
WHERE p.SupplierName = 'Stock Entry'
  AND NOT EXISTS (SELECT 1 FROM PurchaseItems i WHERE i.PurchaseId = p.Id);

COMMIT;
