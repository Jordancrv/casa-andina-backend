-- CRM: sede de alta manual. No cambia perfiles ni reservas existentes.
-- Ejecutar en la base del proyecto antes de desplegar la nueva API.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Cliente', N'U') IS NULL OR OBJECT_ID(N'dbo.Sede', N'U') IS NULL
        THROW 51000, 'Se requieren las tablas Cliente y Sede del modelo normalizado.', 1;

    IF COL_LENGTH(N'dbo.Cliente', N'SedeRegistroId') IS NULL
        ALTER TABLE dbo.Cliente ADD SedeRegistroId INT NULL;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cliente')
        AND name = N'SedeRegistroId' AND (system_type_id <> TYPE_ID(N'int') OR is_nullable <> 1))
        THROW 51001, 'SedeRegistroId debe ser INT NULL. Revise el esquema antes de continuar.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cliente_SedeRegistro')
        EXEC(N'ALTER TABLE dbo.Cliente WITH CHECK ADD CONSTRAINT FK_Cliente_SedeRegistro
            FOREIGN KEY (SedeRegistroId) REFERENCES dbo.Sede(SedeId);');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Cliente') AND name = N'IX_Cliente_SedeRegistroId')
        EXEC(N'CREATE INDEX IX_Cliente_SedeRegistroId ON dbo.Cliente(SedeRegistroId);');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
