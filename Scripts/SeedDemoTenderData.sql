SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'tender_tracking'
    THROW 51000, 'Demo seeding is restricted to the configured local tender_tracking development database.', 1;
IF UPPER(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName'))) <> UPPER(HOST_NAME())
    THROW 51001, 'Demo seeding must run against SQL Server on this same development machine.', 1;

DECLARE @Now datetime2(7) = SYSUTCDATETIME();
DECLARE @JeUserId int;
DECLARE @CellUserId int;
DECLARE @ChairmanUserId int;
DECLARE @AuditorUserId int;
DECLARE @ContractorId int;
DECLARE @FinancialYear nvarchar(9);
DECLARE @TenderCategoryId int;
DECLARE @ScopeDocumentTypeId int;
DECLARE @TechnicalDocumentTypeId int;
DECLARE @FinancialDocumentTypeId int;
DECLARE @JeRoleId int;
DECLARE @CellRoleId int;
DECLARE @ChairmanRoleId int;
DECLARE @AuditorRoleId int;

SELECT @JeRoleId = RoleId FROM dbo.Roles WHERE RoleCode = N'JE' AND IsActive = 1;
SELECT @CellRoleId = RoleId FROM dbo.Roles WHERE RoleCode = N'TENDER_CELL' AND IsActive = 1;
SELECT @ChairmanRoleId = RoleId FROM dbo.Roles WHERE RoleCode = N'CHAIRMAN' AND IsActive = 1;
SELECT @AuditorRoleId = RoleId FROM dbo.Roles WHERE RoleCode = N'DIRECTOR_AUDITOR' AND IsActive = 1;

IF @JeRoleId IS NULL OR @CellRoleId IS NULL OR @ChairmanRoleId IS NULL OR @AuditorRoleId IS NULL
    THROW 51002, 'The four active demo workflow roles must exist before seeding.', 1;

IF (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = @JeRoleId AND IsActive = 1) <> 1 OR
   (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = @CellRoleId AND IsActive = 1) <> 1 OR
   (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = @ChairmanRoleId AND IsActive = 1) <> 1 OR
   (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = @AuditorRoleId AND IsActive = 1) <> 1
    THROW 51003, 'Expected exactly one active user mapped to each demo role.', 1;

SELECT @JeUserId = UserId FROM dbo.Users WHERE RoleId = @JeRoleId AND IsActive = 1;
SELECT @CellUserId = UserId FROM dbo.Users WHERE RoleId = @CellRoleId AND IsActive = 1;
SELECT @ChairmanUserId = UserId FROM dbo.Users WHERE RoleId = @ChairmanRoleId AND IsActive = 1;
SELECT @AuditorUserId = UserId FROM dbo.Users WHERE RoleId = @AuditorRoleId AND IsActive = 1;
SELECT TOP (1) @ContractorId = ContractorId FROM dbo.Contractors WHERE IsActive = 1 ORDER BY ContractorId;
SELECT TOP (1) @FinancialYear = FinancialYear
FROM dbo.FinancialYears WHERE IsActive = 1 ORDER BY SortOrder DESC, StartDate DESC;
SELECT @TenderCategoryId = TenderCategoryId
FROM dbo.TenderTypes WHERE TypeName = N'30 Days' AND IsActive = 1;
SELECT @ScopeDocumentTypeId = DocumentTypeId
FROM dbo.DocumentTypes WHERE TypeName = N'Scope of Work' AND IsActive = 1;
SELECT @TechnicalDocumentTypeId = DocumentTypeId
FROM dbo.DocumentTypes WHERE TypeName = N'Technical Requirements' AND IsActive = 1;
SELECT @FinancialDocumentTypeId = DocumentTypeId
FROM dbo.DocumentTypes WHERE TypeName = N'Financial Requirements' AND IsActive = 1;

IF @ContractorId IS NULL OR @FinancialYear IS NULL OR @TenderCategoryId IS NULL OR
   @ScopeDocumentTypeId IS NULL OR @TechnicalDocumentTypeId IS NULL OR @FinancialDocumentTypeId IS NULL
    THROW 51004, 'Required active contractor, financial year, tender category, or document categories are missing.', 1;

DECLARE @Seeds TABLE
(
    DemoNumber int NOT NULL PRIMARY KEY,
    TenderNumber nvarchar(50) NOT NULL,
    TrackingId nvarchar(100) NOT NULL,
    Title nvarchar(300) NOT NULL,
    Description nvarchar(max) NOT NULL,
    DepartmentName nvarchar(150) NOT NULL,
    EstimatedAmount decimal(18,2) NOT NULL,
    StageName nvarchar(80) NOT NULL,
    CurrentStatus nvarchar(50) NOT NULL,
    HistoryThrough int NOT NULL,
    RouteVariant tinyint NOT NULL,
    IsPublished bit NOT NULL,
    ApprovalsReady bit NOT NULL,
    HasShortfall bit NOT NULL,
    HasTechnicalBid bit NOT NULL,
    SecondaryReviewComplete bit NOT NULL,
    HasFinancialBids bit NOT NULL,
    AuditDecision nvarchar(20) NULL,
    HasWorkOrder bit NOT NULL,
    HasAgreement bit NOT NULL
);

INSERT INTO @Seeds VALUES
(1,N'DEMO-PORTAL-001',N'DEMO-PORTAL-TRACK-001',N'DEMO - Construction of Internal Roads in Zone 1',
 N'DEMO ONLY. Illustrative internal-road improvement package in Zone 1, including site preparation, pavement works, drainage tie-ins, traffic management, and quality checks. Not a real procurement opportunity.',
 N'Zone 1',3850000.00,N'Final_JE_SubmitTender',N'Draft',0,0,0,0,0,0,0,0,NULL,0,0),
(2,N'DEMO-PORTAL-002',N'DEMO-PORTAL-TRACK-002',N'DEMO - Repair and Resurfacing of Roads in Zone 2',
 N'DEMO ONLY. Illustrative resurfacing package for selected Zone 2 streets, including milling, asphalt overlay, junction reinstatement, and road-marking works. A Tender Cell document shortfall is open for JE correction.',
 N'Zone 2',4620000.00,N'Final_JE_CorrectTenderDocuments',N'Pending JE Document Correction',1,1,0,0,1,0,0,0,NULL,0,0),
(3,N'DEMO-PORTAL-003',N'DEMO-PORTAL-TRACK-003',N'DEMO - Installation of Underground Drainage Pipeline',
 N'DEMO ONLY. Illustrative drainage pipeline extension in Zone 3, including trenching, pipe laying, chambers, reinstatement, and testing. Financial bid opening is approved and JE financial data entry is pending.',
 N'Zone 3',7280000.00,N'Final_JE_OpenFinancialBids',N'Financial Bid Opening Approved',8,0,0,1,0,1,1,0,NULL,0,0),
(4,N'DEMO-PORTAL-004',N'DEMO-PORTAL-TRACK-004',N'DEMO - Construction of Municipal Public Toilet',
 N'DEMO ONLY. Illustrative public-facility construction package in the City Engineer department, including foundations, accessible amenities, water connection, drainage, and commissioning.',
 N'City Engineer',5940000.00,N'Final_JE_StartWorkOrder',N'Final Financial Approval Completed',10,0,0,1,0,1,1,1,N'Approved',0,0),
(5,N'DEMO-PORTAL-005',N'DEMO-PORTAL-TRACK-005',N'DEMO - Water Supply Pipeline Extension',
 N'DEMO ONLY. Illustrative PHE pipeline extension, including pipe supply, trenching, valves, service connections, pressure testing, and reinstatement. Work order is generated; JE agreement action is pending.',
 N'PHE',8460000.00,N'Final_JE_StartAgreement',N'Work Order Generated',12,0,0,1,0,1,1,1,N'Approved',1,0),
(6,N'DEMO-PORTAL-006',N'DEMO-PORTAL-TRACK-006',N'DEMO - Construction of Municipal Office Building',
 N'DEMO ONLY. Illustrative municipal office building package for lifecycle demonstration, including civil works, electrical provisions, safety checks, handover, and sample completion documents. This record is marked completed for presentation.',
 N'Zone 4',12650000.00,N'Final_JE_CompleteTender',N'Completed',15,0,0,1,1,1,1,1,N'Approved',1,1),
(7,N'DEMO-PORTAL-007',N'DEMO-PORTAL-TRACK-007',N'DEMO - Storm Water Drainage Improvement',
 N'DEMO ONLY. Illustrative storm-water drainage improvement package in the Garden department catchment, including channel repairs, inlet improvements, and desilting access.',
 N'Garden',3120000.00,N'Final_Cell_InitialTenderDocumentVerification',N'Pending Tender Cell Verification',1,0,0,1,0,0,0,0,NULL,0,0),
(8,N'DEMO-PORTAL-008',N'DEMO-PORTAL-TRACK-008',N'DEMO - Street Light Installation and Maintenance',
 N'DEMO ONLY. Illustrative street-light installation and maintenance package in Zone 5, including poles, LED luminaires, cabling, earthing, testing, and maintenance response.',
 N'Zone 5',4180000.00,N'Final_Cell_CompleteSecondaryVerification',N'Tender Cell Verification Pending',6,0,0,1,1,0,0,0,NULL,0,0),
(9,N'DEMO-PORTAL-009',N'DEMO-PORTAL-TRACK-009',N'DEMO - Development of Municipal Garden',
 N'DEMO ONLY. Illustrative garden development works in Zone 6, including pathways, planting, irrigation, seating, lighting, and maintenance planning. Technical documents are verified and financial opening approval is pending.',
 N'Zone 6',2760000.00,N'Final_Samiti_ApproveFinancialOpening',N'Financial Bid Opening Approval Pending',7,0,0,1,0,1,1,0,NULL,0,0),
(10,N'DEMO-PORTAL-010',N'DEMO-PORTAL-TRACK-010',N'DEMO - Road Repair and Footpath Improvement',
 N'DEMO ONLY. Illustrative road repair and footpath improvement package in Zone 7, including kerb work, accessible ramps, surface repairs, and pedestrian safety provisions. Chief Audit financial approval is pending.',
 N'Zone 7',5340000.00,N'Final_Auditor_ApproveFinancialBids',N'Final Financial Approval Pending',9,0,0,1,0,1,1,1,NULL,0,0),
(11,N'DEMO-PORTAL-011',N'DEMO-PORTAL-TRACK-011',N'DEMO - Municipal Access Road Renewal',
 N'DEMO ONLY. Illustrative municipal access-road renewal package in Zone 8. Chief Audit returned the financial decision with remarks, and JE resubmission is pending.',
 N'Zone 8',4980000.00,N'Final_JE_ResubmitFinancialApproval',N'Final Financial Approval Rejected',9,3,0,1,0,1,1,1,N'Returned',0,0),
(12,N'DEMO-PORTAL-012',N'DEMO-PORTAL-TRACK-012',N'DEMO - Demonstration Street Lighting Tender',
 N'DEMO ONLY. Illustrative published tender for street-light upgrades in Zone 1. Dates, scope, documents, approvals, and location are sample presentation data only; this is not an actual SMC tender.',
 N'Zone 1',3650000.00,N'Final_JE_OpenTechnicalBids',N'Tender Published',3,0,1,1,0,0,0,0,NULL,0,0),
(13,N'DEMO-PORTAL-013',N'DEMO-PORTAL-TRACK-013',N'DEMO - Municipal Utility Works Package',
 N'DEMO ONLY. Illustrative utility works package in the Zoo department area. The Samiti returned financial bid opening with remarks and Tender Cell re-verification is pending.',
 N'Zoo',3270000.00,N'Final_Cell_ReverifyRejectedOpening',N'Financial Bid Opening Rejected',7,2,0,1,0,1,1,0,NULL,0,0);

IF EXISTS
(
    SELECT 1
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    WHERE t.TrackingId <> d.TrackingId
)
    THROW 51005, 'A tender number in the DEMO-PORTAL range is already used by a different record; no data was changed.', 1;

IF EXISTS
(
    SELECT 1
    FROM @Seeds d
    LEFT JOIN dbo.Departments dep ON dep.DepartmentName = d.DepartmentName AND dep.IsActive = 1
    WHERE dep.DepartmentId IS NULL
)
    THROW 51006, 'One or more requested active departments are missing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.Tender
    (
        TenderNumber,TrackingId,Title,Description,DepartmentId,EstimatedAmount,TenderType,
        CurrentStatus,SubmissionStartDate,SubmissionDeadline,PublicationDate,CreationToken,
        CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,IsDeleted,TenderCategory,WorkflowVersion,
        BudgetInformation,TechnicalSanctionNumber,TechnicalSanctionAuthority,TechnicalSanctionDate,
        PreBidMeetingUtc,PreBidLocation,PreBidDetails,TechnicalBidOpeningScheduledUtc,
        EligibilityCriteria,FinancialYear,EMDAmount,TenderDocumentFee,ProcessingFee,SecurityDepositAmount,
        PublicationScheduledUtc,FinancialBidOpeningScheduledUtc,TenderCategoryId,TenderMethod,
        WizardCurrentStep,WizardCompletedStepsMask
    )
    SELECT
        d.TenderNumber,d.TrackingId,d.Title,d.Description,dep.DepartmentId,d.EstimatedAmount,N'Online',
        d.CurrentStatus,DATEADD(DAY,-7,@Now),DATEADD(DAY,23,@Now),
        CASE WHEN d.IsPublished=1 THEN DATEADD(DAY,-1,@Now) ELSE NULL END,NEWID(),
        @JeUserId,DATEADD(DAY,-30,@Now),@JeUserId,@Now,0,N'30 Days',2,
        N'DEMO only; illustrative departmental budget allocation.',N'DEMO-TS-' + RIGHT(d.TenderNumber,3),
        N'Demo Municipal Engineer',CONVERT(date,DATEADD(DAY,-25,@Now)),
        DATEADD(DAY,5,@Now),N'DEMO project site',N'Demonstration pre-bid details only.',
        DATEADD(DAY,30,@Now),N'DEMO eligibility criteria; verify all real requirements before procurement.',
        @FinancialYear,CONVERT(decimal(18,2),d.EstimatedAmount * 0.02),500.00,100.00,
        CONVERT(decimal(18,2),d.EstimatedAmount * 0.05),DATEADD(DAY,-7,@Now),
        DATEADD(DAY,32,@Now),@TenderCategoryId,N'Online',0,127
    FROM @Seeds d
    INNER JOIN dbo.Departments dep ON dep.DepartmentName = d.DepartmentName AND dep.IsActive = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
        WHERE t.TenderNumber = d.TenderNumber
    );

    INSERT INTO dbo.TenderRequirements
    (
        TenderId,ScopeOfWork,TechnicalRequirements,FinancialRequirements,AdditionalConditions,
        TermsAndConditions,CreatedBy,CreatedAtUtc,ModifiedBy,ModifiedAtUtc
    )
    SELECT t.TenderId,
        N'DEMO scope: survey, carry out the described municipal works, maintain public safety, and submit completion evidence.',
        N'DEMO technical requirements: follow applicable municipal specifications, testing, inspection, and safety requirements.',
        N'DEMO financial requirements: all amounts are illustrative; quote against the approved BOQ and real tender terms.',
        N'DEMO only. Confirm location, quantities, approvals, and applicable permits before any real procurement.',
        N'DEMO only. No contractual offer or obligation is created by these sample terms.',
        @JeUserId,@Now,@JeUserId,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TenderRequirements r WHERE r.TenderId = t.TenderId);

    INSERT INTO dbo.BOQ(TenderId,ItemName,Description,Quantity,Unit,Rate,CreatedDate)
    SELECT t.TenderId,N'DEMO municipal works package',
        N'Illustrative lump-sum line matching the DEMO tender estimate; not an approved estimate.',
        1,N'Lump Sum',d.EstimatedAmount,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    WHERE NOT EXISTS (SELECT 1 FROM dbo.BOQ b WHERE b.TenderId = t.TenderId);

    INSERT INTO dbo.TenderDocuments
    (
        TenderId,DocumentType,FileName,FilePath,UploadedBy,UploadedDate,DocumentTypeId,
        DocumentName,Remarks,FileExtension,FileSizeBytes
    )
    SELECT t.TenderId,docs.DocumentType,docs.FileName,docs.FilePath,@JeUserId,@Now,
        docs.DocumentTypeId,docs.DocumentName,N'DEMO sample document; not a procurement record.',
        N'.txt',600
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    CROSS APPLY
    (
        SELECT N'Scope of Work' AS DocumentType,N'DEMO-Scope-of-Work.txt' AS FileName,
            N'Uploads/Demo/DEMO-Scope-of-Work.txt' AS FilePath,@ScopeDocumentTypeId AS DocumentTypeId,
            N'DEMO Scope of Work' AS DocumentName
        UNION ALL
        SELECT N'Technical Requirements',N'DEMO-Technical-Requirements.txt',
            N'Uploads/Demo/DEMO-Technical-Requirements.txt',@TechnicalDocumentTypeId,
            N'DEMO Technical Requirements'
        UNION ALL
        SELECT N'Financial Requirements',N'DEMO-Financial-Requirements.txt',
            N'Uploads/Demo/DEMO-Financial-Requirements.txt',@FinancialDocumentTypeId,
            N'DEMO Financial Requirements'
    ) docs
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TenderDocuments existing
        WHERE existing.TenderId = t.TenderId AND existing.DocumentTypeId = docs.DocumentTypeId
          AND existing.IsDeleted=0
    );

    INSERT INTO dbo.TenderRequiredBidDocuments
    (
        TenderId,DocumentType,CreatedBy,CreatedAtUtc,Description,IsMandatory,AllowedExtensions,MaxFileSizeBytes
    )
    SELECT t.TenderId,N'Business Registration',@JeUserId,@Now,
        N'DEMO bidder placeholder for registration documents.',1,N'pdf,txt',2097152
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TenderRequiredBidDocuments r
        WHERE r.TenderId = t.TenderId AND r.DocumentType = N'Business Registration'
    );

    INSERT INTO dbo.TenderAdministrativeApprovals
    (
        TenderId,ApprovalType,IsApproved,ApprovalReferenceNumber,ApprovalDate,ModifiedBy,ModifiedAtUtc
    )
    SELECT t.TenderId,approval.ApprovalType,d.ApprovalsReady,
        CASE WHEN d.ApprovalsReady=1
             THEN N'DEMO-' + RIGHT(d.TenderNumber,3) + N'-' + approval.ApprovalType
             ELSE NULL END,
        CASE WHEN d.ApprovalsReady=1 THEN CONVERT(date,DATEADD(DAY,-25,@Now)) ELSE NULL END,
        @JeUserId,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    CROSS APPLY
    (
        SELECT N'Administrative' AS ApprovalType
        UNION ALL SELECT N'TechnicalSanction'
        UNION ALL SELECT N'GeneralBody'
        UNION ALL SELECT N'DPDC'
    ) approval
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TenderAdministrativeApprovals existing
        WHERE existing.TenderId = t.TenderId AND existing.ApprovalType = approval.ApprovalType
    );

    INSERT INTO dbo.TenderGisLocations
    (
        TenderId,Latitude,Longitude,LocationName,ModifiedBy,ModifiedAtUtc
    )
    SELECT t.TenderId,CONVERT(decimal(9,6),19.0760 + (d.DemoNumber * 0.001)),
        CONVERT(decimal(9,6),72.8777 + (d.DemoNumber * 0.001)),
        N'DEMO approximate location - ' + d.DepartmentName,
        @JeUserId,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TenderGisLocations g WHERE g.TenderId = t.TenderId);

    INSERT INTO dbo.TenderPublicationSchedule
    (
        TenderId,PublicationStartUtc,OriginalClosingUtc,CurrentClosingUtc,
        PublicationPeriodDays,Remarks,ApprovedBy,ApprovedAtUtc,ModifiedAtUtc
    )
    SELECT t.TenderId,DATEADD(DAY,-7,@Now),DATEADD(DAY,23,@Now),DATEADD(DAY,23,@Now),
        30,N'DEMO publication dates; not a real tender notice.',@CellUserId,@Now,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TenderPublicationSchedule p WHERE p.TenderId = t.TenderId);

    INSERT INTO dbo.TenderDocumentShortfalls
    (
        TenderId,RaisedBy,RaisedAtUtc,DeadlineAtUtc,Remarks,RequiredDocuments,ResolutionStatus
    )
    SELECT t.TenderId,@CellUserId,@Now,DATEADD(DAY,3,@Now),
        N'DEMO shortfall: clarify and replace the sample scope document before resubmission.',
        N'DEMO Scope of Work clarification',N'Open'
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber = d.TenderNumber
    WHERE d.HasShortfall=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderDocumentShortfalls sf
          WHERE sf.TenderId=t.TenderId
      );

    INSERT INTO dbo.TenderRequirementVerificationHistory
    (
        TenderId,DocumentId,ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks,ReviewedAtUtc
    )
    SELECT t.TenderId,doc.DocumentId,N'JE',N'Verified',@JeUserId,@JeRoleId,
        N'DEMO initial JE review.',DATEADD(DAY,-2,@Now)
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.TenderDocuments doc ON doc.TenderId=t.TenderId AND doc.IsDeleted=0
    WHERE d.DemoNumber<>1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderRequirementVerificationHistory h
          WHERE h.TenderId=t.TenderId AND h.DocumentId=doc.DocumentId AND h.ReviewStage=N'JE'
      );

    INSERT INTO dbo.TenderRequirementVerificationHistory
    (
        TenderId,DocumentId,ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks,ReviewedAtUtc
    )
    SELECT t.TenderId,doc.DocumentId,N'TENDER_CELL',
        CASE WHEN d.HasShortfall=1 AND doc.DocumentType=N'Scope of Work' THEN N'Not Verified' ELSE N'Verified' END,
        @CellUserId,@CellRoleId,
        CASE WHEN d.HasShortfall=1 AND doc.DocumentType=N'Scope of Work'
             THEN N'DEMO: clarification requested.'
             ELSE N'DEMO Tender Cell review.' END,
        DATEADD(DAY,-1,@Now)
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.TenderDocuments doc ON doc.TenderId=t.TenderId AND doc.IsDeleted=0
    WHERE (d.HistoryThrough>=2 OR d.HasShortfall=1)
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderRequirementVerificationHistory h
          WHERE h.TenderId=t.TenderId AND h.DocumentId=doc.DocumentId AND h.ReviewStage=N'TENDER_CELL'
      );

    INSERT INTO dbo.TechnicalBid(TenderId,ContractorId,DocumentPath,Status,Remarks,SubmittedDate,TermsAcceptedAt,TermsVersion)
    SELECT t.TenderId,@ContractorId,N'Uploads/Demo/DEMO-Bidder-Registration.txt',N'Submitted',
        N'DEMO bidder submission only.',DATEADD(DAY,-1,@Now),DATEADD(DAY,-1,@Now),N'DEMO-v1'
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    WHERE d.HasTechnicalBid=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TechnicalBid b
          WHERE b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
      );

    INSERT INTO dbo.TechnicalBidDocuments(TechnicalBidId,FileName,FilePath,DocumentType,UploadedDate,UploadedBy)
    SELECT b.TechnicalBidId,N'DEMO-Bidder-Registration.txt',
        N'Uploads/Demo/DEMO-Bidder-Registration.txt',N'Business Registration',DATEADD(DAY,-1,@Now),NULL
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.TechnicalBid b ON b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
    WHERE d.HasTechnicalBid=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TechnicalBidDocuments doc
          WHERE doc.TechnicalBidId=b.TechnicalBidId AND doc.DocumentType=N'Business Registration'
      );

    INSERT INTO dbo.TenderDocumentVerificationHistory
    (
        TenderId,ContractorId,TechnicalBidDocumentId,FileName,FilePath,DocumentType,
        ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks,ReviewedAtUtc
    )
    SELECT t.TenderId,@ContractorId,doc.TechnicalBidDocumentId,doc.FileName,doc.FilePath,doc.DocumentType,
        review.ReviewStage,N'Verified',review.ReviewerId,review.ReviewerRoleId,
        N'DEMO bidder document review.',DATEADD(DAY,-1,@Now)
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.TechnicalBid b ON b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
    INNER JOIN dbo.TechnicalBidDocuments doc ON doc.TechnicalBidId=b.TechnicalBidId
    CROSS APPLY
    (
        SELECT N'Primary' AS ReviewStage,@JeUserId AS ReviewerId,@JeRoleId AS ReviewerRoleId
        UNION ALL
        SELECT N'Secondary',@CellUserId,@CellRoleId WHERE d.SecondaryReviewComplete=1
    ) review
    WHERE d.HasTechnicalBid=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderDocumentVerificationHistory existing
          WHERE existing.TechnicalBidDocumentId=doc.TechnicalBidDocumentId
            AND existing.ReviewStage=review.ReviewStage
      );

    INSERT INTO dbo.TenderVerificationReviews
    (
        TenderId,ContractorId,ReviewStage,Decision,ReviewerId,ReviewerRoleId,Remarks,ReviewedAtUtc
    )
    SELECT t.TenderId,@ContractorId,review.ReviewStage,N'Approved',
        review.ReviewerId,review.ReviewerRoleId,N'DEMO bidder eligibility review.',DATEADD(DAY,-1,@Now)
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    CROSS APPLY
    (
        SELECT N'Primary' AS ReviewStage,@JeUserId AS ReviewerId,@JeRoleId AS ReviewerRoleId
        UNION ALL
        SELECT N'Secondary',@CellUserId,@CellRoleId WHERE d.SecondaryReviewComplete=1
    ) review
    WHERE d.HasTechnicalBid=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderVerificationReviews existing
          WHERE existing.TenderId=t.TenderId AND existing.ContractorId=@ContractorId
            AND existing.ReviewStage=review.ReviewStage
      );

    INSERT INTO dbo.FinancialBid(TenderId,ContractorId,BidAmount,Rank,Status,SubmittedDate)
    SELECT t.TenderId,@ContractorId,CONVERT(decimal(18,2),d.EstimatedAmount * 0.96),1,N'Submitted',DATEADD(DAY,-1,@Now)
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    WHERE d.HasFinancialBids=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.FinancialBid b
          WHERE b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
      );

    INSERT INTO dbo.TenderFinancialBidOpening(TenderId,ActualOpeningUtc,OpenedBy,OpeningRemarks)
    SELECT t.TenderId,DATEADD(HOUR,-12,@Now),@JeUserId,N'DEMO financial bid opening record.'
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    WHERE d.HasFinancialBids=1
      AND NOT EXISTS (SELECT 1 FROM dbo.TenderFinancialBidOpening o WHERE o.TenderId=t.TenderId);

    INSERT INTO dbo.TenderFinancialBidOpeningDetails(FinancialBidOpeningId,FinancialBidId,QuotedAmount,Remarks)
    SELECT o.FinancialBidOpeningId,b.FinancialBidId,b.BidAmount,N'DEMO illustrative bid amount.'
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.TenderFinancialBidOpening o ON o.TenderId=t.TenderId
    INNER JOIN dbo.FinancialBid b ON b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
    WHERE d.HasFinancialBids=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderFinancialBidOpeningDetails detail
          WHERE detail.FinancialBidOpeningId=o.FinancialBidOpeningId
            AND detail.FinancialBidId=b.FinancialBidId
      );

    INSERT INTO dbo.TenderFinancialApprovals
    (
        TenderId,Decision,ApprovedAmount,ApprovedBy,ApprovedAtUtc,Remarks,SelectedFinancialBidId
    )
    SELECT t.TenderId,d.AuditDecision,
        CASE WHEN d.AuditDecision=N'Approved' THEN b.BidAmount ELSE NULL END,
        @AuditorUserId,DATEADD(DAY,-1,@Now),
        CASE WHEN d.AuditDecision=N'Approved'
             THEN N'DEMO financial approval; illustrative only.'
             ELSE N'DEMO returned: provide financial clarification before resubmission.' END,
        CASE WHEN d.AuditDecision=N'Approved' THEN b.FinancialBidId ELSE NULL END
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.FinancialBid b ON b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
    WHERE d.AuditDecision IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.TenderFinancialApprovals a WHERE a.TenderId=t.TenderId);

    INSERT INTO dbo.WorkOrder
    (
        TenderId,ContractorId,OrderNo,IssueDate,Amount,WorkDescription,
        TermsAndConditions,AuthorizedSignatory
    )
    SELECT t.TenderId,@ContractorId,N'DEMO-WO-' + RIGHT(d.TenderNumber,3),
        DATEADD(DAY,-1,@Now),b.BidAmount,N'DEMO work order for presentation only.',
        N'DEMO only; not an issued municipal order.',N'Demo Municipal Engineer'
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.FinancialBid b ON b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
    WHERE d.HasWorkOrder=1
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkOrder w WHERE w.TenderId=t.TenderId);

    INSERT INTO dbo.Agreement
    (
        TenderId,ContractorId,AgreementNo,AgreementDate,DocumentPath,ApprovedAmount,
        TermsAndConditions
    )
    SELECT t.TenderId,@ContractorId,N'DEMO-AG-' + RIGHT(d.TenderNumber,3),
        DATEADD(DAY,-1,@Now),N'Uploads/Demo/DEMO-Agreement.txt',b.BidAmount,
        N'DEMO only; no contractual obligation is created.'
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.FinancialBid b ON b.TenderId=t.TenderId AND b.ContractorId=@ContractorId
    WHERE d.HasAgreement=1
      AND NOT EXISTS (SELECT 1 FROM dbo.Agreement a WHERE a.TenderId=t.TenderId);

    INSERT INTO dbo.TenderGeneratedDocuments
    (
        TenderId,WorkOrderId,AgreementId,DocumentKind,VersionNumber,FileName,FilePath,GeneratedBy,GeneratedAtUtc
    )
    SELECT t.TenderId,w.WorkOrderId,NULL,N'WorkOrder',1,N'DEMO-Work-Order.txt',
        N'Uploads/Demo/DEMO-Work-Order.txt',@JeUserId,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.WorkOrder w ON w.TenderId=t.TenderId
    WHERE d.HasWorkOrder=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderGeneratedDocuments g
          WHERE g.TenderId=t.TenderId AND g.DocumentKind=N'WorkOrder'
      );

    INSERT INTO dbo.TenderGeneratedDocuments
    (
        TenderId,WorkOrderId,AgreementId,DocumentKind,VersionNumber,FileName,FilePath,GeneratedBy,GeneratedAtUtc
    )
    SELECT t.TenderId,NULL,a.AgreementId,N'Agreement',1,N'DEMO-Agreement.txt',
        N'Uploads/Demo/DEMO-Agreement.txt',@JeUserId,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.Agreement a ON a.TenderId=t.TenderId
    WHERE d.HasAgreement=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderGeneratedDocuments g
          WHERE g.TenderId=t.TenderId AND g.DocumentKind=N'Agreement'
      );

    INSERT INTO dbo.TenderGeneratedDocuments
    (
        TenderId,WorkOrderId,AgreementId,DocumentKind,VersionNumber,FileName,FilePath,GeneratedBy,GeneratedAtUtc
    )
    SELECT t.TenderId,NULL,NULL,N'BOQ',1,N'DEMO-Bill-of-Quantities.txt',
        N'Uploads/Demo/DEMO-Bill-of-Quantities.txt',@JeUserId,@Now
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    WHERE d.HasAgreement=1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderGeneratedDocuments g
          WHERE g.TenderId=t.TenderId AND g.DocumentKind=N'BOQ'
      );

    DECLARE @Steps TABLE
    (
        SequenceNumber int NOT NULL PRIMARY KEY,
        StageName nvarchar(80) NOT NULL,
        PreviousStatus nvarchar(100) NOT NULL,
        NewStatus nvarchar(100) NOT NULL,
        ActorRoleCode nvarchar(50) NOT NULL
    );

    INSERT INTO @Steps VALUES
    (1,N'Final_JE_SubmitTender',N'Draft',N'Pending Tender Cell Verification',N'JE'),
    (2,N'Final_Cell_InitialTenderDocumentVerification',N'Pending Tender Cell Verification',N'Submitted',N'TENDER_CELL'),
    (3,N'Final_JE_PublishTender',N'Submitted',N'Tender Published',N'JE'),
    (4,N'Final_JE_OpenTechnicalBids',N'Tender Published',N'Primary Verification Pending',N'JE'),
    (5,N'Final_JE_CompletePrimaryVerification',N'Primary Verification Pending',N'Primary Verification Completed',N'JE'),
    (6,N'Final_JE_ForwardToTenderCell',N'Primary Verification Completed',N'Tender Cell Verification Pending',N'JE'),
    (7,N'Final_Cell_CompleteSecondaryVerification',N'Tender Cell Verification Pending',N'Financial Bid Opening Approval Pending',N'TENDER_CELL'),
    (8,N'Final_Samiti_ApproveFinancialOpening',N'Financial Bid Opening Approval Pending',N'Financial Bid Opening Approved',N'CHAIRMAN'),
    (9,N'Final_JE_OpenFinancialBids',N'Financial Bid Opening Approved',N'Final Financial Approval Pending',N'JE'),
    (10,N'Final_Auditor_ApproveFinancialBids',N'Final Financial Approval Pending',N'Final Financial Approval Completed',N'DIRECTOR_AUDITOR'),
    (11,N'Final_JE_StartWorkOrder',N'Final Financial Approval Completed',N'Work Order Pending',N'JE'),
    (12,N'Final_JE_GenerateWorkOrder',N'Work Order Pending',N'Work Order Generated',N'JE'),
    (13,N'Final_JE_StartAgreement',N'Work Order Generated',N'Tender Agreement Pending',N'JE'),
    (14,N'Final_JE_GenerateAgreement',N'Tender Agreement Pending',N'Tender Agreement Generated',N'JE'),
    (15,N'Final_JE_CompleteTender',N'Tender Agreement Generated',N'Completed',N'JE');

    INSERT INTO dbo.TenderWorkflowHistory
    (
        TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,UserId,RoleId,
        DepartmentId,ActionAtUtc,Remarks,ActorRoleCode
    )
    SELECT t.TenderId,2,stage.StageId,step.PreviousStatus,step.NewStatus,
        N'DEMO_' + step.StageName + N'_' + RIGHT(d.TenderNumber,3),actor.UserId,role.RoleId,
        t.DepartmentId,DATEADD(MINUTE,step.SequenceNumber,DATEADD(DAY,-20,@Now)),
        N'DEMO workflow history seeded for presentation; not a live approval action.',
        step.ActorRoleCode
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN @Steps step ON step.SequenceNumber<=d.HistoryThrough
    INNER JOIN dbo.WorkflowStages stage
        ON stage.WorkflowVersion=2 AND stage.StageName=step.StageName AND stage.IsActive=1
    INNER JOIN dbo.Roles role ON role.RoleCode=step.ActorRoleCode AND role.IsActive=1
    INNER JOIN dbo.Users actor ON actor.RoleId=role.RoleId AND actor.IsActive=1
    WHERE (d.RouteVariant IN (0,2,3) OR step.SequenceNumber=1)
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderWorkflowHistory existing
          WHERE existing.TenderId=t.TenderId
            AND existing.ActionName=N'DEMO_' + step.StageName + N'_' + RIGHT(d.TenderNumber,3)
      );

    INSERT INTO dbo.TenderWorkflowHistory
    (
        TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,UserId,RoleId,
        DepartmentId,ActionAtUtc,Remarks,ActorRoleCode
    )
    SELECT t.TenderId,2,stage.StageId,N'Pending Tender Cell Verification',N'Pending JE Document Correction',
        N'DEMO_RaiseTenderDocumentShortfall_002',@CellUserId,@CellRoleId,t.DepartmentId,
        DATEADD(MINUTE,30,DATEADD(DAY,-20,@Now)),
        N'DEMO Tender Cell shortfall raised; three-day correction window is active.',N'TENDER_CELL'
    FROM dbo.Tender t
    INNER JOIN @Seeds d ON d.TenderNumber=t.TenderNumber AND d.RouteVariant=1
    INNER JOIN dbo.WorkflowStages stage
        ON stage.WorkflowVersion=2 AND stage.StageName=N'Final_Cell_InitialTenderDocumentVerification'
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TenderWorkflowHistory h
        WHERE h.TenderId=t.TenderId AND h.ActionName=N'DEMO_RaiseTenderDocumentShortfall_002'
    );

    INSERT INTO dbo.TenderWorkflowHistory
    (
        TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,UserId,RoleId,
        DepartmentId,ActionAtUtc,Remarks,ActorRoleCode
    )
    SELECT t.TenderId,2,stage.StageId,N'Financial Bid Opening Approval Pending',N'Financial Bid Opening Rejected',
        N'DEMO_ReturnFinancialOpening_013',@ChairmanUserId,@ChairmanRoleId,t.DepartmentId,
        DATEADD(MINUTE,40,DATEADD(DAY,-20,@Now)),
        N'DEMO return: Tender Cell re-verification is required.',N'CHAIRMAN'
    FROM dbo.Tender t
    INNER JOIN @Seeds d ON d.TenderNumber=t.TenderNumber AND d.RouteVariant=2
    INNER JOIN dbo.WorkflowStages stage
        ON stage.WorkflowVersion=2 AND stage.StageName=N'Final_Samiti_ApproveFinancialOpening'
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TenderWorkflowHistory h
        WHERE h.TenderId=t.TenderId AND h.ActionName=N'DEMO_ReturnFinancialOpening_013'
    );

    INSERT INTO dbo.TenderWorkflowHistory
    (
        TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,UserId,RoleId,
        DepartmentId,ActionAtUtc,Remarks,ActorRoleCode
    )
    SELECT t.TenderId,2,stage.StageId,N'Final Financial Approval Pending',N'Final Financial Approval Rejected',
        N'DEMO_ReturnFinancialDecision_011',@AuditorUserId,@AuditorRoleId,t.DepartmentId,
        DATEADD(MINUTE,50,DATEADD(DAY,-20,@Now)),
        N'DEMO return: provide financial clarification and resubmit.',N'DIRECTOR_AUDITOR'
    FROM dbo.Tender t
    INNER JOIN @Seeds d ON d.TenderNumber=t.TenderNumber AND d.RouteVariant=3
    INNER JOIN dbo.WorkflowStages stage
        ON stage.WorkflowVersion=2 AND stage.StageName=N'Final_Auditor_ApproveFinancialBids'
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TenderWorkflowHistory h
        WHERE h.TenderId=t.TenderId AND h.ActionName=N'DEMO_ReturnFinancialDecision_011'
    );

    INSERT INTO dbo.TenderWorkflowAssignments
    (
        TenderId,ShortfallId,StageId,AssignedRoleId,AssignedUserId,DepartmentId,
        AssignedAtUtc,CompletedAtUtc,IsCurrent
    )
    SELECT t.TenderId,NULL,stage.StageId,role.RoleId,actor.UserId,t.DepartmentId,
        DATEADD(MINUTE,-5,h.ActionAtUtc),h.ActionAtUtc,0
    FROM dbo.TenderWorkflowHistory h
    INNER JOIN dbo.Tender t ON t.TenderId=h.TenderId
    INNER JOIN @Seeds d ON d.TenderNumber=t.TenderNumber
    INNER JOIN dbo.WorkflowStages stage ON stage.StageId=h.StageId
    INNER JOIN dbo.Roles role ON role.RoleId=h.RoleId
    INNER JOIN dbo.Users actor ON actor.UserId=h.UserId
    WHERE h.ActionName LIKE N'DEMO[_]%'
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderWorkflowAssignments a
          WHERE a.TenderId=t.TenderId AND a.StageId=stage.StageId AND a.IsCurrent=0
      );

    INSERT INTO dbo.TenderWorkflowAssignments
    (
        TenderId,ShortfallId,StageId,AssignedRoleId,AssignedUserId,DepartmentId,AssignedAtUtc,IsCurrent
    )
    SELECT t.TenderId,
        CASE WHEN d.HasShortfall=1 THEN
            (SELECT TOP (1) sf.TenderDocumentShortfallId FROM dbo.TenderDocumentShortfalls sf
             WHERE sf.TenderId=t.TenderId AND sf.ResolutionStatus=N'Open'
             ORDER BY sf.RaisedAtUtc DESC,sf.TenderDocumentShortfallId DESC)
            ELSE NULL END,
        stage.StageId,stage.ApproverRoleId,actor.UserId,t.DepartmentId,DATEADD(HOUR,-1,@Now),1
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.WorkflowStages stage
        ON stage.WorkflowVersion=2 AND stage.StageName=d.StageName AND stage.IsActive=1
    INNER JOIN dbo.Users actor ON actor.RoleId=stage.ApproverRoleId AND actor.IsActive=1
    WHERE d.StageName<>N'Final_JE_CompleteTender'
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.TenderWorkflowAssignments a
          WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
      );

    IF EXISTS
    (
        SELECT 1
        FROM @Seeds d
        INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
        INNER JOIN dbo.WorkflowStages stage
            ON stage.WorkflowVersion=2 AND stage.StageName=d.StageName
        WHERE t.WorkflowVersion<>2
           OR (d.StageName<>N'Final_JE_CompleteTender' AND NOT EXISTS
               (SELECT 1 FROM dbo.TenderWorkflowAssignments a
                WHERE a.TenderId=t.TenderId AND a.IsCurrent=1))
    )
        THROW 51007, 'A demonstration tender could not be assigned to its current workflow stage.', 1;

    COMMIT TRANSACTION;

    SELECT d.TenderNumber,d.Title,dep.DepartmentName,d.CurrentStatus,d.StageName,
        role.RoleCode AS CurrentRole,
        CASE WHEN d.IsPublished=1 THEN N'DEMO published listing' ELSE N'Workflow demonstration' END AS DemoPurpose
    FROM @Seeds d
    INNER JOIN dbo.Tender t ON t.TenderNumber=d.TenderNumber
    INNER JOIN dbo.Departments dep ON dep.DepartmentId=t.DepartmentId
    INNER JOIN dbo.WorkflowStages stage ON stage.WorkflowVersion=2 AND stage.StageName=d.StageName
    INNER JOIN dbo.Roles role ON role.RoleId=stage.ApproverRoleId
    ORDER BY d.DemoNumber;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
