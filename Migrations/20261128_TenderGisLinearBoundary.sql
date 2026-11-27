SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderGisLocations', N'U') IS NULL
        THROW 51000, 'Apply 20261125_TenderCreationWizard.sql before this migration.', 1;

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryStartLatitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryStartLatitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryStartLongitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryStartLongitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryEndLatitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryEndLatitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryEndLongitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryEndLongitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryLineGeometryJson') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryLineGeometryJson NVARCHAR(MAX) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_LinearBoundaryCoordinates'
    )
        ALTER TABLE dbo.TenderGisLocations WITH CHECK
            ADD CONSTRAINT CK_TenderGisLocations_LinearBoundaryCoordinates
            CHECK
            (
                (BoundaryStartLatitude IS NULL AND BoundaryStartLongitude IS NULL
                 AND BoundaryEndLatitude IS NULL AND BoundaryEndLongitude IS NULL)
                OR
                (BoundaryStartLatitude BETWEEN -90 AND 90
                 AND BoundaryStartLongitude BETWEEN -180 AND 180
                 AND BoundaryEndLatitude BETWEEN -90 AND 90
                 AND BoundaryEndLongitude BETWEEN -180 AND 180)
            );

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_BoundaryLineJson'
    )
        ALTER TABLE dbo.TenderGisLocations WITH CHECK
            ADD CONSTRAINT CK_TenderGisLocations_BoundaryLineJson
            CHECK(BoundaryLineGeometryJson IS NULL OR ISJSON(BoundaryLineGeometryJson)=1);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
