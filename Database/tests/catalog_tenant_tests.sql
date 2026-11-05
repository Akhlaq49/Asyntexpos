-- ============================================================================================================
-- Tenant isolation tests for the catalogue (CatalogSchema.sql). Creates two test companies, checks what the
-- database itself enforces, removes them with sp_delete_company, and prints PASS / FAIL per check.
--   mysql ... < Database/tests/catalog_tenant_tests.sql
-- Leaves nothing behind (also when a check fails).
-- ============================================================================================================

DROP TEMPORARY TABLE IF EXISTS t_result;
CREATE TEMPORARY TABLE t_result (n INT AUTO_INCREMENT PRIMARY KEY, outcome VARCHAR(4), test VARCHAR(255));

DROP PROCEDURE IF EXISTS _t_expect;
DELIMITER $$
-- runs stmt; should_fail = 1: the database must refuse it
CREATE PROCEDURE _t_expect(IN should_fail TINYINT, IN test VARCHAR(255), IN stmt TEXT)
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

DROP PROCEDURE IF EXISTS _t_check;
DELIMITER $$
CREATE PROCEDURE _t_check(IN test VARCHAR(255), IN cond TINYINT)
BEGIN
  INSERT INTO t_result (outcome, test) VALUES (IF(cond, 'PASS', 'FAIL'), test);
END$$
DELIMITER ;

-- ── two companies with the same names ───────────────────────────────────────────────────────────────────
INSERT INTO Tenants (Name, Email, IsActive, DefaultDashboard, CreatedAt) VALUES ('Tenant test A', 'tenant-test-a@test.local', 1, '', NOW(6));
SET @a = LAST_INSERT_ID();
INSERT INTO Tenants (Name, Email, IsActive, DefaultDashboard, CreatedAt) VALUES ('Tenant test B', 'tenant-test-b@test.local', 1, '', NOW(6));
SET @b = LAST_INSERT_ID();

INSERT INTO CatalogTiers (TenantId, Name, Code, SortOrder) VALUES (@a, 'Standard', 'standard', 1), (@a, 'Premium', 'premium', 2), (@b, 'Standard', 'standard', 1), (@b, 'Premium', 'premium', 2);
SELECT Id INTO @a_std FROM CatalogTiers WHERE TenantId = @a AND Code = 'standard';
SELECT Id INTO @a_prem FROM CatalogTiers WHERE TenantId = @a AND Code = 'premium';
SELECT Id INTO @b_std FROM CatalogTiers WHERE TenantId = @b AND Code = 'standard';

INSERT INTO SolutionGroups (TenantId, Name, SortOrder, IsDeleted) VALUES (@a, 'Lighting Control', 1, 0);
SET @a_cat = LAST_INSERT_ID();
INSERT INTO SolutionGroups (TenantId, Name, SortOrder, IsDeleted) VALUES (@b, 'Lighting Control', 1, 0);
SET @b_cat = LAST_INSERT_ID();
INSERT INTO SolutionProductLines (TenantId, GroupId, Name, InStandard, InPremium, SortOrder, IsDeleted) VALUES (@a, @a_cat, 'Piano Series', 1, 1, 1, 0);
SET @a_fam = LAST_INSERT_ID();
INSERT INTO SolutionProductLines (TenantId, GroupId, Name, InStandard, InPremium, SortOrder, IsDeleted) VALUES (@b, @b_cat, 'Piano Series', 1, 0, 1, 0);
SET @b_fam = LAST_INSERT_ID();
INSERT INTO CatalogFamilyTiers (TenantId, FamilyId, TierId) VALUES (@a, @a_fam, @a_std), (@a, @a_fam, @a_prem), (@b, @b_fam, @b_std);

