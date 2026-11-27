SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderGisLocations', N'U') IS NULL
        THROW 51000, 'TenderDocuments and TenderGisLocations must exist before this migration is applied.', 1;

    IF COL_LENGTH(N'dbo.TenderDocuments', N'MimeType') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD MimeType NVARCHAR(100) NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.TenderDocuments
        SET MimeType=CASE LOWER(COALESCE(NULLIF(FileExtension,N''''),N''''))
            WHEN N''.pdf'' THEN N''application/pdf''
            WHEN N''.doc'' THEN N''application/msword''
            WHEN N''.docx'' THEN N''application/vnd.openxmlformats-officedocument.wordprocessingml.document''
            WHEN N''.xls'' THEN N''application/vnd.ms-excel''
            WHEN N''.xlsx'' THEN N''application/vnd.openxmlformats-officedocument.spreadsheetml.sheet''
            WHEN N''.jpg'' THEN N''image/jpeg''
            WHEN N''.jpeg'' THEN N''image/jpeg''
            WHEN N''.png'' THEN N''image/png''
            ELSE N''application/octet-stream''
        END
        WHERE MimeType IS NULL OR LTRIM(RTRIM(MimeType))=N'''';

        ALTER TABLE dbo.TenderDocuments ALTER COLUMN MimeType NVARCHAR(100) NOT NULL;

        IF NOT EXISTS
        (
            SELECT 1 FROM sys.default_constraints
            WHERE parent_object_id=OBJECT_ID(N''dbo.TenderDocuments'')
              AND name=N''DF_TenderDocuments_MimeType''
        )
            ALTER TABLE dbo.TenderDocuments
                ADD CONSTRAINT DF_TenderDocuments_MimeType
                DEFAULT(N''application/octet-stream'') FOR MimeType;';

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryGeometryType') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryGeometryType NVARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryGeometryJson') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryGeometryJson NVARCHAR(MAX) NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.TenderGisLocations
        SET BoundaryGeometryType=CASE
                WHEN BoundaryCoordinatesJson IS NOT NULL THEN N''Polygon''
                WHEN JSON_VALUE(BoundaryLineGeometryJson,N''$.type'')=N''LineString'' THEN N''LineString''
                ELSE NULL
            END,
            BoundaryGeometryJson=CASE
                WHEN BoundaryCoordinatesJson IS NOT NULL
                    THEN N''{"type":"Polygon","coordinates":['' + BoundaryCoordinatesJson + N'']}''
                WHEN JSON_VALUE(BoundaryLineGeometryJson,N''$.type'')=N''LineString''
                    THEN BoundaryLineGeometryJson
                ELSE NULL
            END
        WHERE BoundaryGeometryType IS NULL OR BoundaryGeometryJson IS NULL;

        IF NOT EXISTS
        (
            SELECT 1 FROM sys.check_constraints
            WHERE parent_object_id=OBJECT_ID(N''dbo.TenderGisLocations'')
              AND name=N''CK_TenderGisLocations_BoundaryGeometry''
        )
            ALTER TABLE dbo.TenderGisLocations WITH CHECK
                ADD CONSTRAINT CK_TenderGisLocations_BoundaryGeometry
                CHECK
                (
                    (BoundaryGeometryType IS NULL AND BoundaryGeometryJson IS NULL)
                    OR
                    (BoundaryGeometryType IN (N''LineString'',N''Polygon'')
                     AND ISJSON(BoundaryGeometryJson)=1
                     AND JSON_VALUE(BoundaryGeometryJson,N''$.type'')=BoundaryGeometryType)
                );';

    IF OBJECT_ID(N'dbo.TenderDocumentFileHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderDocumentFileHistory
        (
            FileHistoryId BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderDocumentFileHistory PRIMARY KEY,
            TenderId INT NOT NULL,
            DocumentId INT NOT NULL,
            DocumentType NVARCHAR(80) NOT NULL,
            DocumentName NVARCHAR(250) NOT NULL,
            FileName NVARCHAR(260) NOT NULL,
            FilePath NVARCHAR(400) NOT NULL,
            FileExtension NVARCHAR(10) NOT NULL,
            FileSizeBytes BIGINT NULL,
            MimeType NVARCHAR(100) NOT NULL,
            UploadedBy INT NOT NULL,
            UploadedDate DATETIME2 NOT NULL,
            ActionName NVARCHAR(20) NOT NULL,
            ActionBy INT NOT NULL,
            ActionAtUtc DATETIME2 NOT NULL
                CONSTRAINT DF_TenderDocumentFileHistory_ActionAtUtc DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT CK_TenderDocumentFileHistory_ActionName
                CHECK(ActionName IN (N'Replaced',N'Deleted')),
            CONSTRAINT FK_TenderDocumentFileHistory_Tender
                FOREIGN KEY(TenderId) REFERENCES dbo.Tender(TenderId),
            CONSTRAINT FK_TenderDocumentFileHistory_UploadedBy
                FOREIGN KEY(UploadedBy) REFERENCES dbo.Users(UserId),
            CONSTRAINT FK_TenderDocumentFileHistory_ActionBy
                FOREIGN KEY(ActionBy) REFERENCES dbo.Users(UserId)
        );

        CREATE INDEX IX_TenderDocumentFileHistory_Document_ActionAtUtc
            ON dbo.TenderDocumentFileHistory(DocumentId,ActionAtUtc DESC);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
