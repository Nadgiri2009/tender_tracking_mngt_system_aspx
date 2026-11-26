SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderGisLocations', N'U') IS NULL
        THROW 51000, 'Apply 20261125_TenderCreationWizard.sql before this migration.', 1;

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryCoordinatesJson') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryCoordinatesJson NVARCHAR(MAX) NULL;

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'SelectedAreaSquareMeters') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD SelectedAreaSquareMeters DECIMAL(18,2) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_SelectedArea'
    )
        ALTER TABLE dbo.TenderGisLocations WITH CHECK
            ADD CONSTRAINT CK_TenderGisLocations_SelectedArea
            CHECK(SelectedAreaSquareMeters IS NULL OR SelectedAreaSquareMeters > 0);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_BoundaryJson'
    )
        ALTER TABLE dbo.TenderGisLocations WITH CHECK
            ADD CONSTRAINT CK_TenderGisLocations_BoundaryJson
            CHECK(BoundaryCoordinatesJson IS NULL OR ISJSON(BoundaryCoordinatesJson)=1);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
