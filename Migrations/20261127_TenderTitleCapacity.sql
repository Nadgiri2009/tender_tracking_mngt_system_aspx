SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TenderTitleMaxLength SMALLINT;
    SELECT @TenderTitleMaxLength = max_length
    FROM sys.columns
    WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'Title';

    IF @TenderTitleMaxLength IS NOT NULL AND @TenderTitleMaxLength >= 0
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'IX_Tender_Status_Dept')
            DROP INDEX IX_Tender_Status_Dept ON dbo.Tender;

        ALTER TABLE dbo.Tender ALTER COLUMN Title NVARCHAR(MAX) NOT NULL;
    END;

    DECLARE @RevisionTitleMaxLength SMALLINT;
    SELECT @RevisionTitleMaxLength = max_length
    FROM sys.columns
    WHERE object_id=OBJECT_ID(N'dbo.TenderRevisions') AND name=N'Title';
    IF @RevisionTitleMaxLength IS NOT NULL AND @RevisionTitleMaxLength >= 0
        ALTER TABLE dbo.TenderRevisions ALTER COLUMN Title NVARCHAR(MAX) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'IX_Tender_Status_Dept')
        CREATE INDEX IX_Tender_Status_Dept ON dbo.Tender(CurrentStatus,DepartmentId)
            INCLUDE(TenderNumber,Title,EstimatedAmount,SubmissionDeadline,CreatedDate,IsDeleted);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
