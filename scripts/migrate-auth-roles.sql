-- Actualización no destructiva para bases creadas antes de incorporar Rol.Codigo.
-- No es necesaria cuando se vuelve a ejecutar el DDL completo del proyecto.

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Rol', N'Codigo') IS NULL
        ALTER TABLE dbo.Rol ADD Codigo NVARCHAR(30) NULL;

    UPDATE dbo.Rol
    SET Codigo = CASE Nombre
        WHEN N'Administrador' THEN N'ADMINISTRADOR'
        WHEN N'Recepción' THEN N'RECEPCION'
        WHEN N'Recepcion' THEN N'RECEPCION'
        WHEN N'Operaciones' THEN N'OPERACIONES'
        WHEN N'Mantenimiento' THEN N'MANTENIMIENTO'
        ELSE Codigo
    END
    WHERE Codigo IS NULL;

    IF EXISTS (SELECT 1 FROM dbo.Rol WHERE Codigo IS NULL)
        THROW 51010, 'Existen roles sin un código reconocido. Corríjalos antes de continuar.', 1;

    ALTER TABLE dbo.Rol ALTER COLUMN Codigo NVARCHAR(30) NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_Rol_Codigo'
          AND parent_object_id = OBJECT_ID(N'dbo.Rol'))
    BEGIN
        ALTER TABLE dbo.Rol
            ADD CONSTRAINT UQ_Rol_Codigo UNIQUE (Codigo);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
