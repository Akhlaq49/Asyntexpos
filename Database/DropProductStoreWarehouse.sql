-- Store & Warehouse were removed from products (frontend, API and EF model).
-- The app no longer reads or writes these columns, so leaving them is harmless.
-- Run this ONCE per database only when you are sure the old values are not needed:
-- it permanently deletes each product's saved Store/Warehouse name.

ALTER TABLE `Products` DROP COLUMN `Store`;
ALTER TABLE `Products` DROP COLUMN `Warehouse`;
