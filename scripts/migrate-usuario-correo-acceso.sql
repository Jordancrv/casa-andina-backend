-- Agrega el identificador de acceso a Usuario, separado de PersonaContacto.
-- Rellena cuentas existentes usando un correo principal de contacto.
-- Abortara antes de confirmar si alguna cuenta no tiene correo principal o si hay duplicados.
-- Ejecutar en la base objetivo despues de contar con un respaldo verificable.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
        THROW 51100, 'No existe dbo.Usuario en esta base de datos.', 1;
    IF OBJECT_ID(N'dbo.PersonaContacto', N'U') IS NULL
        THROW 51101, 'No existe dbo.PersonaContacto para migrar los correos de acceso.', 1;

    IF COL_LENGTH(N'dbo.Usuario', N'Correo') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Usuario ADD Correo NVARCHAR(150) NULL;';

    EXEC sys.sp_executesql N'
        UPDATE usuario
        SET Correo = LOWER(LTRIM(RTRIM(contacto.Valor)))
        FROM dbo.Usuario usuario
        CROSS APPLY (
            SELECT TOP (1) pc.Valor
            FROM dbo.PersonaContacto pc
            WHERE pc.PersonaId = usuario.PersonaId
              AND pc.TipoContacto = N''Correo''
              AND pc.EsPrincipal = 1
              AND LEN(LTRIM(RTRIM(pc.Valor))) > 0
            ORDER BY pc.PersonaContactoId
        ) contacto
        WHERE usuario.Correo IS NULL OR LEN(LTRIM(RTRIM(usuario.Correo))) = 0;';

    EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.Usuario WHERE Correo IS NULL OR LEN(LTRIM(RTRIM(Correo))) = 0)
            THROW 51102, ''Hay usuarios sin correo principal de acceso. Corrija esos datos y vuelva a ejecutar.'', 1;';

    EXEC sys.sp_executesql N'
        IF EXISTS (
            SELECT LOWER(LTRIM(RTRIM(Correo))) FROM dbo.Usuario
            GROUP BY LOWER(LTRIM(RTRIM(Correo))) HAVING COUNT(*) > 1
        )
            THROW 51103, ''Hay correos de acceso duplicados entre usuarios. Resuelva los duplicados antes de continuar.'', 1;';

    EXEC sys.sp_executesql N'
        UPDATE dbo.Usuario
        SET Correo = LOWER(LTRIM(RTRIM(Correo)))
        WHERE Correo COLLATE Latin1_General_100_BIN2 <>
              LOWER(LTRIM(RTRIM(Correo))) COLLATE Latin1_General_100_BIN2;
        ALTER TABLE dbo.Usuario ALTER COLUMN Correo NVARCHAR(150) NOT NULL;';

    IF EXISTS (
        SELECT 1 FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(N'dbo.Usuario')
          AND i.name = N'UX_Usuario_Correo'
          AND (i.is_unique = 0 OR i.has_filter = 1
               OR (SELECT COUNT(*) FROM sys.index_columns ic
                   WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                     AND ic.key_ordinal > 0) <> 1
               OR NOT EXISTS (
                   SELECT 1 FROM sys.index_columns ic
                   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                     AND ic.key_ordinal = 1 AND c.name = N'Correo'
               ))
    )
        THROW 51104, 'UX_Usuario_Correo existe con una definicion incompatible.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Usuario')
          AND name = N'UX_Usuario_Correo'
    )
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_Usuario_Correo ON dbo.Usuario(Correo);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
