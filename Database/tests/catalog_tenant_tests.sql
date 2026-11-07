-- ============================================================================================================
-- Tenant isolation tests for the catalogue (Database/CATL/Schema.sql). Creates two test companies, checks what the
-- database itself enforces, removes them with SYST_PROC_DLCO, and prints PASS / FAIL per check.
--   mysql ... < Database/tests/catalog_tenant_tests.sql
-- Leaves nothing behind (also when a check fails).
-- ============================================================================================================

DROP TEMPORARY TABLE IF EXISTS t_result;
CREATE TEMPORARY TABLE t_result (n INT AUTO_INCREMENT PRIMARY KEY, outcome VARCHAR(4), test VARCHAR(255));

DROP PROCEDURE IF EXISTS SYST_PROC_TEXP;
DELIMITER $$
-- runs stmt; should_fail = 1: the database must refuse it
CREATE PROCEDURE SYST_PROC_TEXP(IN should_fail TINYINT, IN test VARCHAR(255), IN stmt TEXT)
BEGIN
  DECLARE failed TINYINT DEFAULT 0;
  BEGIN
    DECLARE CONTINUE HANDLER FOR SQLEXCEPTION SET failed = 1;
    SET @t_sql = stmt;
    PREPARE s FROM @t_sql; EXECUTE s; DEALLOCATE PREPARE s;
  END;
  INSERT INTO t_result (outcome, test) VALUES (IF(failed = should_fail, 'PASS', 'FAIL'), test);
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS SYST_PROC_TCHK;
DELIMITER $$
CREATE PROCEDURE SYST_PROC_TCHK(IN test VARCHAR(255), IN cond TINYINT)
BEGIN
  INSERT INTO t_result (outcome, test) VALUES (IF(cond, 'PASS', 'FAIL'), test);
END$$
DELIMITER ;

-- ── two companies with the same names ───────────────────────────────────────────────────────────────────
INSERT INTO SYST_TNNT (Name, Email, IsActive, DefaultDashboard, CreatedAt) VALUES ('Tenant test A', 'tenant-test-a@test.local', 1, '', NOW(6));
SET @a = LAST_INSERT_ID();
INSERT INTO SYST_TNNT (Name, Email, IsActive, DefaultDashboard, CreatedAt) VALUES ('Tenant test B', 'tenant-test-b@test.local', 1, '', NOW(6));
SET @b = LAST_INSERT_ID();

INSERT INTO CATL_TIER (TenantId, Name, Code, SortOrder) VALUES (@a, 'Standard', 'standard', 1), (@a, 'Premium', 'premium', 2), (@b, 'Standard', 'standard', 1), (@b, 'Premium', 'premium', 2);
SELECT Id INTO @a_std FROM CATL_TIER WHERE TenantId = @a AND Code = 'standard';
SELECT Id INTO @a_prem FROM CATL_TIER WHERE TenantId = @a AND Code = 'premium';
SELECT Id INTO @b_std FROM CATL_TIER WHERE TenantId = @b AND Code = 'standard';

INSERT INTO CATL_CATG (TenantId, Name, SortOrder, IsDeleted) VALUES (@a, 'Lighting Control', 1, 0);
SET @a_cat = LAST_INSERT_ID();
INSERT INTO CATL_CATG (TenantId, Name, SortOrder, IsDeleted) VALUES (@b, 'Lighting Control', 1, 0);
SET @b_cat = LAST_INSERT_ID();
INSERT INTO CATL_FMLY (TenantId, GroupId, Name, SortOrder, IsDeleted) VALUES (@a, @a_cat, 'Piano Series', 1, 0);
SET @a_fam = LAST_INSERT_ID();
INSERT INTO CATL_FMLY (TenantId, GroupId, Name, SortOrder, IsDeleted) VALUES (@b, @b_cat, 'Piano Series', 1, 0);
SET @b_fam = LAST_INSERT_ID();
INSERT INTO CATL_FMTR (TenantId, FamilyId, TierId) VALUES (@a, @a_fam, @a_std), (@a, @a_fam, @a_prem), (@b, @b_fam, @b_std);

INSERT INTO CATL_PROD (TenantId, FamilyId, Name) VALUES (@a, @a_fam, 'Innswitch Piano 2 Gang');
SET @a_prod = LAST_INSERT_ID();
INSERT INTO CATL_PROD (TenantId, FamilyId, Name) VALUES (@b, @b_fam, 'Innswitch Piano 2 Gang');
SET @b_prod = LAST_INSERT_ID();
INSERT INTO CATL_PROD (TenantId, FamilyId, Name, Unit, FallbackPrice) VALUES (@a, @a_fam, 'Curtain Rail', 'm', 1000);
SET @a_rail = LAST_INSERT_ID();

