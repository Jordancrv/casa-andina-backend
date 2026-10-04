-- Lleva el esquema normalizado existente al modelo final de Casa Andina.
-- Compatible con el DDL nuevo y con las migraciones anteriores de personas y reservas.
-- Ejecutar primero migrate-personas-reservas.sql y migrate-reservas-multiples-habitaciones.sql
-- cuando la base todavía use el esquema legado. No elimina registros de negocio.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.PersonaNatural', N'Genero') IS NULL
        ALTER TABLE dbo.PersonaNatural ADD Genero NCHAR(1) NULL;
    IF COL_LENGTH(N'dbo.PersonaNatural', N'Nombres') IS NOT NULL
        ALTER TABLE dbo.PersonaNatural ALTER COLUMN Nombres NVARCHAR(200) NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PersonaNatural_Genero')
        EXEC sys.sp_executesql N'ALTER TABLE dbo.PersonaNatural ADD CONSTRAINT CK_PersonaNatural_Genero
            CHECK (Genero IS NULL OR Genero IN (N''F'', N''M'', N''O'', N''N''));';

    IF OBJECT_ID(N'dbo.PersonaContacto', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PersonaContacto (
            PersonaContactoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PersonaContacto PRIMARY KEY,
            PersonaId INT NOT NULL,
            TipoContacto NVARCHAR(20) NOT NULL,
            Valor NVARCHAR(150) NOT NULL,
            EsPrincipal BIT NOT NULL CONSTRAINT DF_PersonaContacto_Principal DEFAULT (0),
            Verificado BIT NOT NULL CONSTRAINT DF_PersonaContacto_Verificado DEFAULT (0),
            CONSTRAINT FK_PersonaContacto_Persona FOREIGN KEY (PersonaId) REFERENCES dbo.Persona(PersonaId),
            CONSTRAINT CK_PersonaContacto_Tipo CHECK (TipoContacto IN (N'Correo', N'Telefono')),
            CONSTRAINT CK_PersonaContacto_Valor CHECK (LEN(LTRIM(RTRIM(Valor))) > 0)
        );
    END;

    IF OBJECT_ID(N'dbo.PersonaCorreo', N'U') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.PersonaContacto (PersonaId, TipoContacto, Valor, EsPrincipal, Verificado)
            SELECT antiguo.PersonaId, N''Correo'', LEFT(LOWER(LTRIM(RTRIM(antiguo.Correo))), 150), antiguo.EsPrincipal, antiguo.Verificado
            FROM dbo.PersonaCorreo antiguo
            WHERE LEN(LTRIM(RTRIM(antiguo.Correo))) > 0
              AND NOT EXISTS (SELECT 1 FROM dbo.PersonaContacto nuevo
                  WHERE nuevo.TipoContacto = N''Correo'' AND LOWER(nuevo.Valor) = LOWER(LTRIM(RTRIM(antiguo.Correo))));';

    IF OBJECT_ID(N'dbo.PersonaTelefono', N'U') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.PersonaContacto (PersonaId, TipoContacto, Valor, EsPrincipal)
            SELECT antiguo.PersonaId, N''Telefono'', LEFT(LTRIM(RTRIM(antiguo.Numero)), 150), antiguo.EsPrincipal
            FROM dbo.PersonaTelefono antiguo
            WHERE LEN(LTRIM(RTRIM(antiguo.Numero))) > 0
              AND NOT EXISTS (SELECT 1 FROM dbo.PersonaContacto nuevo
                  WHERE nuevo.PersonaId = antiguo.PersonaId AND nuevo.TipoContacto = N''Telefono''
                    AND nuevo.Valor = LTRIM(RTRIM(antiguo.Numero)));';

    -- Copia contactos que todavía residan en las tablas heredadas de Usuario/Cliente.
    IF COL_LENGTH(N'dbo.Usuario', N'Correo') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.PersonaContacto (PersonaId, TipoContacto, Valor, EsPrincipal)
            SELECT usuario.PersonaId, N''Correo'', LEFT(LOWER(LTRIM(RTRIM(usuario.Correo))), 150), 1
            FROM dbo.Usuario usuario
            WHERE usuario.PersonaId IS NOT NULL AND LEN(LTRIM(RTRIM(usuario.Correo))) > 0
              AND NOT EXISTS (SELECT 1 FROM dbo.PersonaContacto contacto
                  WHERE contacto.TipoContacto = N''Correo'' AND LOWER(contacto.Valor) = LOWER(LTRIM(RTRIM(usuario.Correo))));';

    IF COL_LENGTH(N'dbo.Usuario', N'Telefono') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.PersonaContacto (PersonaId, TipoContacto, Valor, EsPrincipal)
            SELECT usuario.PersonaId, N''Telefono'', LEFT(LTRIM(RTRIM(usuario.Telefono)), 150), 1
            FROM dbo.Usuario usuario
            WHERE usuario.PersonaId IS NOT NULL AND LEN(LTRIM(RTRIM(usuario.Telefono))) > 0
              AND NOT EXISTS (SELECT 1 FROM dbo.PersonaContacto contacto
                  WHERE contacto.PersonaId = usuario.PersonaId AND contacto.TipoContacto = N''Telefono''
                    AND contacto.Valor = LTRIM(RTRIM(usuario.Telefono)));';

    IF COL_LENGTH(N'dbo.Cliente', N'Correo') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.PersonaContacto (PersonaId, TipoContacto, Valor, EsPrincipal)
            SELECT cliente.PersonaId, N''Correo'', LEFT(LOWER(LTRIM(RTRIM(cliente.Correo))), 150), 1
            FROM dbo.Cliente cliente
            WHERE cliente.PersonaId IS NOT NULL AND LEN(LTRIM(RTRIM(cliente.Correo))) > 0
              AND NOT EXISTS (SELECT 1 FROM dbo.PersonaContacto contacto
                  WHERE contacto.TipoContacto = N''Correo'' AND LOWER(contacto.Valor) = LOWER(LTRIM(RTRIM(cliente.Correo))));';

    IF COL_LENGTH(N'dbo.Cliente', N'Telefono') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.PersonaContacto (PersonaId, TipoContacto, Valor, EsPrincipal)
            SELECT cliente.PersonaId, N''Telefono'', LEFT(LTRIM(RTRIM(cliente.Telefono)), 150), 1
            FROM dbo.Cliente cliente
            WHERE cliente.PersonaId IS NOT NULL AND LEN(LTRIM(RTRIM(cliente.Telefono))) > 0
              AND NOT EXISTS (SELECT 1 FROM dbo.PersonaContacto contacto
                  WHERE contacto.PersonaId = cliente.PersonaId AND contacto.TipoContacto = N''Telefono''
                    AND contacto.Valor = LTRIM(RTRIM(cliente.Telefono)));';

    IF EXISTS (
        SELECT LOWER(Valor) FROM dbo.PersonaContacto
        WHERE TipoContacto = N'Correo'
        GROUP BY LOWER(Valor) HAVING COUNT(DISTINCT PersonaId) > 1)
        THROW 51030, 'Hay correos asociados a varias personas. Resuelva esos duplicados antes de aplicar la unicidad de PersonaContacto.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PersonaContacto') AND name = N'UX_PersonaContacto_Correo')
        CREATE UNIQUE INDEX UX_PersonaContacto_Correo ON dbo.PersonaContacto(Valor) WHERE TipoContacto = N'Correo';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PersonaContacto') AND name = N'UX_PersonaContacto_Principal')
        CREATE UNIQUE INDEX UX_PersonaContacto_Principal ON dbo.PersonaContacto(PersonaId, TipoContacto) WHERE EsPrincipal = 1;

    -- Roles: copia el RolId anterior antes de quitar la relación 1:N.
    IF COL_LENGTH(N'dbo.Rol', N'Codigo') IS NULL ALTER TABLE dbo.Rol ADD Codigo NVARCHAR(30) NULL;
    EXEC sys.sp_executesql N'
        UPDATE dbo.Rol SET Codigo = CASE
            WHEN Nombre = N''Administrador'' THEN N''ADMINISTRADOR''
            WHEN Nombre IN (N''Recepcionista'', N''Recepción'', N''Recepcion'') THEN N''RECEPCIONISTA''
            WHEN Nombre = N''Cliente'' THEN N''CLIENTE''
            WHEN Nombre = N''Superadministrador'' THEN N''SUPERADMINISTRADOR''
            WHEN Nombre = N''Operaciones'' THEN N''OPERACIONES''
            WHEN Nombre = N''Mantenimiento'' THEN N''MANTENIMIENTO''
            ELSE UPPER(REPLACE(Nombre, N'' '', N''_'')) END
        WHERE Codigo IS NULL OR LTRIM(RTRIM(Codigo)) = N'''';
        IF EXISTS (SELECT Codigo FROM dbo.Rol GROUP BY Codigo HAVING COUNT(*) > 1)
            THROW 51031, ''Hay roles con códigos duplicados; resuélvalos antes de continuar.'', 1;
        ALTER TABLE dbo.Rol ALTER COLUMN Codigo NVARCHAR(30) NOT NULL;';

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Rol_Codigo' AND parent_object_id = OBJECT_ID(N'dbo.Rol'))
        ALTER TABLE dbo.Rol ADD CONSTRAINT UQ_Rol_Codigo UNIQUE (Codigo);

    MERGE dbo.Rol AS destino
    USING (VALUES
        (N'ADMINISTRADOR', N'Administrador'),
        (N'RECEPCIONISTA', N'Recepcionista'),
        (N'CLIENTE', N'Cliente'),
        (N'SUPERADMINISTRADOR', N'Superadministrador')
    ) AS origen(Codigo, Nombre)
    ON destino.Codigo = origen.Codigo
    WHEN NOT MATCHED THEN INSERT(Codigo, Nombre) VALUES(origen.Codigo, origen.Nombre);

    IF OBJECT_ID(N'dbo.UsuarioRol', N'U') IS NULL
        CREATE TABLE dbo.UsuarioRol (
            UsuarioId INT NOT NULL,
            RolId INT NOT NULL,
            FechaAsignacion DATETIME2 NOT NULL CONSTRAINT DF_UsuarioRol_Fecha DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_UsuarioRol PRIMARY KEY (UsuarioId, RolId),
            CONSTRAINT FK_UsuarioRol_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario(UsuarioId) ON DELETE CASCADE,
            CONSTRAINT FK_UsuarioRol_Rol FOREIGN KEY (RolId) REFERENCES dbo.Rol(RolId)
        );

    IF COL_LENGTH(N'dbo.Usuario', N'RolId') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.UsuarioRol(UsuarioId, RolId)
            SELECT usuario.UsuarioId, usuario.RolId FROM dbo.Usuario usuario
            WHERE usuario.RolId IS NOT NULL AND NOT EXISTS
                (SELECT 1 FROM dbo.UsuarioRol ur WHERE ur.UsuarioId = usuario.UsuarioId AND ur.RolId = usuario.RolId);';

    -- Personas provenientes de Cliente obtienen también cuenta de acceso.
    IF COL_LENGTH(N'dbo.Cliente', N'ContrasenaHash') IS NOT NULL
       AND COL_LENGTH(N'dbo.Cliente', N'PersonaId') IS NOT NULL
       AND COL_LENGTH(N'dbo.Usuario', N'ContrasenaHash') IS NOT NULL
    BEGIN
        DECLARE @TieneUsuarioNombre BIT = IIF(COL_LENGTH(N'dbo.Usuario', N'Nombre') IS NULL, 0, 1);
        DECLARE @TieneUsuarioCorreo BIT = IIF(COL_LENGTH(N'dbo.Usuario', N'Correo') IS NULL, 0, 1);
        DECLARE @TieneUsuarioTelefono BIT = IIF(COL_LENGTH(N'dbo.Usuario', N'Telefono') IS NULL, 0, 1);
        DECLARE @TieneUsuarioRol BIT = IIF(COL_LENGTH(N'dbo.Usuario', N'RolId') IS NULL, 0, 1);
        DECLARE @TieneUsuarioSede BIT = IIF(COL_LENGTH(N'dbo.Usuario', N'SedeId') IS NULL, 0, 1);
        DECLARE @UsuarioColumns NVARCHAR(MAX) = N'PersonaId, ContrasenaHash, Estado, FechaRegistro';
        DECLARE @UsuarioSelect NVARCHAR(MAX) = N'c.PersonaId, c.ContrasenaHash, N''Activo'', COALESCE(c.FechaRegistro, SYSDATETIME())';
        IF @TieneUsuarioSede=1 BEGIN SET @UsuarioColumns += N', SedeId'; SET @UsuarioSelect += N', NULL'; END;
        IF @TieneUsuarioNombre=1 BEGIN SET @UsuarioColumns += N', Nombre'; SET @UsuarioSelect += N', COALESCE(n.Nombres, j.RazonSocial, N''Cliente'')'; END;
        IF @TieneUsuarioCorreo=1 BEGIN SET @UsuarioColumns += N', Correo'; SET @UsuarioSelect += N', COALESCE((SELECT TOP (1) pc.Valor FROM dbo.PersonaContacto pc WHERE pc.PersonaId=c.PersonaId AND pc.TipoContacto=N''Correo'' ORDER BY pc.EsPrincipal DESC, pc.PersonaContactoId), CONCAT(N''cliente-'', c.ClienteId, N''@invalid.local''))'; END;
        IF @TieneUsuarioTelefono=1 BEGIN SET @UsuarioColumns += N', Telefono'; SET @UsuarioSelect += N', (SELECT TOP (1) pc.Valor FROM dbo.PersonaContacto pc WHERE pc.PersonaId=c.PersonaId AND pc.TipoContacto=N''Telefono'' ORDER BY pc.EsPrincipal DESC, pc.PersonaContactoId)'; END;
        IF @TieneUsuarioRol=1 BEGIN SET @UsuarioColumns += N', RolId'; SET @UsuarioSelect += N', (SELECT TOP (1) RolId FROM dbo.Rol WHERE Codigo=N''CLIENTE'')'; END;
        DECLARE @SqlInsertUsuario NVARCHAR(MAX) = N'INSERT INTO dbo.Usuario(' + @UsuarioColumns + N') SELECT ' + @UsuarioSelect
            + N' FROM dbo.Cliente c LEFT JOIN dbo.PersonaNatural n ON n.PersonaId=c.PersonaId LEFT JOIN dbo.PersonaJuridica j ON j.PersonaId=c.PersonaId'
            + N' WHERE c.ContrasenaHash IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.Usuario u WHERE u.PersonaId=c.PersonaId);';
        EXEC sys.sp_executesql @SqlInsertUsuario;

        INSERT INTO dbo.UsuarioRol(UsuarioId, RolId)
        SELECT u.UsuarioId, r.RolId FROM dbo.Usuario u
        INNER JOIN dbo.Cliente c ON c.PersonaId=u.PersonaId
        INNER JOIN dbo.Rol r ON r.Codigo=N'CLIENTE'
        WHERE NOT EXISTS(SELECT 1 FROM dbo.UsuarioRol ur WHERE ur.UsuarioId=u.UsuarioId AND ur.RolId=r.RolId);
    END;

    -- Asegura perfil Cliente para toda cuenta con rol Cliente.
    INSERT INTO dbo.Cliente(PersonaId, Estado, FechaRegistro)
    SELECT u.PersonaId, N'Activo', SYSDATETIME()
    FROM dbo.Usuario u
    INNER JOIN dbo.UsuarioRol ur ON ur.UsuarioId=u.UsuarioId
    INNER JOIN dbo.Rol r ON r.RolId=ur.RolId AND r.Codigo=N'CLIENTE'
    WHERE NOT EXISTS(SELECT 1 FROM dbo.Cliente c WHERE c.PersonaId=u.PersonaId);

    -- Sede en cabecera se deduce de las habitaciones reservadas.
    IF COL_LENGTH(N'dbo.Reserva', N'SedeId') IS NULL ALTER TABLE dbo.Reserva ADD SedeId INT NULL;
    IF COL_LENGTH(N'dbo.ReservaHabitacion', N'CantidadPersonas') IS NULL
        ALTER TABLE dbo.ReservaHabitacion ADD CantidadPersonas SMALLINT NOT NULL CONSTRAINT DF_ReservaHabitacion_CantidadPersonas DEFAULT (1);
    IF COL_LENGTH(N'dbo.ReservaHabitacion', N'PrecioAcordado') IS NULL
        ALTER TABLE dbo.ReservaHabitacion ADD PrecioAcordado DECIMAL(10,2) NULL;
    IF COL_LENGTH(N'dbo.ReservaHabitacion', N'Subtotal') IS NULL
        ALTER TABLE dbo.ReservaHabitacion ADD Subtotal DECIMAL(10,2) NULL;

    IF COL_LENGTH(N'dbo.ReservaHabitacion', N'NumAdultos') IS NOT NULL
       AND COL_LENGTH(N'dbo.ReservaHabitacion', N'NumNinos') IS NOT NULL
        EXEC sys.sp_executesql N'UPDATE dbo.ReservaHabitacion SET CantidadPersonas = NumAdultos + NumNinos WHERE CantidadPersonas = 1;';
    IF COL_LENGTH(N'dbo.ReservaHabitacion', N'PrecioTotal') IS NOT NULL
        EXEC sys.sp_executesql N'UPDATE dbo.ReservaHabitacion SET Subtotal = COALESCE(Subtotal, PrecioTotal), PrecioAcordado = COALESCE(PrecioAcordado, PrecioTotal) WHERE Subtotal IS NULL OR PrecioAcordado IS NULL;';
    IF COL_LENGTH(N'dbo.ReservaHabitacion', N'PrecioNoche') IS NOT NULL
        EXEC sys.sp_executesql N'UPDATE dbo.ReservaHabitacion SET PrecioAcordado = COALESCE(PrecioAcordado, PrecioNoche, 0), Subtotal = COALESCE(Subtotal, PrecioTotal, PrecioNoche, 0);';

    IF COL_LENGTH(N'dbo.Reserva', N'HabitacionId') IS NOT NULL
       AND COL_LENGTH(N'dbo.Reserva', N'FechaCheckIn') IS NOT NULL
       AND COL_LENGTH(N'dbo.Reserva', N'FechaCheckOut') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ReservaHabitacion(ReservaId, HabitacionId, FechaCheckIn, FechaCheckOut, CantidadPersonas, PrecioAcordado, Subtotal)
            SELECT r.ReservaId, r.HabitacionId, r.FechaCheckIn, r.FechaCheckOut, 1, r.PrecioTotal, r.PrecioTotal
            FROM dbo.Reserva r
            WHERE NOT EXISTS(SELECT 1 FROM dbo.ReservaHabitacion rh WHERE rh.ReservaId=r.ReservaId);';

    EXEC sys.sp_executesql N'
        UPDATE r SET SedeId = h.SedeId
        FROM dbo.Reserva r
        INNER JOIN (SELECT ReservaId, MIN(HabitacionId) AS HabitacionId FROM dbo.ReservaHabitacion GROUP BY ReservaId) rh ON rh.ReservaId=r.ReservaId
        INNER JOIN dbo.Habitacion h ON h.HabitacionId=rh.HabitacionId
        WHERE r.SedeId IS NULL;
        IF EXISTS(SELECT 1 FROM dbo.Reserva WHERE SedeId IS NULL)
            THROW 51032, ''Hay reservas sin habitación o sin sede. Asigne su sede antes de continuar.'', 1;
        IF EXISTS(SELECT 1 FROM dbo.Reserva r INNER JOIN dbo.ReservaHabitacion rh ON rh.ReservaId=r.ReservaId INNER JOIN dbo.Habitacion h ON h.HabitacionId=rh.HabitacionId WHERE h.SedeId<>r.SedeId)
            THROW 51033, ''Una reserva contiene habitaciones de distintas sedes. Sepárelas antes de continuar.'', 1;
        ALTER TABLE dbo.Reserva ALTER COLUMN SedeId INT NOT NULL;';

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Reserva_Sede')
        ALTER TABLE dbo.Reserva ADD CONSTRAINT FK_Reserva_Sede FOREIGN KEY(SedeId) REFERENCES dbo.Sede(SedeId);

    EXEC sys.sp_executesql N'
        UPDATE dbo.ReservaHabitacion SET PrecioAcordado=0 WHERE PrecioAcordado IS NULL;
        UPDATE dbo.ReservaHabitacion SET Subtotal=0 WHERE Subtotal IS NULL;
        ALTER TABLE dbo.ReservaHabitacion ALTER COLUMN PrecioAcordado DECIMAL(10,2) NOT NULL;
        ALTER TABLE dbo.ReservaHabitacion ALTER COLUMN Subtotal DECIMAL(10,2) NOT NULL;';

    -- Auditoría estructural. No registrar hashes, tokens ni secretos en Antes/Despues.
    IF OBJECT_ID(N'dbo.Auditoria', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Auditoria (
            AuditoriaId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Auditoria PRIMARY KEY,
            FechaUtc DATETIME2 NOT NULL CONSTRAINT DF_Auditoria_FechaUtc DEFAULT (SYSUTCDATETIME()),
            UsuarioId INT NULL,
            SedeId INT NULL,
            Accion NVARCHAR(30) NOT NULL,
            Entidad NVARCHAR(128) NOT NULL,
            EntidadId NVARCHAR(100) NOT NULL,
            Antes NVARCHAR(MAX) NULL,
            Despues NVARCHAR(MAX) NULL,
            DireccionIp NVARCHAR(45) NULL,
            CorrelationId UNIQUEIDENTIFIER NULL,
            CONSTRAINT FK_Auditoria_Usuario FOREIGN KEY(UsuarioId) REFERENCES dbo.Usuario(UsuarioId) ON DELETE SET NULL,
            CONSTRAINT FK_Auditoria_Sede FOREIGN KEY(SedeId) REFERENCES dbo.Sede(SedeId) ON DELETE SET NULL,
            CONSTRAINT CK_Auditoria_Accion CHECK(Accion IN (N'INSERT',N'UPDATE',N'DELETE',N'LOGIN',N'LOGOUT',N'OTRA'))
        );
    END;

    -- Migra huéspedes naturales y sus asignaciones desde ReservaHuesped legado.
    IF OBJECT_ID(N'dbo.ReservaHuesped', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.AsignacionHuesped', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.Huesped', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.ReservaHuesped', N'PersonaId') IS NOT NULL
       AND COL_LENGTH(N'dbo.ReservaHuesped', N'ReservaId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.Huesped(PersonaId)
            SELECT DISTINCT legado.PersonaId
            FROM dbo.ReservaHuesped legado
            INNER JOIN dbo.PersonaNatural pn ON pn.PersonaId=legado.PersonaId
            WHERE NOT EXISTS(SELECT 1 FROM dbo.Huesped h WHERE h.PersonaId=legado.PersonaId);

            INSERT INTO dbo.AsignacionHuesped(ReservaHabitacionId,HuespedId,EsTitular,Observacion)
            SELECT rh.ReservaHabitacionId,h.HuespedId,legado.EsTitular,legado.Observacion
            FROM dbo.ReservaHuesped legado
            INNER JOIN dbo.ReservaHabitacion rh ON rh.ReservaId=legado.ReservaId
            INNER JOIN dbo.Huesped h ON h.PersonaId=legado.PersonaId
            WHERE NOT EXISTS(SELECT 1 FROM dbo.AsignacionHuesped ah
                WHERE ah.ReservaHabitacionId=rh.ReservaHabitacionId AND ah.HuespedId=h.HuespedId);';
    END;
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Auditoria') AND name=N'IX_Auditoria_FechaUtc') CREATE INDEX IX_Auditoria_FechaUtc ON dbo.Auditoria(FechaUtc);
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Auditoria') AND name=N'IX_Auditoria_Entidad') CREATE INDEX IX_Auditoria_Entidad ON dbo.Auditoria(Entidad,EntidadId,FechaUtc);
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Auditoria') AND name=N'IX_Auditoria_Usuario_Fecha') CREATE INDEX IX_Auditoria_Usuario_Fecha ON dbo.Auditoria(UsuarioId,FechaUtc);
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Auditoria') AND name=N'IX_Auditoria_Sede_Fecha') CREATE INDEX IX_Auditoria_Sede_Fecha ON dbo.Auditoria(SedeId,FechaUtc);

    -- Retira solo columnas/tablas sustituidas después de copiar los datos.
    IF COL_LENGTH(N'dbo.Usuario', N'RolId') IS NOT NULL
    BEGIN
        DECLARE @FkRolUsuario sysname;
        DECLARE @SqlDropFkRolUsuario NVARCHAR(500);
        SELECT @FkRolUsuario=name FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.Usuario') AND referenced_object_id=OBJECT_ID(N'dbo.Rol');
        IF @FkRolUsuario IS NOT NULL
        BEGIN
            SET @SqlDropFkRolUsuario = N'ALTER TABLE dbo.Usuario DROP CONSTRAINT [' + REPLACE(@FkRolUsuario, N']', N']]') + N']';
            EXEC sys.sp_executesql @SqlDropFkRolUsuario;
        END;
        ALTER TABLE dbo.Usuario DROP COLUMN RolId;
    END;

    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Usuario') AND name=N'UQ_Usuario_Correo')
    BEGIN
        IF EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Usuario') AND name=N'UQ_Usuario_Correo')
            ALTER TABLE dbo.Usuario DROP CONSTRAINT UQ_Usuario_Correo;
        ELSE DROP INDEX UQ_Usuario_Correo ON dbo.Usuario;
    END;
    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Usuario') AND name=N'UX_Usuario_Correo')
        DROP INDEX UX_Usuario_Correo ON dbo.Usuario;
    IF COL_LENGTH(N'dbo.Usuario', N'Nombre') IS NOT NULL ALTER TABLE dbo.Usuario DROP COLUMN Nombre;
    IF COL_LENGTH(N'dbo.Usuario', N'Correo') IS NOT NULL ALTER TABLE dbo.Usuario DROP COLUMN Correo;
    IF COL_LENGTH(N'dbo.Usuario', N'Telefono') IS NOT NULL ALTER TABLE dbo.Usuario DROP COLUMN Telefono;

    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Cliente') AND name=N'UQ_Cliente_DNI')
    BEGIN
        IF EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Cliente') AND name=N'UQ_Cliente_DNI')
            ALTER TABLE dbo.Cliente DROP CONSTRAINT UQ_Cliente_DNI;
        ELSE DROP INDEX UQ_Cliente_DNI ON dbo.Cliente;
    END;
    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Cliente') AND name=N'UQ_Cliente_Correo')
    BEGIN
        IF EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Cliente') AND name=N'UQ_Cliente_Correo')
            ALTER TABLE dbo.Cliente DROP CONSTRAINT UQ_Cliente_Correo;
        ELSE DROP INDEX UQ_Cliente_Correo ON dbo.Cliente;
    END;
    IF COL_LENGTH(N'dbo.Cliente', N'NombreCompleto') IS NOT NULL ALTER TABLE dbo.Cliente DROP COLUMN NombreCompleto;
    IF COL_LENGTH(N'dbo.Cliente', N'DNI') IS NOT NULL ALTER TABLE dbo.Cliente DROP COLUMN DNI;
    IF COL_LENGTH(N'dbo.Cliente', N'Correo') IS NOT NULL ALTER TABLE dbo.Cliente DROP COLUMN Correo;
    IF COL_LENGTH(N'dbo.Cliente', N'Telefono') IS NOT NULL ALTER TABLE dbo.Cliente DROP COLUMN Telefono;
    IF COL_LENGTH(N'dbo.Cliente', N'ContrasenaHash') IS NOT NULL ALTER TABLE dbo.Cliente DROP COLUMN ContrasenaHash;
    IF COL_LENGTH(N'dbo.Cliente', N'FotoUrl') IS NOT NULL ALTER TABLE dbo.Cliente DROP COLUMN FotoUrl;

    IF COL_LENGTH(N'dbo.Reserva', N'HabitacionId') IS NOT NULL
    BEGIN
        DECLARE @SqlDropReservaIndexes NVARCHAR(MAX) = N'';
        SELECT @SqlDropReservaIndexes += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.Reserva;'
        FROM sys.indexes i
        WHERE i.object_id=OBJECT_ID(N'dbo.Reserva')
          AND i.is_primary_key=0 AND i.is_unique_constraint=0
          AND EXISTS (
              SELECT 1
              FROM sys.index_columns ic
              INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
              WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id
                AND c.name IN (N'HabitacionId', N'FechaCheckIn', N'FechaCheckOut', N'NumAdultos', N'NumNinos'));
        IF LEN(@SqlDropReservaIndexes) > 0 EXEC sys.sp_executesql @SqlDropReservaIndexes;

        DECLARE @FkReservaHabitacion sysname;
        DECLARE @SqlDropFkReservaHabitacion NVARCHAR(500);
        SELECT @FkReservaHabitacion=name FROM sys.foreign_keys
        WHERE parent_object_id=OBJECT_ID(N'dbo.Reserva') AND referenced_object_id=OBJECT_ID(N'dbo.Habitacion');
        IF @FkReservaHabitacion IS NOT NULL
        BEGIN
            SET @SqlDropFkReservaHabitacion = N'ALTER TABLE dbo.Reserva DROP CONSTRAINT [' + REPLACE(@FkReservaHabitacion, N']', N']]') + N']';
            EXEC sys.sp_executesql @SqlDropFkReservaHabitacion;
        END;
        ALTER TABLE dbo.Reserva DROP COLUMN HabitacionId;
    END;
    IF COL_LENGTH(N'dbo.Reserva', N'FechaCheckIn') IS NOT NULL ALTER TABLE dbo.Reserva DROP COLUMN FechaCheckIn;
    IF COL_LENGTH(N'dbo.Reserva', N'FechaCheckOut') IS NOT NULL ALTER TABLE dbo.Reserva DROP COLUMN FechaCheckOut;
    IF COL_LENGTH(N'dbo.Reserva', N'NumAdultos') IS NOT NULL ALTER TABLE dbo.Reserva DROP COLUMN NumAdultos;
    IF COL_LENGTH(N'dbo.Reserva', N'NumNinos') IS NOT NULL ALTER TABLE dbo.Reserva DROP COLUMN NumNinos;

    IF OBJECT_ID(N'dbo.PersonaTelefono', N'U') IS NOT NULL DROP TABLE dbo.PersonaTelefono;
    IF OBJECT_ID(N'dbo.PersonaCorreo', N'U') IS NOT NULL DROP TABLE dbo.PersonaCorreo;

    IF OBJECT_ID(N'dbo.Comprobante', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.Comprobante', N'TipoComprobante') IS NULL
        ALTER TABLE dbo.Comprobante ADD TipoComprobante NVARCHAR(10) NOT NULL
            CONSTRAINT DF_Comprobante_TipoComprobante DEFAULT (N'Boleta');
    IF OBJECT_ID(N'dbo.Comprobante', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Comprobante_TipoComprobante')
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Comprobante ADD CONSTRAINT CK_Comprobante_TipoComprobante
            CHECK (TipoComprobante IN (N''Boleta'', N''Factura''));';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
