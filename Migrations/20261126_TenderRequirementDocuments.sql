SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.DocumentTypes', N'U') IS NULL OR
       COL_LENGTH(N'dbo.DocumentTypes', N'DocumentTypeId') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'DocumentTypeId') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'DocumentName') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'FileExtension') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'FileSizeBytes') IS NULL
        THROW 51000, 'Apply 20261125_TenderDocumentManagement.sql before this migration.', 1;

    DECLARE @RequirementTypes TABLE
    (
        TypeName NVARCHAR(80) NOT NULL PRIMARY KEY,
        SortOrder INT NOT NULL
    );

    INSERT INTO @RequirementTypes(TypeName, SortOrder)
    VALUES (N'Scope of Work', 20),
           (N'Technical Requirements', 21),
           (N'Financial Requirements', 22);

    UPDATE existingType
    SET IsActive=1,SortOrder=requirementType.SortOrder
    FROM dbo.DocumentTypes existingType
    INNER JOIN @RequirementTypes requirementType
        ON requirementType.TypeName=existingType.TypeName;

    INSERT INTO dbo.DocumentTypes(TypeName,SortOrder,IsActive)
    SELECT requirementType.TypeName,requirementType.SortOrder,1
    FROM @RequirementTypes requirementType
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.DocumentTypes existingType WITH (UPDLOCK,HOLDLOCK)
        WHERE existingType.TypeName=requirementType.TypeName
    );

    UPDATE dbo.Tender
    SET WizardCurrentStep=CASE
            WHEN WizardCurrentStep>5 THEN WizardCurrentStep-1
            ELSE WizardCurrentStep
        END,
        WizardCompletedStepsMask=
            (WizardCompletedStepsMask & 15) |
            CASE WHEN (WizardCompletedStepsMask & 32)<>0 THEN 16 ELSE 0 END |
            CASE WHEN (WizardCompletedStepsMask & 64)<>0 THEN 32 ELSE 0 END
    WHERE WizardCurrentStep>6 OR (WizardCompletedStepsMask & 64)<>0;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
