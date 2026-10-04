-- Normaliza la reserva para permitir varias habitaciones y huéspedes por habitación.
-- Migración aditiva y reejecutable; conserva las columnas antiguas durante el despliegue.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Usuario', N'PersonaId') IS NULL
        ALTER TABLE dbo.Usuario ADD PersonaId INT NULL;

    CREATE TABLE #UsuarioPersona (UsuarioId INT PRIMARY KEY, PersonaId INT NOT NULL);
    CREATE TABLE #UsuariosNuevasPersonas (UsuarioId INT PRIMARY KEY, PersonaId INT NOT NULL);

    EXEC sys.sp_executesql N'
    INSERT INTO #UsuarioPersona (UsuarioId, PersonaId)
    SELECT usuario.UsuarioId, correo.PersonaId
    FROM dbo.Usuario usuario
    INNER JOIN dbo.PersonaCorreo correo
        ON LOWER(LTRIM(RTRIM(correo.Correo))) = LOWER(LTRIM(RTRIM(usuario.Correo)))
    WHERE usuario.PersonaId IS NULL;

    MERGE dbo.Persona AS destino
    USING (
        SELECT usuario.UsuarioId, usuario.Nombre, usuario.Correo
        FROM dbo.Usuario usuario
        WHERE usuario.PersonaId IS NULL
          AND NOT EXISTS (SELECT 1 FROM #UsuarioPersona vinculo WHERE vinculo.UsuarioId = usuario.UsuarioId)
    ) AS origen
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT (TipoPersona, Estado, FechaRegistro)
        VALUES (N''N'', N''Activo'', SYSUTCDATETIME())
    OUTPUT origen.UsuarioId, inserted.PersonaId
        INTO #UsuariosNuevasPersonas (UsuarioId, PersonaId);

    INSERT INTO dbo.PersonaNatural (PersonaId, Nombres)
    SELECT nuevas.PersonaId, LEFT(LTRIM(RTRIM(usuario.Nombre)), 100)
    FROM #UsuariosNuevasPersonas nuevas
    INNER JOIN dbo.Usuario usuario ON usuario.UsuarioId = nuevas.UsuarioId;

    INSERT INTO dbo.PersonaCorreo (PersonaId, Correo, EsPrincipal)
    SELECT nuevas.PersonaId, LOWER(LTRIM(RTRIM(usuario.Correo))), 1
    FROM #UsuariosNuevasPersonas nuevas
    INNER JOIN dbo.Usuario usuario ON usuario.UsuarioId = nuevas.UsuarioId
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.PersonaCorreo correo
        WHERE LOWER(LTRIM(RTRIM(correo.Correo))) = LOWER(LTRIM(RTRIM(usuario.Correo)))
    );

    INSERT INTO #UsuarioPersona (UsuarioId, PersonaId)
    SELECT UsuarioId, PersonaId FROM #UsuariosNuevasPersonas;

    UPDATE usuario
    SET PersonaId = vinculo.PersonaId
    FROM dbo.Usuario usuario
    INNER JOIN #UsuarioPersona vinculo ON vinculo.UsuarioId = usuario.UsuarioId;

    IF EXISTS (SELECT 1 FROM dbo.Usuario WHERE PersonaId IS NULL)
        THROW 51020, ''No se pudo asociar todos los usuarios con una persona.'', 1;

    ALTER TABLE dbo.Usuario ALTER COLUMN PersonaId INT NOT NULL;
    ';

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Usuario_Persona')
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Usuario ADD CONSTRAINT FK_Usuario_Persona
            FOREIGN KEY (PersonaId) REFERENCES dbo.Persona (PersonaId);';

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Usuario') AND name = N'UX_Usuario_Persona')
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_Usuario_Persona ON dbo.Usuario (PersonaId);';

    IF OBJECT_ID(N'dbo.Huesped', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Huesped (
            HuespedId INT IDENTITY(1,1) NOT NULL,
            PersonaId INT NOT NULL,
            FechaRegistro DATETIME2 NOT NULL CONSTRAINT DF_Huesped_FechaRegistro DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_Huesped PRIMARY KEY (HuespedId),
            CONSTRAINT UQ_Huesped_Persona UNIQUE (PersonaId),
            CONSTRAINT FK_Huesped_PersonaNatural FOREIGN KEY (PersonaId)
                REFERENCES dbo.PersonaNatural (PersonaId)
        );
    END;

    IF OBJECT_ID(N'dbo.ReservaHabitacion', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ReservaHabitacion (
            ReservaHabitacionId INT IDENTITY(1,1) NOT NULL,
            ReservaId INT NOT NULL,
            HabitacionId INT NOT NULL,
            FechaCheckIn DATE NOT NULL,
            FechaCheckOut DATE NOT NULL,
            NumAdultos TINYINT NOT NULL CONSTRAINT DF_ReservaHabitacion_Adultos DEFAULT (1),
            NumNinos TINYINT NOT NULL CONSTRAINT DF_ReservaHabitacion_Ninos DEFAULT (0),
            PrecioNoche DECIMAL(10,2) NULL,
            PrecioTotal DECIMAL(10,2) NOT NULL,
            CONSTRAINT PK_ReservaHabitacion PRIMARY KEY (ReservaHabitacionId),
            CONSTRAINT FK_ReservaHabitacion_Reserva FOREIGN KEY (ReservaId)
                REFERENCES dbo.Reserva (ReservaId) ON DELETE CASCADE,
            CONSTRAINT FK_ReservaHabitacion_Habitacion FOREIGN KEY (HabitacionId)
                REFERENCES dbo.Habitacion (HabitacionId),
            CONSTRAINT CK_ReservaHabitacion_Fechas CHECK (FechaCheckOut > FechaCheckIn),
            CONSTRAINT CK_ReservaHabitacion_PrecioNoche CHECK (PrecioNoche >= 0),
            CONSTRAINT CK_ReservaHabitacion_PrecioTotal CHECK (PrecioTotal >= 0)
        );
    END;

    IF COL_LENGTH(N'dbo.Reserva', N'HabitacionId') IS NOT NULL
    EXEC sys.sp_executesql N'
    INSERT INTO dbo.ReservaHabitacion
        (ReservaId, HabitacionId, FechaCheckIn, FechaCheckOut, NumAdultos, NumNinos, PrecioNoche, PrecioTotal)
    SELECT reserva.ReservaId, reserva.HabitacionId, reserva.FechaCheckIn, reserva.FechaCheckOut,
           reserva.NumAdultos, reserva.NumNinos, NULL, reserva.PrecioTotal
    FROM dbo.Reserva reserva
    INNER JOIN dbo.Habitacion habitacion ON habitacion.HabitacionId = reserva.HabitacionId
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ReservaHabitacion detalle
        WHERE detalle.ReservaId = reserva.ReservaId
          AND detalle.HabitacionId = reserva.HabitacionId
          AND detalle.FechaCheckIn = reserva.FechaCheckIn
          AND detalle.FechaCheckOut = reserva.FechaCheckOut
    );';

    IF OBJECT_ID(N'dbo.AsignacionHuesped', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AsignacionHuesped (
            ReservaHabitacionId INT NOT NULL,
            HuespedId INT NOT NULL,
            EsTitular BIT NOT NULL CONSTRAINT DF_AsignacionHuesped_Titular DEFAULT (0),
            Observacion NVARCHAR(250) NULL,
            CONSTRAINT PK_AsignacionHuesped PRIMARY KEY (ReservaHabitacionId, HuespedId),
            CONSTRAINT FK_AsignacionHuesped_Detalle FOREIGN KEY (ReservaHabitacionId)
                REFERENCES dbo.ReservaHabitacion (ReservaHabitacionId) ON DELETE CASCADE,
            CONSTRAINT FK_AsignacionHuesped_Huesped FOREIGN KEY (HuespedId)
                REFERENCES dbo.Huesped (HuespedId)
        );
        CREATE UNIQUE INDEX UX_AsignacionHuesped_Titular
            ON dbo.AsignacionHuesped (ReservaHabitacionId) WHERE EsTitular = 1;
    END;

    IF OBJECT_ID(N'dbo.ReservaHuesped', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.Reserva', N'HabitacionId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
        INSERT INTO dbo.Huesped (PersonaId)
        SELECT DISTINCT legado.PersonaId
        FROM dbo.ReservaHuesped legado
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Huesped huesped WHERE huesped.PersonaId = legado.PersonaId);

        INSERT INTO dbo.AsignacionHuesped (ReservaHabitacionId, HuespedId, EsTitular, Observacion)
        SELECT detalle.ReservaHabitacionId, huesped.HuespedId, legado.EsTitular, legado.Observacion
        FROM dbo.ReservaHuesped legado
        INNER JOIN dbo.Reserva reserva ON reserva.ReservaId = legado.ReservaId
        INNER JOIN dbo.ReservaHabitacion detalle
            ON detalle.ReservaId = reserva.ReservaId
           AND detalle.HabitacionId = reserva.HabitacionId
           AND detalle.FechaCheckIn = reserva.FechaCheckIn
           AND detalle.FechaCheckOut = reserva.FechaCheckOut
        INNER JOIN dbo.Huesped huesped ON huesped.PersonaId = legado.PersonaId
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.AsignacionHuesped existente
            WHERE existente.ReservaHabitacionId = detalle.ReservaHabitacionId
              AND existente.HuespedId = huesped.HuespedId
        );';
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ReservaHabitacion') AND name = N'IX_ReservaHabitacion_Habitacion_Fechas')
        CREATE INDEX IX_ReservaHabitacion_Habitacion_Fechas
            ON dbo.ReservaHabitacion (HabitacionId, FechaCheckIn, FechaCheckOut);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ReservaHabitacion') AND name = N'UQ_ReservaHabitacion_Reserva_Habitacion_Fechas')
        CREATE UNIQUE INDEX UQ_ReservaHabitacion_Reserva_Habitacion_Fechas
            ON dbo.ReservaHabitacion (ReservaId, HabitacionId, FechaCheckIn, FechaCheckOut);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
