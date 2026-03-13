CREATE TABLE IF NOT EXISTS `SmsWhitelistedNumbers` (
    `Id`          INT           NOT NULL AUTO_INCREMENT,
    `PhoneNumber` VARCHAR(20)   NOT NULL,
    `CreatedAt`   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_SmsWhitelistedNumbers_Phone` (`PhoneNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
