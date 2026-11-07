-- ============================================================================================================
-- 2026-11-07-01  Drop the tables, columns, views and procedures nothing uses any more.  MySQL 8 / MariaDB 10.5.
-- Run by a person (never by the app), BEFORE 2026-11-07-02-rename-tables.sql, on a database backed up first:
--   mysqldump --skip-lock-tables --no-tablespaces --set-gtid-purged=OFF --column-statistics=0 <db> > backup.sql
--   mysql <db> < 2026-11-07-01-drop-unused-tables.sql
-- Safe to rerun (IF EXISTS everywhere).
--
--   Invoices, InvoiceItems           invoice screens were never part of the app
--   SmsMessages, SmsWhitelistedNumbers   nothing queues SMS
--   VariantAttributes                no screen
--   PartyAddresses (+ Sales.BillingAddressId / ShippingAddressId)   never filled
--   PackageItems, PackageQuantities, CatalogMigrations   moved into the catalogue (CatalogProducts / CatalogPackageLines)
--   CatalogPriceLists, CatalogPriceListItems, v_active_sku_price   prices come from the inventory (Products.Price)
--   SolutionProductLines.InStandard / InPremium   the plans are in CatalogFamilyTiers
--   views v_* and procedures sp_delete_company, _cat_*   recreated with their new names by the app on start
-- ============================================================================================================

DROP PROCEDURE IF EXISTS _mig_drop_fks;
DELIMITER $$
-- drops the foreign keys of a column (their names differ between databases)
CREATE PROCEDURE _mig_drop_fks(IN tbl VARCHAR(64), IN col VARCHAR(64))
BEGIN
  DECLARE done INT DEFAULT 0;
  DECLARE fk VARCHAR(64);
  DECLARE cur CURSOR FOR
    SELECT CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(tbl) AND COLUMN_NAME = col AND REFERENCED_TABLE_NAME IS NOT NULL;
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;
  OPEN cur;
  l: LOOP
    FETCH cur INTO fk;
    IF done THEN LEAVE l; END IF;
    SET @s = CONCAT('ALTER TABLE `', tbl, '` DROP FOREIGN KEY `', fk, '`');
    PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
  END LOOP;
  CLOSE cur;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS _mig_drop_column;
DELIMITER $$
CREATE PROCEDURE _mig_drop_column(IN tbl VARCHAR(64), IN col VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(tbl) AND COLUMN_NAME = col) THEN
    CALL _mig_drop_fks(tbl, col);
    SET @s = CONCAT('ALTER TABLE `', tbl, '` DROP COLUMN `', col, '`');
    PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
  END IF;
END$$
DELIMITER ;

CALL _mig_drop_column('Sales', 'BillingAddressId');
CALL _mig_drop_column('Sales', 'ShippingAddressId');
CALL _mig_drop_column('SolutionProductLines', 'InStandard');
CALL _mig_drop_column('SolutionProductLines', 'InPremium');

-- views read the tables being renamed: dropped here, recreated (new names) by the app
DROP VIEW IF EXISTS `v_active_sku_price`;
DROP VIEW IF EXISTS `v_suggestable_product`;
DROP VIEW IF EXISTS `v_package_quote`;
DROP VIEW IF EXISTS `v_package_tier_conflict`;
DROP VIEW IF EXISTS `v_unbalanced_journal`;
DROP VIEW IF EXISTS `v_account_balance`;

DROP PROCEDURE IF EXISTS `sp_delete_company`;
DROP PROCEDURE IF EXISTS `_cat_exec_if`;
DROP PROCEDURE IF EXISTS `_cat_add_column`;
DROP PROCEDURE IF EXISTS `_cat_add_index`;
DROP PROCEDURE IF EXISTS `_cat_add_fk`;

SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS `InvoiceItems`;
DROP TABLE IF EXISTS `Invoices`;
DROP TABLE IF EXISTS `SmsMessages`;
DROP TABLE IF EXISTS `SmsWhitelistedNumbers`;
DROP TABLE IF EXISTS `VariantAttributes`;
DROP TABLE IF EXISTS `PartyAddresses`;
DROP TABLE IF EXISTS `PackageQuantities`;
DROP TABLE IF EXISTS `PackageItems`;
DROP TABLE IF EXISTS `CatalogPriceListItems`;
DROP TABLE IF EXISTS `CatalogPriceLists`;
DROP TABLE IF EXISTS `CatalogMigrations`;
SET FOREIGN_KEY_CHECKS = 1;

DROP PROCEDURE IF EXISTS _mig_drop_column;
DROP PROCEDURE IF EXISTS _mig_drop_fks;
