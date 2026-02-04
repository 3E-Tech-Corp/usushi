-- Migration 005: Add ReceiptAssetId to Meals table
-- Migrates receipt image storage from raw PhotoPath to Asset system

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Meals') AND name = 'ReceiptAssetId')
BEGIN
    ALTER TABLE Meals ADD ReceiptAssetId INT NULL;
    PRINT 'Added ReceiptAssetId column to Meals table';
END
