-- ═══════════════════════════════════════════════════════════
-- Tenant Menu Configuration
-- Stores hidden (disabled) menu keys per tenant.
-- If a menu key has a row here, it is hidden for that tenant.
-- No row = menu is visible (default behavior).
-- ═══════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS `TenantMenuConfigs` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` int NOT NULL,
    `MenuKey` varchar(200) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_TenantMenuConfigs_TenantId_MenuKey` (`TenantId`, `MenuKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Add DefaultDashboard column to Tenants table
ALTER TABLE `Tenants`
    ADD COLUMN IF NOT EXISTS `DefaultDashboard` varchar(200) NOT NULL DEFAULT '/admin-dashboard-2';
