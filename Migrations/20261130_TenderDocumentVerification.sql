SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 51000, 'TenderDocuments and Users must exist before this migration is applied.', 1;

    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerificationStatus') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerificationStatus NVARCHAR(30) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerificationRemarks') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerificationRemarks NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerifiedBy') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerifiedBy INT NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerifiedAtUtc') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerifiedAtUtc DATETIME2(0) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderDocuments')
          AND name=N'FK_TenderDocuments_VerifiedBy'
    )
        ALTER TABLE dbo.TenderDocuments WITH CHECK
            ADD CONSTRAINT FK_TenderDocuments_VerifiedBy
            FOREIGN KEY(VerifiedBy) REFERENCES dbo.Users(UserId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderDocuments')
          AND name=N'CK_TenderDocuments_VerificationStatus'
    )
        ALTER TABLE dbo.TenderDocuments WITH CHECK
            ADD CONSTRAINT CK_TenderDocuments_VerificationStatus
            CHECK(VerificationStatus IS NULL OR VerificationStatus IN (N'Verified',N'Not Verified'));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