INSERT INTO CatalogProducts (TenantId, FamilyId, Name) VALUES (@a, @a_fam, 'Innswitch Piano 2 Gang');
SET @a_prod = LAST_INSERT_ID();
INSERT INTO CatalogProducts (TenantId, FamilyId, Name) VALUES (@b, @b_fam, 'Innswitch Piano 2 Gang');
SET @b_prod = LAST_INSERT_ID();
INSERT INTO CatalogProducts (TenantId, FamilyId, Name, Unit, FallbackPrice) VALUES (@a, @a_fam, 'Curtain Rail', 'm', 1000);
SET @a_rail = LAST_INSERT_ID();

INSERT INTO Products (TenantId, ProductName, Model, Color, ProductType, Quantity, Price, DiscountValue, QuantityAlert, CreatedAt, UpdatedAt, IsDeleted, CatalogProductId, IsDefaultSku)
VALUES (@a, 'Innswitch Piano 2 Gang White', 'T1L2W', 'White', 'single', 0, 17311, 0, 0, NOW(6), NOW(6), 0, @a_prod, 1);
SET @a_white = LAST_INSERT_ID();
INSERT INTO Products (TenantId, ProductName, Model, Color, ProductType, Quantity, Price, DiscountValue, QuantityAlert, CreatedAt, UpdatedAt, IsDeleted, CatalogProductId, IsDefaultSku)
VALUES (@a, 'Innswitch Piano 2 Gang Black', 'T1L2B', 'Black', 'single', 0, 17311, 0, 0, NOW(6), NOW(6), 0, @a_prod, 0);
SET @a_black = LAST_INSERT_ID();
INSERT INTO Products (TenantId, ProductName, Model, Color, ProductType, Quantity, Price, DiscountValue, QuantityAlert, CreatedAt, UpdatedAt, IsDeleted, CatalogProductId, IsDefaultSku)
VALUES (@b, 'Innswitch Piano 2 Gang White', 'T1L2W', 'White', 'single', 0, 20000, 0, 0, NOW(6), NOW(6), 0, @b_prod, 1);
SET @b_white = LAST_INSERT_ID();

INSERT INTO CatalogPriceLists (TenantId, Name, IsActive) VALUES (@a, 'Price list 2026', 1), (@b, 'Price list 2026', 1);
SELECT Id INTO @a_list FROM CatalogPriceLists WHERE TenantId = @a;
SELECT Id INTO @b_list FROM CatalogPriceLists WHERE TenantId = @b;
INSERT INTO CatalogPriceListItems (TenantId, PriceListId, SkuId, Price) VALUES (@a, @a_list, @a_white, 17311), (@a, @a_list, @a_black, 17500), (@b, @b_list, @b_white, 20000);

