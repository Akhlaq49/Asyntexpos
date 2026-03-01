-- SQL Script: Drop Purchases Table (Cleanup)
-- Description: Removes the Purchases table
-- Use this for development/testing purposes only
-- Make sure to backup your data before running this!

-- Drop table if it exists
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Purchases')
BEGIN
    DROP TABLE [dbo].[Purchases];
    PRINT 'Table [Purchases] dropped successfully';
END
ELSE
BEGIN
    PRINT 'Table [Purchases] does not exist';
END
