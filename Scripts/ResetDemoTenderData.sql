SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'tender_tracking'
    THROW 51010, 'Demo reset is restricted to the configured local tender_tracking development database.', 1;
IF UPPER(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName'))) <> UPPER(HOST_NAME())
    THROW 51011, 'Demo reset must run against SQL Server on this same development machine.', 1;

DECLARE @DemoTenders TABLE(TenderId int NOT NULL PRIMARY KEY);

INSERT INTO @DemoTenders(TenderId)
SELECT TenderId
FROM dbo.Tender
WHERE TenderNumber LIKE N'DEMO-PORTAL-%'
  AND TrackingId LIKE N'DEMO-PORTAL-TRACK-%';

IF NOT EXISTS (SELECT 1 FROM @DemoTenders)
BEGIN
    PRINT 'No DEMO-PORTAL records found. Nothing was changed.';
    RETURN;
END;

IF EXISTS
(
    SELECT 1 FROM dbo.TenderBidOpening WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    UNION ALL
    SELECT 1 FROM dbo.TenderBudgetOpinions WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    UNION ALL
    SELECT 1 FROM dbo.TenderEvaluationDocuments WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    UNION ALL
    SELECT 1 FROM dbo.TenderLifecycleHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
)
    THROW 51012, 'Reset is blocked because immutable audit records exist for one or more demo tenders.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE FROM dbo.TenderGeneratedDocuments WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderShortfallSubmissions
    WHERE ShortfallItemId IN
    (
        SELECT i.ShortfallItemId
        FROM dbo.TenderShortfallItems i
        INNER JOIN dbo.TenderShortfalls sf ON sf.ShortfallId=i.ShortfallId
        WHERE sf.TenderId IN (SELECT TenderId FROM @DemoTenders)
    );
    DELETE FROM dbo.TenderNotifications WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderShortfallItems
    WHERE ShortfallId IN
    (
        SELECT ShortfallId FROM dbo.TenderShortfalls
        WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    );
    DELETE FROM dbo.TenderShortfalls WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderFinancialBidOpeningDetails
    WHERE FinancialBidOpeningId IN
    (
        SELECT FinancialBidOpeningId FROM dbo.TenderFinancialBidOpening
        WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    );
    DELETE FROM dbo.TenderFinancialBidDocuments
    WHERE FinancialBidId IN
    (
        SELECT FinancialBidId FROM dbo.FinancialBid
        WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    );
    DELETE FROM dbo.TenderFinancialApprovals WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderFinancialBidOpening WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.Payments
    WHERE BillId IN
    (
        SELECT BillId FROM dbo.Bills WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    );
    DELETE FROM dbo.MeasurementBook WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.WorkExecution WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.Bills WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.Estimate WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.Agreement WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.WorkOrder WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.FinancialBid WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);

    DELETE FROM dbo.TenderDocumentVerificationHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderVerificationReviews WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TechnicalBidDocuments
    WHERE TechnicalBidId IN
    (
        SELECT TechnicalBidId FROM dbo.TechnicalBid
        WHERE TenderId IN (SELECT TenderId FROM @DemoTenders)
    );
    DELETE FROM dbo.TechnicalBid WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);

    DELETE FROM dbo.TenderRequirementVerificationHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderDocumentFileHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderAdministrativeApprovalHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderAdministrativeApprovals WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.ApprovalHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderEvaluation WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderExtensionRequests WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderDocumentShortfalls WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderImportantDateHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderOfflineSubmissions WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderRevisions WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderWorkflowAssignments WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderWorkflowHistory WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderPublicationSchedule WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderGisLocations WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderRequirements WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderRequiredBidDocuments WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.TenderDocuments WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.BOQ WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);
    DELETE FROM dbo.Tender WHERE TenderId IN (SELECT TenderId FROM @DemoTenders);

    COMMIT TRANSACTION;
    PRINT 'Only DEMO-PORTAL tenders and their linked seeded data were removed. Demo sample files were retained.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
