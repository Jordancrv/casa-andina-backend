-- Normalización de personas, documentos, contactos y huéspedes de reservas.
-- Migración aditiva y reejecutable. Conserva las columnas antiguas de Cliente
-- durante la transición del frontend y backend al nuevo modelo.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TipoDocumento', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TipoDocumento (
            TipoDocumentoId INT IDENTITY(1,1) NOT NULL,
            Codigo NVARCHAR(20) NOT NULL,
            Nombre NVARCHAR(80) NOT NULL,
            TipoPersonaPermitida NCHAR(1) NOT NULL,
            LongitudMinima TINYINT NOT NULL,
            LongitudMaxima TINYINT NOT NULL,
            Activo BIT NOT NULL CONSTRAINT DF_TipoDocumento_Activo DEFAULT (1),
            CONSTRAINT PK_TipoDocumento PRIMARY KEY (TipoDocumentoId),
            CONSTRAINT UQ_TipoDocumento_Codigo UNIQUE (Codigo),
            CONSTRAINT CK_TipoDocumento_TipoPersona CHECK (TipoPersonaPermitida IN (N'N', N'J', N'A')),
            CONSTRAINT CK_TipoDocumento_Longitud CHECK (LongitudMinima > 0 AND LongitudMaxima >= LongitudMinima)
        );
    END;

    MERGE dbo.TipoDocumento AS target
    USING (VALUES
        (N'DNI', N'Documento Nacional de Identidad', N'N', 8, 8),
        (N'CE', N'Carné de Extranjería', N'N', 9, 12),
        (N'PASAPORTE', N'Pasaporte', N'N', 6, 20),
        (N'RUC', N'Registro Único de Contribuyentes', N'J', 11, 11)
    ) AS source (Codigo, Nombre, TipoPersonaPermitida, LongitudMinima, LongitudMaxima)
    ON target.Codigo = source.Codigo
    WHEN MATCHED THEN UPDATE SET
        Nombre = source.Nombre,
        TipoPersonaPermitida = source.TipoPersonaPermitida,
        LongitudMinima = source.LongitudMinima,
        LongitudMaxima = source.LongitudMaxima,
        Activo = 1
    WHEN NOT MATCHED THEN
        INSERT (Codigo, Nombre, TipoPersonaPermitida, LongitudMinima, LongitudMaxima)
        VALUES (source.Codigo, source.Nombre, source.TipoPersonaPermitida, source.LongitudMinima, source.LongitudMaxima);

    IF OBJECT_ID(N'dbo.Persona', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Persona (
            PersonaId INT IDENTITY(1,1) NOT NULL,
            TipoPersona NCHAR(1) NOT NULL,
            Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_Persona_Estado DEFAULT (N'Activo'),
            FechaRegistro DATETIME2 NOT NULL CONSTRAINT DF_Persona_FechaRegistro DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_Persona PRIMARY KEY (PersonaId),
            CONSTRAINT CK_Persona_Tipo CHECK (TipoPersona IN (N'N', N'J')),
            CONSTRAINT CK_Persona_Estado CHECK (Estado IN (N'Activo', N'Inactivo'))
        );
    END;

    IF OBJECT_ID(N'dbo.PersonaNatural', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PersonaNatural (
            PersonaId INT NOT NULL,
            Nombres NVARCHAR(100) NOT NULL,
            Apellidos NVARCHAR(100) NULL,
            FechaNacimiento DATE NULL,
            Nacionalidad NVARCHAR(60) NULL,
            CONSTRAINT PK_PersonaNatural PRIMARY KEY (PersonaId),
            CONSTRAINT FK_PersonaNatural_Persona FOREIGN KEY (PersonaId)
                REFERENCES dbo.Persona (PersonaId)
        );
    END;

    IF OBJECT_ID(N'dbo.PersonaJuridica', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PersonaJuridica (
            PersonaId INT NOT NULL,
            RazonSocial NVARCHAR(200) NOT NULL,
            NombreComercial NVARCHAR(150) NULL,
            CONSTRAINT PK_PersonaJuridica PRIMARY KEY (PersonaId),
            CONSTRAINT FK_PersonaJuridica_Persona FOREIGN KEY (PersonaId)
                REFERENCES dbo.Persona (PersonaId)
        );
    END;

    IF OBJECT_ID(N'dbo.PersonaDocumento', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PersonaDocumento (
            PersonaDocumentoId INT IDENTITY(1,1) NOT NULL,
            PersonaId INT NOT NULL,
            TipoDocumentoId INT NOT NULL,
            Numero NVARCHAR(20) NOT NULL,
            PaisEmision NVARCHAR(60) NULL,
            FechaVencimiento DATE NULL,
            EsPrincipal BIT NOT NULL CONSTRAINT DF_PersonaDocumento_Principal DEFAULT (0),
            CONSTRAINT PK_PersonaDocumento PRIMARY KEY (PersonaDocumentoId),
            CONSTRAINT FK_PersonaDocumento_Persona FOREIGN KEY (PersonaId)
                REFERENCES dbo.Persona (PersonaId),
            CONSTRAINT FK_PersonaDocumento_Tipo FOREIGN KEY (TipoDocumentoId)
                REFERENCES dbo.TipoDocumento (TipoDocumentoId),
            CONSTRAINT UQ_PersonaDocumento_TipoNumero UNIQUE (TipoDocumentoId, Numero)
        );
        CREATE UNIQUE INDEX UX_PersonaDocumento_Principal
            ON dbo.PersonaDocumento (PersonaId) WHERE EsPrincipal = 1;
    END;

    IF OBJECT_ID(N'dbo.PersonaCorreo', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PersonaCorreo (
            PersonaCorreoId INT IDENTITY(1,1) NOT NULL,
            PersonaId INT NOT NULL,
            Correo NVARCHAR(150) NOT NULL,
            EsPrincipal BIT NOT NULL CONSTRAINT DF_PersonaCorreo_Principal DEFAULT (0),
            Verificado BIT NOT NULL CONSTRAINT DF_PersonaCorreo_Verificado DEFAULT (0),
            CONSTRAINT PK_PersonaCorreo PRIMARY KEY (PersonaCorreoId),
            CONSTRAINT FK_PersonaCorreo_Persona FOREIGN KEY (PersonaId)
                REFERENCES dbo.Persona (PersonaId),
            CONSTRAINT UQ_PersonaCorreo_Correo UNIQUE (Correo)
        );
        CREATE UNIQUE INDEX UX_PersonaCorreo_Principal
            ON dbo.PersonaCorreo (PersonaId) WHERE EsPrincipal = 1;
    END;

    IF OBJECT_ID(N'dbo.PersonaTelefono', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PersonaTelefono (
            PersonaTelefonoId INT IDENTITY(1,1) NOT NULL,
            PersonaId INT NOT NULL,
            Numero NVARCHAR(20) NOT NULL,
            EsPrincipal BIT NOT NULL CONSTRAINT DF_PersonaTelefono_Principal DEFAULT (0),
            CONSTRAINT PK_PersonaTelefono PRIMARY KEY (PersonaTelefonoId),
            CONSTRAINT FK_PersonaTelefono_Persona FOREIGN KEY (PersonaId)
                REFERENCES dbo.Persona (PersonaId),
            CONSTRAINT UQ_PersonaTelefono_PersonaNumero UNIQUE (PersonaId, Numero)
        );
        CREATE UNIQUE INDEX UX_PersonaTelefono_Principal
            ON dbo.PersonaTelefono (PersonaId) WHERE EsPrincipal = 1;
    END;

    IF COL_LENGTH(N'dbo.Cliente', N'PersonaId') IS NULL
        ALTER TABLE dbo.Cliente ADD PersonaId INT NULL;

    CREATE TABLE #ClientePersona (ClienteId INT PRIMARY KEY, PersonaId INT NOT NULL);

    EXEC sys.sp_executesql N'
        MERGE dbo.Persona AS target
        USING (
            SELECT ClienteId
            FROM dbo.Cliente
            WHERE PersonaId IS NULL
        ) AS source
        ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (TipoPersona, Estado, FechaRegistro)
            VALUES (N''N'', N''Activo'', SYSUTCDATETIME())
        OUTPUT source.ClienteId, inserted.PersonaId
            INTO #ClientePersona (ClienteId, PersonaId);';

    INSERT INTO dbo.PersonaNatural (PersonaId, Nombres)
    SELECT mapping.PersonaId, cliente.NombreCompleto
    FROM #ClientePersona mapping
    INNER JOIN dbo.Cliente cliente ON cliente.ClienteId = mapping.ClienteId;

    INSERT INTO dbo.PersonaDocumento (PersonaId, TipoDocumentoId, Numero, PaisEmision, EsPrincipal)
    SELECT mapping.PersonaId, tipo.TipoDocumentoId, cliente.DNI, N'Perú', 1
    FROM #ClientePersona mapping
    INNER JOIN dbo.Cliente cliente ON cliente.ClienteId = mapping.ClienteId
    INNER JOIN dbo.TipoDocumento tipo ON tipo.Codigo = N'DNI'
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.PersonaDocumento documento
        WHERE documento.TipoDocumentoId = tipo.TipoDocumentoId
          AND documento.Numero = cliente.DNI
    );

    INSERT INTO dbo.PersonaCorreo (PersonaId, Correo, EsPrincipal)
    SELECT mapping.PersonaId, LOWER(LTRIM(RTRIM(cliente.Correo))), 1
    FROM #ClientePersona mapping
    INNER JOIN dbo.Cliente cliente ON cliente.ClienteId = mapping.ClienteId
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.PersonaCorreo correo
        WHERE correo.Correo = LOWER(LTRIM(RTRIM(cliente.Correo)))
    );

    INSERT INTO dbo.PersonaTelefono (PersonaId, Numero, EsPrincipal)
    SELECT mapping.PersonaId, cliente.Telefono, 1
    FROM #ClientePersona mapping
    INNER JOIN dbo.Cliente cliente ON cliente.ClienteId = mapping.ClienteId
    WHERE cliente.Telefono IS NOT NULL
      AND LTRIM(RTRIM(cliente.Telefono)) <> N'';

    EXEC sys.sp_executesql N'
        UPDATE cliente
        SET PersonaId = mapping.PersonaId
        FROM dbo.Cliente cliente
        INNER JOIN #ClientePersona mapping ON mapping.ClienteId = cliente.ClienteId;

        IF NOT EXISTS (SELECT 1 FROM dbo.Cliente WHERE PersonaId IS NULL)
           AND EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N''dbo.Cliente'')
                  AND name = N''PersonaId''
                  AND is_nullable = 1)
            ALTER TABLE dbo.Cliente ALTER COLUMN PersonaId INT NOT NULL;';

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_Cliente_Persona')
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Cliente ADD CONSTRAINT FK_Cliente_Persona
            FOREIGN KEY (PersonaId) REFERENCES dbo.Persona (PersonaId);';

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Cliente')
          AND name = N'UQ_Cliente_Persona')
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UQ_Cliente_Persona ON dbo.Cliente (PersonaId);';

    IF OBJECT_ID(N'dbo.ReservaHuesped', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ReservaHuesped (
            ReservaId INT NOT NULL,
            PersonaId INT NOT NULL,
            EsTitular BIT NOT NULL CONSTRAINT DF_ReservaHuesped_Titular DEFAULT (0),
            Observacion NVARCHAR(250) NULL,
            CONSTRAINT PK_ReservaHuesped PRIMARY KEY (ReservaId, PersonaId),
            CONSTRAINT FK_ReservaHuesped_Reserva FOREIGN KEY (ReservaId)
                REFERENCES dbo.Reserva (ReservaId),
            CONSTRAINT FK_ReservaHuesped_PersonaNatural FOREIGN KEY (PersonaId)
                REFERENCES dbo.PersonaNatural (PersonaId)
        );
        CREATE UNIQUE INDEX UX_ReservaHuesped_Titular
            ON dbo.ReservaHuesped (ReservaId) WHERE EsTitular = 1;
    END;

    EXEC sys.sp_executesql N'
        INSERT INTO dbo.ReservaHuesped (ReservaId, PersonaId, EsTitular)
        SELECT reserva.ReservaId, cliente.PersonaId, 1
        FROM dbo.Reserva reserva
        INNER JOIN dbo.Cliente cliente ON cliente.ClienteId = reserva.ClienteId
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.ReservaHuesped huesped
            WHERE huesped.ReservaId = reserva.ReservaId
        );';

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ReservaHuesped')
          AND name = N'IX_ReservaHuesped_Persona')
        CREATE INDEX IX_ReservaHuesped_Persona ON dbo.ReservaHuesped (PersonaId, ReservaId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
