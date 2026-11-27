SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 51000, 'Tender and Users tables must exist before this migration is applied.', 1;

    IF OBJECT_ID(N'dbo.TenderImportantDateHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderImportantDateHistory
        (
            HistoryId INT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderImportantDateHistory PRIMARY KEY,
            TenderId INT NOT NULL,
            FieldName NVARCHAR(64) NOT NULL,
            PreviousValue NVARCHAR(2000) NULL,
            NewValue NVARCHAR(2000) NULL,
            ModifiedBy INT NOT NULL,
            ModifiedAtUtc DATETIME2(0) NOT NULL
                CONSTRAINT DF_TenderImportantDateHistory_ModifiedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_TenderImportantDateHistory_Tender
                FOREIGN KEY(TenderId) REFERENCES dbo.Tender(TenderId),
            CONSTRAINT FK_TenderImportantDateHistory_User
                FOREIGN KEY(ModifiedBy) REFERENCES dbo.Users(UserId)
        );
        CREATE INDEX IX_TenderImportantDateHistory_TenderModified
            ON dbo.TenderImportantDateHistory(TenderId,ModifiedAtUtc DESC,HistoryId DESC);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