INSERT INTO INVT_PROD (TenantId, ProductName, Model, Color, ProductType, Quantity, Price, DiscountValue, QuantityAlert, CreatedAt, UpdatedAt, IsDeleted, CatalogProductId, IsDefaultSku)
VALUES (@a, 'Innswitch Piano 2 Gang White', 'T1L2W', 'White', 'single', 0, 17311, 0, 0, NOW(6), NOW(6), 0, @a_prod, 1);
SET @a_white = LAST_INSERT_ID();
INSERT INTO INVT_PROD (TenantId, ProductName, Model, Color, ProductType, Quantity, Price, DiscountValue, QuantityAlert, CreatedAt, UpdatedAt, IsDeleted, CatalogProductId, IsDefaultSku)
VALUES (@a, 'Innswitch Piano 2 Gang Black', 'T1L2B', 'Black', 'single', 0, 17311, 0, 0, NOW(6), NOW(6), 0, @a_prod, 0);
SET @a_black = LAST_INSERT_ID();
INSERT INTO INVT_PROD (TenantId, ProductName, Model, Color, ProductType, Quantity, Price, DiscountValue, QuantityAlert, CreatedAt, UpdatedAt, IsDeleted, CatalogProductId, IsDefaultSku)
VALUES (@b, 'Innswitch Piano 2 Gang White', 'T1L2W', 'White', 'single', 0, 20000, 0, 0, NOW(6), NOW(6), 0, @b_prod, 1);
SET @b_white = LAST_INSERT_ID();


