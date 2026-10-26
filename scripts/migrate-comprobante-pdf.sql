-- Datos de emisión inmutables para PDF de reservas pagadas.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Comprobante', N'U') IS NULL
        THROW 51000, 'Se requiere Comprobante del modelo normalizado.', 1;
    IF COL_LENGTH(N'dbo.Comprobante', N'DatosEmisionJson') IS NULL
        ALTER TABLE dbo.Comprobante ADD DatosEmisionJson NVARCHAR(MAX) NULL;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Comprobante')
        AND name=N'DatosEmisionJson' AND (system_type_id <> TYPE_ID(N'nvarchar') OR max_length <> -1 OR is_nullable <> 1))
        THROW 51001, 'DatosEmisionJson debe ser NVARCHAR(MAX) NULL.', 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
