SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WorkflowStages', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowAssignments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Roles', N'U') IS NULL
        THROW 51050, 'Apply the version-2 tender workflow migrations before this migration.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.TenderWorkflowAssignments a
        INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
        WHERE a.IsCurrent=1 AND s.WorkflowVersion=2
          AND s.StageName<>N'Final_JE_SubmitTender'
    )
        THROW 51051, 'A non-initial version-2 workflow is active; review it before aligning workflow stages.', 1;

    DECLARE @ExpectedStages TABLE
    (
        StageOrder INT NOT NULL PRIMARY KEY,
        StageName NVARCHAR(80) NOT NULL UNIQUE,
        StatusOnPending NVARCHAR(50) NOT NULL,
        StatusOnApprove NVARCHAR(50) NOT NULL,
        StatusOnReject NVARCHAR(50) NOT NULL,
        RoleCode NVARCHAR(50) NOT NULL
    );

    INSERT INTO @ExpectedStages
        (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,RoleCode)
    VALUES
        (101,N'Final_JE_SubmitTender',N'Draft',N'Pending Tender Cell Verification',N'Draft',N'JE'),
        (102,N'Final_Cell_InitialTenderDocumentVerification',N'Pending Tender Cell Verification',N'Submitted',N'Pending JE Document Correction',N'TENDER_CELL'),
        (103,N'Final_JE_PublishTender',N'Submitted',N'Tender Published',N'Submitted',N'JE'),
        (104,N'Final_JE_OpenTechnicalBids',N'Tender Published',N'Primary Verification Pending',N'Tender Published',N'JE'),
        (105,N'Final_JE_CompletePrimaryVerification',N'Primary Verification Pending',N'Primary Verification Completed',N'Primary Verification Pending',N'JE'),
        (106,N'Final_JE_ForwardToTenderCell',N'Primary Verification Completed',N'Tender Cell Verification Pending',N'Primary Verification Completed',N'JE'),
        (107,N'Final_Cell_CompleteSecondaryVerification',N'Tender Cell Verification Pending',N'Financial Bid Opening Approval Pending',N'Shortfall Generation Pending',N'TENDER_CELL'),
        (108,N'Final_Cell_CreateShortfall',N'Shortfall Generation Pending',N'Shortfall Pending JE Approval',N'Tender Cell Verification Pending',N'TENDER_CELL'),
        (109,N'Final_JE_ApproveShortfall',N'Shortfall Pending JE Approval',N'Awaiting Contractor Submission',N'Shortfall Generation Pending',N'JE'),
        (110,N'Final_Contractor_SubmitShortfall',N'Awaiting Contractor Submission',N'Shortfall Documents Submitted',N'Awaiting Contractor Submission',N'TENDER_CELL'),
        (111,N'Final_Cell_ClearShortfall',N'Shortfall Documents Submitted',N'Shortfall Cleared',N'Awaiting Contractor Submission',N'TENDER_CELL'),
        (112,N'Final_Cell_ResumeVerification',N'Shortfall Cleared',N'Tender Cell Verification Pending',N'Shortfall Cleared',N'TENDER_CELL'),
        (113,N'Final_Samiti_ApproveFinancialOpening',N'Financial Bid Opening Approval Pending',N'Financial Bid Opening Approved',N'Financial Bid Opening Rejected',N'CHAIRMAN'),
        (114,N'Final_Cell_ReverifyRejectedOpening',N'Financial Bid Opening Rejected',N'Financial Bid Opening Approval Pending',N'Tender Cell Verification Pending',N'TENDER_CELL'),
        (115,N'Final_JE_OpenFinancialBids',N'Financial Bid Opening Approved',N'Final Financial Approval Pending',N'Financial Bid Opening Approved',N'JE'),
        (116,N'Final_Auditor_ApproveFinancialBids',N'Final Financial Approval Pending',N'Final Financial Approval Completed',N'Final Financial Approval Rejected',N'DIRECTOR_AUDITOR'),
        (117,N'Final_JE_ResubmitFinancialApproval',N'Final Financial Approval Rejected',N'Final Financial Approval Pending',N'Final Financial Approval Rejected',N'JE'),
        (118,N'Final_JE_StartWorkOrder',N'Final Financial Approval Completed',N'Work Order Pending',N'Final Financial Approval Completed',N'JE'),
        (119,N'Final_JE_GenerateWorkOrder',N'Work Order Pending',N'Work Order Generated',N'Work Order Pending',N'JE'),
        (120,N'Final_JE_StartAgreement',N'Work Order Generated',N'Tender Agreement Pending',N'Work Order Generated',N'JE'),
        (121,N'Final_JE_GenerateAgreement',N'Tender Agreement Pending',N'Tender Agreement Generated',N'Tender Agreement Pending',N'JE'),
        (122,N'Final_JE_CompleteTender',N'Tender Agreement Generated',N'Completed',N'Tender Agreement Generated',N'JE'),
        (123,N'Final_JE_CorrectTenderDocuments',N'Pending JE Document Correction',N'Pending Tender Cell Verification',N'Pending JE Document Correction',N'JE');

    IF EXISTS
    (
        SELECT 1
        FROM @ExpectedStages expected
        LEFT JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
        WHERE role.RoleId IS NULL
    )
        THROW 51052, 'One or more active workflow roles are missing.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowStages stage
        WHERE stage.WorkflowVersion=2
          AND NOT EXISTS (SELECT 1 FROM @ExpectedStages expected WHERE expected.StageName=stage.StageName)
    )
        THROW 51053, 'An unexpected version-2 stage exists; review it before aligning the workflow.', 1;

    UPDATE dbo.WorkflowStages
    SET StageOrder=20000+StageId
    WHERE WorkflowVersion=2;

    UPDATE stage
    SET StageOrder=expected.StageOrder,
        StatusOnPending=expected.StatusOnPending,
        StatusOnApprove=expected.StatusOnApprove,
        StatusOnReject=expected.StatusOnReject,
        ApproverRoleId=role.RoleId,
        IsActive=1
    FROM dbo.WorkflowStages stage
    INNER JOIN @ExpectedStages expected ON expected.StageName=stage.StageName
    INNER JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
    WHERE stage.WorkflowVersion=2;

    INSERT INTO dbo.WorkflowStages
        (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
         ApproverRoleId,IsActive,WorkflowVersion)
    SELECT expected.StageOrder,expected.StageName,expected.StatusOnPending,
           expected.StatusOnApprove,expected.StatusOnReject,role.RoleId,1,2
    FROM @ExpectedStages expected
    INNER JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
    WHERE NOT EXISTS
        (SELECT 1 FROM dbo.WorkflowStages existing
         WHERE existing.WorkflowVersion=2 AND existing.StageName=expected.StageName);

    IF EXISTS
    (
        SELECT 1
        FROM @ExpectedStages expected
        LEFT JOIN dbo.WorkflowStages stage
          ON stage.WorkflowVersion=2 AND stage.StageName=expected.StageName
        WHERE stage.StageId IS NULL OR stage.StageOrder<>expected.StageOrder OR stage.IsActive<>1
    )
        THROW 51054, 'Version-2 workflow stage alignment did not complete as expected.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
