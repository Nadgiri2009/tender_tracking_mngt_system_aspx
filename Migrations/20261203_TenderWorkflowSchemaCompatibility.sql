SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderFinancialApprovals', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowHistory', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderGeneratedDocuments', N'U') IS NULL
        THROW 51040, 'Apply the version-2 tender workflow schema before this migration.', 1;

    IF COL_LENGTH(N'dbo.TenderFinancialApprovals', N'SelectedFinancialBidId') IS NULL
        ALTER TABLE dbo.TenderFinancialApprovals ADD SelectedFinancialBidId INT NULL;

    IF COL_LENGTH(N'dbo.TenderWorkflowHistory', N'ActorRoleCode') IS NULL
        ALTER TABLE dbo.TenderWorkflowHistory ADD ActorRoleCode NVARCHAR(50) NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderFinancialApprovals')
          AND name=N'FK_TenderFinancialApprovals_SelectedFinancialBid'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.TenderFinancialApprovals
            ADD CONSTRAINT FK_TenderFinancialApprovals_SelectedFinancialBid
                FOREIGN KEY (SelectedFinancialBidId)
                REFERENCES dbo.FinancialBid(FinancialBidId);';

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
          AND name=N'CK_TenderGeneratedDocuments_Kind'
    )
        ALTER TABLE dbo.TenderGeneratedDocuments
            DROP CONSTRAINT CK_TenderGeneratedDocuments_Kind;

    ALTER TABLE dbo.TenderGeneratedDocuments
        ADD CONSTRAINT CK_TenderGeneratedDocuments_Kind
            CHECK (DocumentKind IN (N'WorkOrder',N'Agreement',N'BOQ'));

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
          AND name=N'CK_TenderGeneratedDocuments_Target'
    )
        ALTER TABLE dbo.TenderGeneratedDocuments
            DROP CONSTRAINT CK_TenderGeneratedDocuments_Target;

    ALTER TABLE dbo.TenderGeneratedDocuments
        ADD CONSTRAINT CK_TenderGeneratedDocuments_Target CHECK
        (
            (DocumentKind=N'WorkOrder' AND WorkOrderId IS NOT NULL AND AgreementId IS NULL)
            OR (DocumentKind=N'Agreement' AND AgreementId IS NOT NULL AND WorkOrderId IS NULL)
            OR (DocumentKind=N'BOQ' AND WorkOrderId IS NULL AND AgreementId IS NULL)
        );

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
          AND name=N'UX_TenderGeneratedDocuments_BoqVersion'
    )
        CREATE UNIQUE INDEX UX_TenderGeneratedDocuments_BoqVersion
            ON dbo.TenderGeneratedDocuments(TenderId,VersionNumber)
            WHERE DocumentKind=N'BOQ';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
