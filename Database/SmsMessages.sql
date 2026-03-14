-- ════════════════════════════════════════════════════════════════
--  SMS Messages Table (Poll-based SMS Gateway)
--  Run this on the ReactPosDb database on Plesk
-- ════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS `SmsMessages` (
    `Id`          INT            NOT NULL AUTO_INCREMENT,
    `TenantId`    INT            NOT NULL,
    `To`          VARCHAR(20)    NOT NULL,
    `Message`     VARCHAR(1600)  NOT NULL,
    `Channel`     VARCHAR(20)    NOT NULL DEFAULT 'sms',
    `Status`      VARCHAR(20)    NOT NULL DEFAULT 'pending',
    `Error`       VARCHAR(500)   NULL,
    `Reference`   VARCHAR(100)   NULL,
    `CreatedAt`   DATETIME(6)    NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `ProcessedAt` DATETIME(6)    NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_SmsMessages_TenantId` (`TenantId`),
    INDEX `IX_SmsMessages_Status`   (`Status`),
    INDEX `IX_SmsMessages_TenantId_Status` (`TenantId`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
