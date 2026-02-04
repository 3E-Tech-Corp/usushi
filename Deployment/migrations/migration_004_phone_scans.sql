-- Migration 004: Phone Scans table for saving scanned images and OCR results
-- Uses ImageAssetId FK to Assets instead of raw file paths

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('dbo.PhoneScans') AND type = 'U')
BEGIN
    CREATE TABLE dbo.PhoneScans (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ImageAssetId INT NULL,
        ScannedData NVARCHAR(MAX) NULL,        -- JSON of the OCR results
        ScannedBy INT NULL,                     -- User ID of admin who scanned
        ScannedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ReviewedAt DATETIME2 NULL,
        ReviewedBy INT NULL,
        Notes NVARCHAR(500) NULL,
        CONSTRAINT FK_PhoneScans_Assets FOREIGN KEY (ImageAssetId) REFERENCES Assets(Id)
    );
    PRINT 'Created PhoneScans table';
END