INSERT INTO CATL_SIZE (TenantId, Name, SortOrder, IsDeleted) VALUES (@a, '10 Marla', 1, 0);
SET @a_size = LAST_INSERT_ID();
INSERT INTO CATL_SIZE (TenantId, Name, SortOrder, IsDeleted) VALUES (@b, '10 Marla', 1, 0);
SET @b_size = LAST_INSERT_ID();
INSERT INTO CATL_PKLN (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES
  (@a, @a_size, @a_std, @a_prod, 4), (@a, @a_size, @a_prem, @a_prod, 2), (@a, @a_size, @a_std, @a_rail, 12.5), (@b, @b_size, @b_std, @b_prod, 9);

-- ── names are unique per company, not across companies ───────────────────────────────────────────────────
CALL SYST_PROC_TCHK('two companies have a product with the same name', (SELECT COUNT(*) FROM CATL_PROD WHERE Name = 'Innswitch Piano 2 Gang' AND TenantId IN (@a, @b)) = 2);
CALL SYST_PROC_TEXP(1, 'a company can''t have two products with the same name',
  CONCAT('INSERT INTO CATL_PROD (TenantId, Name) VALUES (', @a, ', ''innswitch piano 2 gang'')'));
CALL SYST_PROC_TEXP(1, 'a company can''t have two tiers with the same name', CONCAT('INSERT INTO CATL_TIER (TenantId, Name, Code) VALUES (', @a, ', ''Standard'', ''x'')'));
CALL SYST_PROC_TEXP(1, 'a company can''t have two colours with the same name',
  CONCAT('INSERT INTO CATL_COLR (TenantId, Name) VALUES (', @a, ', ''White''), (', @a, ', ''white'')'));

-- ── no reference to another company's rows (composite foreign keys) ──────────────────────────────────────
CALL SYST_PROC_TEXP(1, 'product -> family of another company', CONCAT('INSERT INTO CATL_PROD (TenantId, FamilyId, Name) VALUES (', @a, ', ', @b_fam, ', ''x1'')'));
CALL SYST_PROC_TEXP(1, 'SKU -> product of another company', CONCAT('UPDATE INVT_PROD SET CatalogProductId = ', @b_prod, ' WHERE Id = ', @a_black));
CALL SYST_PROC_TEXP(1, 'package line -> product of another company', CONCAT('INSERT INTO CATL_PKLN (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (', @a, ', ', @a_size, ', ', @a_std, ', ', @b_prod, ', 1)'));
CALL SYST_PROC_TEXP(1, 'package line -> package of another company', CONCAT('INSERT INTO CATL_PKLN (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (', @a, ', ', @b_size, ', ', @a_std, ', ', @a_rail, ', 1)'));
CALL SYST_PROC_TEXP(1, 'package line -> tier of another company', CONCAT('INSERT INTO CATL_PKLN (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (', @a, ', ', @a_size, ', ', @b_std, ', ', @a_rail, ', 1)'));
CALL SYST_PROC_TEXP(1, 'family tier -> tier of another company', CONCAT('INSERT INTO CATL_FMTR (TenantId, FamilyId, TierId) VALUES (', @a, ', ', @a_fam, ', ', @b_std, ')'));
CALL SYST_PROC_TEXP(1, 'family -> category of another company', CONCAT('UPDATE CATL_FMLY SET GroupId = ', @b_cat, ' WHERE Id = ', @a_fam));
CALL SYST_PROC_TEXP(1, 'alias -> product of another company', CONCAT('INSERT INTO CATL_ALIS (TenantId, Alias, AliasKey, Source, TargetKind, CatalogProductId) VALUES (', @a, ', ''x'', ''x'', ''package'', ''product'', ', @b_prod, ')'));
CALL SYST_PROC_TEXP(1, 'drawing rule -> product of another company', CONCAT('INSERT INTO CATL_RULE (TenantId, RuleName, Gang, `Rank`, CatalogProductId) VALUES (', @a, ', ''Switch boards'', ''2'', 1, ', @b_prod, ')'));
CALL SYST_PROC_TEXP(0, 'the same rows within one company are accepted', CONCAT('INSERT INTO CATL_RULE (TenantId, RuleName, Gang, `Rank`, CatalogProductId) VALUES (', @a, ', ''Switch boards'', ''2'', 1, ', @a_prod, ')'));

-- ── one default SKU, sensible values ──────────────────────────────────────────────
CALL SYST_PROC_TEXP(1, 'a second default SKU of a product is refused', CONCAT('UPDATE INVT_PROD SET IsDefaultSku = 1 WHERE Id = ', @a_black));
CALL SYST_PROC_TEXP(1, 'a quantity of 0 is refused (missing = no row)', CONCAT('UPDATE CATL_PKLN SET Quantity = 0 WHERE TenantId = ', @a, ' AND CatalogProductId = ', @a_rail));
CALL SYST_PROC_TEXP(1, 'an alias pointing at nothing is refused', CONCAT('INSERT INTO CATL_ALIS (TenantId, Alias, AliasKey, Source, TargetKind) VALUES (', @a, ', ''y'', ''y'', ''package'', ''product'')'));

-- ── the views: own data only, prices from the inventory, never stored ──────────────────────────────────
CALL SYST_PROC_TCHK('CATL_VIEW_PKQT: A 10 Marla Standard = 4 x 17311 + 12.5 m x 1000 (entered price, not in the inventory)',
  (SELECT SUM(LineTotal) FROM CATL_VIEW_PKQT WHERE TenantId = @a AND PackageSizeId = @a_size AND TierId = @a_std) = 4 * 17311 + 12.5 * 1000);
CALL SYST_PROC_TCHK('CATL_VIEW_PKQT: B uses its own price and quantity (9 x 20000)',
  (SELECT SUM(LineTotal) FROM CATL_VIEW_PKQT WHERE TenantId = @b) = 9 * 20000);
CALL SYST_PROC_TCHK('CATL_VIEW_PKQT shows each company only its own lines',
  (SELECT COUNT(*) FROM CATL_VIEW_PKQT WHERE TenantId = @a AND CatalogProductId = @b_prod) = 0);
CALL SYST_PROC_TCHK('fractional quantity kept (12.5 m)', (SELECT Quantity FROM CATL_PKLN WHERE CatalogProductId = @a_rail) = 12.5);
CALL SYST_PROC_TCHK('CATL_VIEW_SUGG: family in both tiers needs confirmation (A)',
  (SELECT MIN(NeedsConfirmation) FROM CATL_VIEW_SUGG WHERE TenantId = @a AND CatalogProductId = @a_prod) = 1);
CALL SYST_PROC_TCHK('CATL_VIEW_SUGG: Standard-only family is not offered for Premium (B)',
  (SELECT COUNT(*) FROM CATL_VIEW_SUGG WHERE TenantId = @b AND TierCode = 'premium') = 0);
UPDATE INVT_PROD SET Price = 18000 WHERE Id = @a_white;
CALL SYST_PROC_TCHK('price comes from the inventory (the default SKU''s INVT_PROD.Price)',
  (SELECT UnitPrice FROM CATL_VIEW_PKQT WHERE TenantId = @a AND CatalogProductId = @a_prod AND TierId = @a_std) = 18000);
UPDATE INVT_PROD SET Price = 17311 WHERE Id = @a_white;
DELETE FROM CATL_FMTR WHERE TenantId = @a AND TierId = @a_prem;
CALL SYST_PROC_TCHK('CATL_VIEW_TRCF lists a Premium line whose family is no longer Premium',
  (SELECT COUNT(*) FROM CATL_VIEW_TRCF WHERE TenantId = @a AND CatalogProductId = @a_prod) = 1);

-- ── deleting a company removes everything of it, and nothing of the other ───────────────────────────────
CALL SYST_PROC_DLCO(@a);
CALL SYST_PROC_TCHK('SYST_PROC_DLCO: nothing of A left',
  (SELECT COUNT(*) FROM SYST_TNNT WHERE Id = @a) + (SELECT COUNT(*) FROM CATL_PROD WHERE TenantId = @a)
  + (SELECT COUNT(*) FROM INVT_PROD WHERE TenantId = @a) + (SELECT COUNT(*) FROM CATL_PKLN WHERE TenantId = @a)
  + (SELECT COUNT(*) FROM CATL_CATG WHERE TenantId = @a) = 0);
CALL SYST_PROC_TCHK('SYST_PROC_DLCO: B untouched',
  (SELECT COUNT(*) FROM CATL_PROD WHERE TenantId = @b) = 1 AND (SELECT COUNT(*) FROM CATL_PKLN WHERE TenantId = @b) = 1);
CALL SYST_PROC_DLCO(@b);

SELECT outcome, test FROM t_result ORDER BY n;
SELECT CONCAT(SUM(outcome = 'PASS'), ' passed, ', SUM(outcome = 'FAIL'), ' failed') AS summary FROM t_result;

DROP PROCEDURE SYST_PROC_TEXP;
DROP PROCEDURE SYST_PROC_TCHK;
DROP TEMPORARY TABLE t_result;
