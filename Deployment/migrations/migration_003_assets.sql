-- Migration 003: Assets table for centralized file storage
-- Pattern from funtime-shared: files named by asset ID, served via /asset/{id} endpoint

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('dbo.Assets') AND type = 'U')
BEGIN
    CREATE TABLE dbo.Assets (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SiteKey NVARCHAR(50) NOT NULL DEFAULT 'usushi',
        FileName NVARCHAR(500) NOT NULL,
        ContentType NVARCHAR(100) NOT NULL,
        FileSize BIGINT NULL,
        StorageUrl NVARCHAR(1000) NOT NULL,
        StorageType NVARCHAR(50) NOT NULL DEFAULT 'local',
        AssetType NVARCHAR(50) NULL,        -- 'phone-scan', 'receipt', 'image', etc.
        Category NVARCHAR(100) NULL,
        UploadedBy INT NULL,
        IsPublic BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    PRINT 'Created Assets table';
END
