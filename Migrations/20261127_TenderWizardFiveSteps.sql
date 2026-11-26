SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL OR
       COL_LENGTH(N'dbo.Tender', N'WizardCurrentStep') IS NULL OR
       COL_LENGTH(N'dbo.Tender', N'WizardCompletedStepsMask') IS NULL
        THROW 51000, 'Apply the tender wizard migrations before this migration.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.Tender')
          AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Tender'), N'WizardCurrentStep', 'ColumnId')
          AND name = N'SMC_TenderWizardFiveStepVersion'
    )
    BEGIN
        UPDATE dbo.Tender
        SET WizardCurrentStep = CASE
                WHEN WizardCurrentStep <= 2 THEN 1
                ELSE WizardCurrentStep - 1
            END,
            WizardCompletedStepsMask =
                (WizardCompletedStepsMask & 1) |
                ((WizardCompletedStepsMask & 60) / 2);

        EXEC sys.sp_addextendedproperty
            @name = N'SMC_TenderWizardFiveStepVersion',
            @value = N'1',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE', @level1name = N'Tender',
            @level2type = N'COLUMN', @level2name = N'WizardCurrentStep';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
