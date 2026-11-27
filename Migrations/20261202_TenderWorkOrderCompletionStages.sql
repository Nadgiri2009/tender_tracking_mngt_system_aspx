SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WorkflowStages', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowAssignments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Roles', N'U') IS NULL
        THROW 51030, 'Apply the version-2 tender workflow migrations before this migration.', 1;

    DECLARE @JeRoleId INT;
    SELECT @JeRoleId=RoleId
    FROM dbo.Roles
    WHERE RoleCode=N'JE' AND IsActive=1;

    IF @JeRoleId IS NULL
        THROW 51031, 'The active Junior Engineer role is required.', 1;

    IF NOT EXISTS
       (SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateWorkOrder') OR
       NOT EXISTS
       (SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateAgreement')
        THROW 51034, 'The existing work-order and agreement generation stages are required.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.TenderWorkflowAssignments a
        INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
        WHERE a.IsCurrent=1 AND s.WorkflowVersion=2
          AND s.StageName IN
              (N'Final_JE_GenerateWorkOrder',N'Final_JE_GenerateAgreement',
               N'Final_JE_StartWorkOrder',N'Final_JE_StartAgreement',
               N'Final_JE_CompleteTender')
    )
        THROW 51032, 'An active work-order or agreement stage exists; review it before changing stage routing.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageOrder BETWEEN 111 AND 115
          AND StageName NOT IN
              (N'Final_JE_GenerateWorkOrder',N'Final_JE_GenerateAgreement',
               N'Final_JE_StartWorkOrder',N'Final_JE_StartAgreement',
               N'Final_JE_CompleteTender')
    )
        THROW 51033, 'Workflow stage orders 111-115 are occupied by unrelated stages.', 1;

    UPDATE dbo.WorkflowStages
    SET StageOrder=10000+StageId
    WHERE WorkflowVersion=2
      AND StageName IN (N'Final_JE_GenerateWorkOrder',N'Final_JE_GenerateAgreement');

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_StartWorkOrder'
    )
        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (111,N'Final_JE_StartWorkOrder',N'Final Financial Approval Completed',
             N'Work Order Pending',N'Final Financial Approval Completed',@JeRoleId,1,2);

    UPDATE dbo.WorkflowStages
    SET StageOrder=112,StatusOnPending=N'Work Order Pending',
        StatusOnApprove=N'Work Order Generated',StatusOnReject=N'Work Order Pending',
        ApproverRoleId=@JeRoleId,IsActive=1
    WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateWorkOrder';

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_StartAgreement'
    )
        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (113,N'Final_JE_StartAgreement',N'Work Order Generated',
             N'Tender Agreement Pending',N'Work Order Generated',@JeRoleId,1,2);

    UPDATE dbo.WorkflowStages
    SET StageOrder=114,StatusOnPending=N'Tender Agreement Pending',
        StatusOnApprove=N'Tender Agreement Generated',StatusOnReject=N'Tender Agreement Pending',
        ApproverRoleId=@JeRoleId,IsActive=1
    WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateAgreement';

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_CompleteTender'
    )
        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (115,N'Final_JE_CompleteTender',N'Tender Agreement Generated',
             N'Completed',N'Tender Agreement Generated',@JeRoleId,1,2);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