INSERT INTO PackageSizes (TenantId, Name, SortOrder, IsDeleted) VALUES (@a, '10 Marla', 1, 0);
SET @a_size = LAST_INSERT_ID();
INSERT INTO PackageSizes (TenantId, Name, SortOrder, IsDeleted) VALUES (@b, '10 Marla', 1, 0);
SET @b_size = LAST_INSERT_ID();
INSERT INTO CatalogPackageLines (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES
  (@a, @a_size, @a_std, @a_prod, 4), (@a, @a_size, @a_prem, @a_prod, 2), (@a, @a_size, @a_std, @a_rail, 12.5), (@b, @b_size, @b_std, @b_prod, 9);

-- ── names are unique per company, not across companies ───────────────────────────────────────────────────
CALL _t_check('two companies have a product with the same name', (SELECT COUNT(*) FROM CatalogProducts WHERE Name = 'Innswitch Piano 2 Gang' AND TenantId IN (@a, @b)) = 2);
CALL _t_expect(1, 'a company can''t have two products with the same name',
  CONCAT('INSERT INTO CatalogProducts (TenantId, Name) VALUES (', @a, ', ''innswitch piano 2 gang'')'));
CALL _t_expect(1, 'a company can''t have two tiers with the same name', CONCAT('INSERT INTO CatalogTiers (TenantId, Name, Code) VALUES (', @a, ', ''Standard'', ''x'')'));
CALL _t_expect(1, 'a company can''t have two colours with the same name',
  CONCAT('INSERT INTO CatalogColors (TenantId, Name) VALUES (', @a, ', ''White''), (', @a, ', ''white'')'));

-- ── no reference to another company's rows (composite foreign keys) ──────────────────────────────────────
CALL _t_expect(1, 'product -> family of another company', CONCAT('INSERT INTO CatalogProducts (TenantId, FamilyId, Name) VALUES (', @a, ', ', @b_fam, ', ''x1'')'));
CALL _t_expect(1, 'SKU -> product of another company', CONCAT('UPDATE Products SET CatalogProductId = ', @b_prod, ' WHERE Id = ', @a_black));
CALL _t_expect(1, 'price list item -> SKU of another company', CONCAT('INSERT INTO CatalogPriceListItems (TenantId, PriceListId, SkuId, Price) VALUES (', @a, ', ', @a_list, ', ', @b_white, ', 1)'));
CALL _t_expect(1, 'price list item -> price list of another company', CONCAT('INSERT INTO CatalogPriceListItems (TenantId, PriceListId, SkuId, Price) VALUES (', @a, ', ', @b_list, ', ', @a_white, ', 1)'));
CALL _t_expect(1, 'package line -> product of another company', CONCAT('INSERT INTO CatalogPackageLines (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (', @a, ', ', @a_size, ', ', @a_std, ', ', @b_prod, ', 1)'));
CALL _t_expect(1, 'package line -> package of another company', CONCAT('INSERT INTO CatalogPackageLines (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (', @a, ', ', @b_size, ', ', @a_std, ', ', @a_rail, ', 1)'));
CALL _t_expect(1, 'package line -> tier of another company', CONCAT('INSERT INTO CatalogPackageLines (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (', @a, ', ', @a_size, ', ', @b_std, ', ', @a_rail, ', 1)'));
CALL _t_expect(1, 'family tier -> tier of another company', CONCAT('INSERT INTO CatalogFamilyTiers (TenantId, FamilyId, TierId) VALUES (', @a, ', ', @a_fam, ', ', @b_std, ')'));
CALL _t_expect(1, 'family -> category of another company', CONCAT('UPDATE SolutionProductLines SET GroupId = ', @b_cat, ' WHERE Id = ', @a_fam));
CALL _t_expect(1, 'alias -> product of another company', CONCAT('INSERT INTO CatalogAliases (TenantId, Alias, AliasKey, Source, TargetKind, CatalogProductId) VALUES (', @a, ', ''x'', ''x'', ''package'', ''product'', ', @b_prod, ')'));
CALL _t_expect(1, 'drawing rule -> product of another company', CONCAT('INSERT INTO CatalogDrawingRuleItems (TenantId, RuleName, Gang, `Rank`, CatalogProductId) VALUES (', @a, ', ''Switch boards'', ''2'', 1, ', @b_prod, ')'));
CALL _t_expect(0, 'the same rows within one company are accepted', CONCAT('INSERT INTO CatalogDrawingRuleItems (TenantId, RuleName, Gang, `Rank`, CatalogProductId) VALUES (', @a, ', ''Switch boards'', ''2'', 1, ', @a_prod, ')'));

-- ── one active price list, one default SKU, sensible values ──────────────────────────────────────────────
CALL _t_expect(1, 'a second active price list is refused', CONCAT('INSERT INTO CatalogPriceLists (TenantId, Name, IsActive) VALUES (', @a, ', ''Another'', 1)'));
CALL _t_expect(0, 'an inactive second price list is fine', CONCAT('INSERT INTO CatalogPriceLists (TenantId, Name, IsActive) VALUES (', @a, ', ''Old list'', 0)'));
CALL _t_expect(1, 'a second default SKU of a product is refused', CONCAT('UPDATE Products SET IsDefaultSku = 1 WHERE Id = ', @a_black));
CALL _t_expect(1, 'a quantity of 0 is refused (missing = no row)', CONCAT('UPDATE CatalogPackageLines SET Quantity = 0 WHERE TenantId = ', @a, ' AND CatalogProductId = ', @a_rail));
CALL _t_expect(1, 'a negative price is refused', CONCAT('UPDATE CatalogPriceListItems SET Price = -1 WHERE SkuId = ', @a_white));
CALL _t_expect(1, 'an alias pointing at nothing is refused', CONCAT('INSERT INTO CatalogAliases (TenantId, Alias, AliasKey, Source, TargetKind) VALUES (', @a, ', ''y'', ''y'', ''package'', ''product'')'));

-- ── the views: own data only, prices from the active list, never stored ──────────────────────────────────
CALL _t_check('v_package_quote: A 10 Marla Standard = 4 x 17311 + 12.5 m x 1000 (packages-file price, no SKU)',
  (SELECT SUM(LineTotal) FROM v_package_quote WHERE TenantId = @a AND PackageSizeId = @a_size AND TierId = @a_std) = 4 * 17311 + 12.5 * 1000);
CALL _t_check('v_package_quote: B uses its own price and quantity (9 x 20000)',
  (SELECT SUM(LineTotal) FROM v_package_quote WHERE TenantId = @b) = 9 * 20000);
CALL _t_check('v_package_quote shows each company only its own lines',
  (SELECT COUNT(*) FROM v_package_quote WHERE TenantId = @a AND CatalogProductId = @b_prod) = 0);
CALL _t_check('fractional quantity kept (12.5 m)', (SELECT Quantity FROM CatalogPackageLines WHERE CatalogProductId = @a_rail) = 12.5);
CALL _t_check('v_suggestable_product: family in both tiers needs confirmation (A)',
  (SELECT MIN(NeedsConfirmation) FROM v_suggestable_product WHERE TenantId = @a AND CatalogProductId = @a_prod) = 1);
CALL _t_check('v_suggestable_product: Standard-only family is not offered for Premium (B)',
  (SELECT COUNT(*) FROM v_suggestable_product WHERE TenantId = @b AND TierCode = 'premium') = 0);
UPDATE CatalogPriceLists SET IsActive = 0 WHERE Id = @a_list;
CALL _t_check('price comes from the active price list only (none active: no price)',
  (SELECT UnitPrice FROM v_package_quote WHERE TenantId = @a AND CatalogProductId = @a_prod AND TierId = @a_std) IS NULL);
UPDATE CatalogPriceLists SET IsActive = 1 WHERE Id = @a_list;
DELETE FROM CatalogFamilyTiers WHERE TenantId = @a AND TierId = @a_prem;
CALL _t_check('v_package_tier_conflict lists a Premium line whose family is no longer Premium',
  (SELECT COUNT(*) FROM v_package_tier_conflict WHERE TenantId = @a AND CatalogProductId = @a_prod) = 1);

-- ── deleting a company removes everything of it, and nothing of the other ───────────────────────────────
CALL sp_delete_company(@a);
CALL _t_check('sp_delete_company: nothing of A left',
  (SELECT COUNT(*) FROM Tenants WHERE Id = @a) + (SELECT COUNT(*) FROM CatalogProducts WHERE TenantId = @a)
  + (SELECT COUNT(*) FROM Products WHERE TenantId = @a) + (SELECT COUNT(*) FROM CatalogPackageLines WHERE TenantId = @a)
  + (SELECT COUNT(*) FROM CatalogPriceLists WHERE TenantId = @a) + (SELECT COUNT(*) FROM SolutionGroups WHERE TenantId = @a) = 0);
CALL _t_check('sp_delete_company: B untouched',
  (SELECT COUNT(*) FROM CatalogProducts WHERE TenantId = @b) = 1 AND (SELECT COUNT(*) FROM CatalogPackageLines WHERE TenantId = @b) = 1);
CALL sp_delete_company(@b);

SELECT outcome, test FROM t_result ORDER BY n;
SELECT CONCAT(SUM(outcome = 'PASS'), ' passed, ', SUM(outcome = 'FAIL'), ' failed') AS summary FROM t_result;

DROP PROCEDURE _t_expect;
DROP PROCEDURE _t_check;
DROP TEMPORARY TABLE t_result;
