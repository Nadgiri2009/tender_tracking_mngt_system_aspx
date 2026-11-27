SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.WorkflowStages', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowAssignments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowHistory', N'U') IS NULL
        THROW 51020, 'Apply the tender workflow and document migrations before this migration.', 1;

    IF OBJECT_ID(N'dbo.TenderRequirementVerificationHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderRequirementVerificationHistory
        (
            VerificationHistoryId BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderRequirementVerificationHistory PRIMARY KEY,
            TenderId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Tender REFERENCES dbo.Tender(TenderId),
            DocumentId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Document REFERENCES dbo.TenderDocuments(DocumentId),
            ReviewStage NVARCHAR(20) NOT NULL,
            VerificationStatus NVARCHAR(20) NOT NULL,
            ReviewerId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Reviewer REFERENCES dbo.Users(UserId),
            ReviewerRoleId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Role REFERENCES dbo.Roles(RoleId),
            Remarks NVARCHAR(1000) NULL,
            ReviewedAtUtc DATETIME2(0) NOT NULL
                CONSTRAINT DF_TenderRequirementVerificationHistory_ReviewedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT CK_TenderRequirementVerificationHistory_Stage
                CHECK(ReviewStage IN (N'JE',N'TENDER_CELL')),
            CONSTRAINT CK_TenderRequirementVerificationHistory_Status
                CHECK(VerificationStatus IN (N'Verified',N'Not Verified'))
        );
        CREATE INDEX IX_TenderRequirementVerificationHistory_Current
            ON dbo.TenderRequirementVerificationHistory
                (TenderId,DocumentId,ReviewStage,ReviewedAtUtc DESC,VerificationHistoryId DESC);
    END;

    IF OBJECT_ID(N'dbo.TenderDocumentShortfalls', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderDocumentShortfalls
        (
            TenderDocumentShortfallId BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderDocumentShortfalls PRIMARY KEY,
            TenderId INT NOT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_Tender REFERENCES dbo.Tender(TenderId),
            RaisedBy INT NOT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_RaisedBy REFERENCES dbo.Users(UserId),
            RaisedAtUtc DATETIME2(0) NOT NULL
                CONSTRAINT DF_TenderDocumentShortfalls_RaisedAtUtc DEFAULT SYSUTCDATETIME(),
            DeadlineAtUtc DATETIME2(0) NOT NULL,
            Remarks NVARCHAR(2000) NOT NULL,
            RequiredDocuments NVARCHAR(2000) NOT NULL,
            ResolutionStatus NVARCHAR(20) NOT NULL
                CONSTRAINT DF_TenderDocumentShortfalls_ResolutionStatus DEFAULT(N'Open'),
            ResubmittedBy INT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_ResubmittedBy REFERENCES dbo.Users(UserId),
            ResubmittedAtUtc DATETIME2(0) NULL,
            ResolvedBy INT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_ResolvedBy REFERENCES dbo.Users(UserId),
            ResolvedAtUtc DATETIME2(0) NULL,
            CONSTRAINT CK_TenderDocumentShortfalls_ResolutionStatus
                CHECK(ResolutionStatus IN (N'Open',N'Resubmitted',N'Resolved'))
        );
        CREATE INDEX IX_TenderDocumentShortfalls_Tender
            ON dbo.TenderDocumentShortfalls(TenderId,RaisedAtUtc DESC);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_Cell_InitialTenderDocumentVerification'
    )
    BEGIN
        IF EXISTS
        (
            SELECT StageOrder FROM dbo.WorkflowStages
            WHERE WorkflowVersion=2 GROUP BY StageOrder HAVING COUNT(*)>1
        )
            THROW 51021, 'Version-2 workflow stage orders are not unique; resolve the conflict before migration.', 1;

        UPDATE dbo.WorkflowStages
        SET StageOrder=StageOrder+1000
        WHERE WorkflowVersion=2 AND StageOrder>=102;

        UPDATE dbo.WorkflowStages
        SET StageOrder=StageOrder-999
        WHERE WorkflowVersion=2 AND StageOrder>=1102;

        DECLARE @CellRoleId INT;
        SELECT @CellRoleId=RoleId FROM dbo.Roles WHERE RoleCode=N'TENDER_CELL' AND IsActive=1;
        IF @CellRoleId IS NULL
            THROW 51022, 'The active Tender Cell role is required.', 1;

        UPDATE dbo.WorkflowStages
        SET StatusOnApprove=N'Pending Tender Cell Verification'
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_SubmitTender';

        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (102,N'Final_Cell_InitialTenderDocumentVerification',
             N'Pending Tender Cell Verification',N'Submitted',N'Pending JE Document Correction',
             @CellRoleId,1,2);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_CorrectTenderDocuments'
    )
    BEGIN
        DECLARE @JeRoleId INT;
        SELECT @JeRoleId=RoleId FROM dbo.Roles WHERE RoleCode=N'JE' AND IsActive=1;
        IF @JeRoleId IS NULL
            THROW 51023, 'The active Junior Engineer role is required.', 1;

        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (123,N'Final_JE_CorrectTenderDocuments',
             N'Pending JE Document Correction',N'Pending Tender Cell Verification',
             N'Pending JE Document Correction',@JeRoleId,1,2);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
