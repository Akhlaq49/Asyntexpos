-- SmartinnPos backend: drop the tables / columns of features removed from this app
-- (installments + misc register, HRM, finance except expenses, coupons, stock transfer/adjustment,
--  stores/warehouses, sub categories, web content). Generated from the local SmartinnPosDb.
-- Permanently deletes that data. Run ONCE, only on the database used by the SmartinnPos backend
-- (never on the shared database still used by the original Asyntexpos backend).

SET FOREIGN_KEY_CHECKS = 0;
ALTER TABLE `parties` DROP FOREIGN KEY `FK_Parties_Departments_DepartmentId`;
ALTER TABLE `parties` DROP FOREIGN KEY `FK_Parties_Designations_DesignationId`;
ALTER TABLE `parties` DROP FOREIGN KEY `FK_Parties_Shifts_ShiftId`;
ALTER TABLE `Parties` DROP COLUMN `DepartmentId`, DROP COLUMN `DesignationId`, DROP COLUMN `ShiftId`, DROP COLUMN `DateOfJoining`, DROP COLUMN `BasicSalary`, DROP COLUMN `EmployeeId`;
ALTER TABLE `Products` DROP COLUMN `SubCategory`;
DROP TABLE IF EXISTS `accounttypes`;
DROP TABLE IF EXISTS `attendances`;
DROP TABLE IF EXISTS `bankaccounts`;
DROP TABLE IF EXISTS `coupons`;
DROP TABLE IF EXISTS `departments`;
DROP TABLE IF EXISTS `designations`;
DROP TABLE IF EXISTS `financeincomes`;
DROP TABLE IF EXISTS `holidays`;
DROP TABLE IF EXISTS `incomecategories`;
DROP TABLE IF EXISTS `installmentplans`;
DROP TABLE IF EXISTS `leaves`;
DROP TABLE IF EXISTS `leavetypes`;
DROP TABLE IF EXISTS `miscellaneousregisters`;
DROP TABLE IF EXISTS `payrolls`;
DROP TABLE IF EXISTS `planguarantors`;
DROP TABLE IF EXISTS `planmedia`;
DROP TABLE IF EXISTS `repaymententries`;
DROP TABLE IF EXISTS `shifts`;
DROP TABLE IF EXISTS `stockadjustments`;
DROP TABLE IF EXISTS `stocktransferitems`;
DROP TABLE IF EXISTS `stocktransfers`;
DROP TABLE IF EXISTS `stores`;
DROP TABLE IF EXISTS `subcategories`;
DROP TABLE IF EXISTS `warehouses`;
DROP TABLE IF EXISTS `webcontents`;
SET FOREIGN_KEY_CHECKS = 1;
