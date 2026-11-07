-- ============================================================================================================
-- SYST: helper procedures used by the module scripts, and SYST_PROC_DLCO (delete a company). MySQL 8 / MariaDB 10.5.
-- Safe to rerun. Run by the backend on start (Data/DatabaseSetup.cs) before Database/CATL and Database/ACCT.
-- Names: constitution "Database naming" - 4-letter segments joined by "_".
-- ============================================================================================================

DROP PROCEDURE IF EXISTS SYST_PROC_EXIF;
DELIMITER $$
-- runs `stmt` when `missing` (a COUNT(*) query over information_schema) returns 0
CREATE PROCEDURE SYST_PROC_EXIF(IN missing TEXT, IN stmt TEXT)
BEGIN
  SET @cat_q = CONCAT('SELECT COUNT(*) INTO @cat_n FROM (', missing, ') x');
  PREPARE s FROM @cat_q; EXECUTE s; DEALLOCATE PREPARE s;
  IF @cat_n = 0 THEN
    SET @cat_s = stmt;
    PREPARE s FROM @cat_s; EXECUTE s; DEALLOCATE PREPARE s;
  END IF;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS SYST_PROC_ADCL;
DELIMITER $$
CREATE PROCEDURE SYST_PROC_ADCL(IN tbl VARCHAR(64), IN col VARCHAR(64), IN ddl TEXT)
BEGIN
  CALL SYST_PROC_EXIF(CONCAT('SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(''', tbl, ''') AND COLUMN_NAME = ''', col, ''''),
                    CONCAT('ALTER TABLE `', tbl, '` ADD COLUMN ', ddl));
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS SYST_PROC_ADIX;
DELIMITER $$
CREATE PROCEDURE SYST_PROC_ADIX(IN tbl VARCHAR(64), IN idx VARCHAR(64), IN ddl TEXT)
BEGIN
  CALL SYST_PROC_EXIF(CONCAT('SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(''', tbl, ''') AND INDEX_NAME = ''', idx, ''''),
                    CONCAT('ALTER TABLE `', tbl, '` ADD ', ddl));
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS SYST_PROC_ADFK;
DELIMITER $$
CREATE PROCEDURE SYST_PROC_ADFK(IN tbl VARCHAR(64), IN fk VARCHAR(64), IN ddl TEXT)
BEGIN
  CALL SYST_PROC_EXIF(CONCAT('SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(''', tbl, ''') AND CONSTRAINT_NAME = ''', fk, ''''),
                    CONCAT('ALTER TABLE `', tbl, '` ADD CONSTRAINT `', fk, '` ', ddl));
END$$
DELIMITER ;

-- ── delete a company and everything it owns ──────────────────────────────────────────────────────────────
-- every table with a TenantId column, then the company; one transaction. Not called by the app - run by hand.
DROP PROCEDURE IF EXISTS SYST_PROC_DLCO;
DELIMITER $$
CREATE PROCEDURE SYST_PROC_DLCO(IN p_company_id INT)
BEGIN
  DECLARE done INT DEFAULT 0;
  DECLARE t VARCHAR(64);
  DECLARE cur CURSOR FOR
    SELECT c.TABLE_NAME FROM information_schema.COLUMNS c
    JOIN information_schema.TABLES tb ON tb.TABLE_SCHEMA = c.TABLE_SCHEMA AND tb.TABLE_NAME = c.TABLE_NAME AND tb.TABLE_TYPE = 'BASE TABLE'
    WHERE c.TABLE_SCHEMA = DATABASE() AND c.COLUMN_NAME = 'TenantId';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; SET FOREIGN_KEY_CHECKS = 1; RESIGNAL; END;
  IF NOT EXISTS (SELECT 1 FROM `SYST_TNNT` WHERE `Id` = p_company_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'No such company';
  END IF;
  START TRANSACTION;
  SET FOREIGN_KEY_CHECKS = 0;      -- the company's own rows reference each other; all of them go
  OPEN cur;
  read_loop: LOOP
    FETCH cur INTO t;
    IF done THEN LEAVE read_loop; END IF;
    SET @sql = CONCAT('DELETE FROM `', t, '` WHERE `TenantId` = ', p_company_id);
    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;
  END LOOP;
  CLOSE cur;
  DELETE FROM `SYST_TNNT` WHERE `Id` = p_company_id;
  SET FOREIGN_KEY_CHECKS = 1;
  COMMIT;
END$$
DELIMITER ;
