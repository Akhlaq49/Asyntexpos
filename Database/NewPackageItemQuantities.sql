-- Package quantities for the package items added from Smartinn_Naming_Comparison.xlsx: 1 for every building size,
-- in every company. Safe to rerun; a quantity already entered is kept.
--
-- Not given a quantity (on purpose):
--   Piano Switch 4 Gang, Matrix / Fusion Switch 1-4 Gang - switch boards are worked out from the drawing;
--     a package quantity on them would add extra switches next to the drawing's
--   Matter Gateway - added on the drawing page only where the client uses another company's automation

INSERT INTO PackageQuantities (TenantId, PackageItemId, PackageSizeId, Quantity, IsDeleted)
SELECT i.TenantId, i.Id, s.Id, 1, 0
FROM PackageItems i
JOIN PackageSizes s ON s.TenantId = i.TenantId AND s.IsDeleted = 0
WHERE i.IsDeleted = 0
  AND i.Name IN ('Smart Panel Hpad 7', 'Smart Panic Button', 'Human Presence Sensor')
ON DUPLICATE KEY UPDATE
  Quantity  = IF(PackageQuantities.IsDeleted = 1 OR PackageQuantities.Quantity = 0, 1, PackageQuantities.Quantity),
  IsDeleted = 0,
  DeletedAt = NULL,
  DeletedBy = NULL;
