-- Esquema aditivo para registrar pagos simulados de reservas.
-- No procesa cobros y nunca almacena PAN, CVV, PIN, tokens o credenciales.
-- Ejecutar sobre una copia/entorno de desarrollo; no elimina ni altera tablas existentes.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Reserva', N'U') IS NULL
        THROW 51040, 'Debe existir dbo.Reserva antes de instalar el registro de pagos.', 1;

    IF OBJECT_ID(N'dbo.EstadoPago', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EstadoPago (
            EstadoPagoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EstadoPago PRIMARY KEY,
            Codigo NVARCHAR(30) NOT NULL,
            Nombre NVARCHAR(60) NOT NULL,
            Descripcion NVARCHAR(200) NULL,
            EsFinal BIT NOT NULL CONSTRAINT DF_EstadoPago_EsFinal DEFAULT (0),
            Activo BIT NOT NULL CONSTRAINT DF_EstadoPago_Activo DEFAULT (1),
            CONSTRAINT UQ_EstadoPago_Codigo UNIQUE (Codigo),
            CONSTRAINT UQ_EstadoPago_Nombre UNIQUE (Nombre)
        );
    END;

    IF OBJECT_ID(N'dbo.MetodoPago', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MetodoPago (
            MetodoPagoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MetodoPago PRIMARY KEY,
            Codigo NVARCHAR(30) NOT NULL,
            Nombre NVARCHAR(80) NOT NULL,
            Categoria NVARCHAR(30) NOT NULL,
            Activo BIT NOT NULL CONSTRAINT DF_MetodoPago_Activo DEFAULT (1),
            FechaCreacion DATETIME2 NOT NULL CONSTRAINT DF_MetodoPago_FechaCreacion DEFAULT (SYSDATETIME()),
            CONSTRAINT UQ_MetodoPago_Codigo UNIQUE (Codigo),
            CONSTRAINT UQ_MetodoPago_Nombre UNIQUE (Nombre),
            CONSTRAINT CK_MetodoPago_Categoria CHECK
                (Categoria = N'Otro' OR Categoria = N'Efectivo' OR Categoria = N'Transferencia'
                    OR Categoria = N'BilleteraDigital' OR Categoria = N'Tarjeta')
        );
    END;

    IF OBJECT_ID(N'dbo.Pago', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Pago (
            PagoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Pago PRIMARY KEY,
            MetodoPagoId INT NOT NULL,
            EstadoPagoId INT NOT NULL,
            ReferenciaProveedor NVARCHAR(100) NULL,
            Importe DECIMAL(10,2) NOT NULL,
            Moneda CHAR(3) NOT NULL CONSTRAINT DF_Pago_Moneda DEFAULT ('PEN'),
            FechaCreacion DATETIME2 NOT NULL CONSTRAINT DF_Pago_FechaCreacion DEFAULT (SYSDATETIME()),
            FechaActualizacion DATETIME2 NULL,
            CONSTRAINT FK_Pago_MetodoPago FOREIGN KEY (MetodoPagoId) REFERENCES dbo.MetodoPago(MetodoPagoId),
            CONSTRAINT FK_Pago_EstadoPago FOREIGN KEY (EstadoPagoId) REFERENCES dbo.EstadoPago(EstadoPagoId),
            CONSTRAINT CK_Pago_Importe CHECK (Importe > 0),
            CONSTRAINT CK_Pago_Moneda CHECK (Moneda LIKE '[A-Z][A-Z][A-Z]'),
            CONSTRAINT CK_Pago_ReferenciaProveedor CHECK
                (ReferenciaProveedor IS NULL OR LEN(LTRIM(RTRIM(ReferenciaProveedor))) > 0)
        );
        CREATE UNIQUE INDEX UX_Pago_ReferenciaProveedor
            ON dbo.Pago(ReferenciaProveedor) WHERE ReferenciaProveedor IS NOT NULL;
        CREATE INDEX IX_Pago_Estado_Fecha ON dbo.Pago(EstadoPagoId, FechaCreacion);
    END;

    IF OBJECT_ID(N'dbo.DetallePago', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.DetallePago (
            DetallePagoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DetallePago PRIMARY KEY,
            ReservaId INT NOT NULL,
            PagoId INT NOT NULL,
            MontoAplicado DECIMAL(10,2) NOT NULL,
            FechaAplicacion DATETIME2 NOT NULL CONSTRAINT DF_DetallePago_FechaAplicacion DEFAULT (SYSDATETIME()),
            CONSTRAINT FK_DetallePago_Reserva FOREIGN KEY (ReservaId) REFERENCES dbo.Reserva(ReservaId),
            CONSTRAINT FK_DetallePago_Pago FOREIGN KEY (PagoId) REFERENCES dbo.Pago(PagoId),
            CONSTRAINT UQ_DetallePago_Reserva_Pago UNIQUE (ReservaId, PagoId),
            CONSTRAINT CK_DetallePago_MontoAplicado CHECK (MontoAplicado > 0)
        );
        CREATE INDEX IX_DetallePago_Pago ON dbo.DetallePago(PagoId);
    END;

    -- Valida el contrato relacional antes de tocar catálogos ya existentes.
    DECLARE @ContratoPago TABLE (
        Tabla SYSNAME NOT NULL,
        Columna SYSNAME NOT NULL,
        Tipo SYSNAME NOT NULL,
        Longitud SMALLINT NOT NULL,
        Precision TINYINT NOT NULL,
        Escala TINYINT NOT NULL,
        EsNula BIT NOT NULL,
        EsIdentity BIT NOT NULL
    );
    INSERT INTO @ContratoPago VALUES
        (N'EstadoPago',N'EstadoPagoId',N'int',4,10,0,0,1),
        (N'EstadoPago',N'Codigo',N'nvarchar',60,0,0,0,0),
        (N'EstadoPago',N'Nombre',N'nvarchar',120,0,0,0,0),
        (N'EstadoPago',N'Descripcion',N'nvarchar',400,0,0,1,0),
        (N'EstadoPago',N'EsFinal',N'bit',1,1,0,0,0),
        (N'EstadoPago',N'Activo',N'bit',1,1,0,0,0),
        (N'MetodoPago',N'MetodoPagoId',N'int',4,10,0,0,1),
        (N'MetodoPago',N'Codigo',N'nvarchar',60,0,0,0,0),
        (N'MetodoPago',N'Nombre',N'nvarchar',160,0,0,0,0),
        (N'MetodoPago',N'Categoria',N'nvarchar',60,0,0,0,0),
        (N'MetodoPago',N'Activo',N'bit',1,1,0,0,0),
        (N'MetodoPago',N'FechaCreacion',N'datetime2',8,27,7,0,0),
        (N'Pago',N'PagoId',N'int',4,10,0,0,1),
        (N'Pago',N'MetodoPagoId',N'int',4,10,0,0,0),
        (N'Pago',N'EstadoPagoId',N'int',4,10,0,0,0),
        (N'Pago',N'ReferenciaProveedor',N'nvarchar',200,0,0,1,0),
        (N'Pago',N'Importe',N'decimal',9,10,2,0,0),
        (N'Pago',N'Moneda',N'char',3,0,0,0,0),
        (N'Pago',N'FechaCreacion',N'datetime2',8,27,7,0,0),
        (N'Pago',N'FechaActualizacion',N'datetime2',8,27,7,1,0),
        (N'DetallePago',N'DetallePagoId',N'int',4,10,0,0,1),
        (N'DetallePago',N'ReservaId',N'int',4,10,0,0,0),
        (N'DetallePago',N'PagoId',N'int',4,10,0,0,0),
        (N'DetallePago',N'MontoAplicado',N'decimal',9,10,2,0,0),
        (N'DetallePago',N'FechaAplicacion',N'datetime2',8,27,7,0,0);

    IF EXISTS (
        SELECT 1
        FROM @ContratoPago esperado
        LEFT JOIN sys.tables t ON t.name = esperado.Tabla AND t.schema_id = SCHEMA_ID(N'dbo')
        LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.name = esperado.Columna
        LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE c.column_id IS NULL
           OR ty.name <> esperado.Tipo
           OR c.max_length <> esperado.Longitud
           OR c.precision <> esperado.Precision
           OR c.scale <> esperado.Escala
           OR c.is_nullable <> esperado.EsNula
           OR c.is_identity <> esperado.EsIdentity)
        THROW 51041, 'El esquema existente de pagos no coincide en columnas o tipos con el contrato esperado; no se aplicaron semillas.', 1;

    DECLARE @ContratoIndice TABLE (
        Tabla SYSNAME NOT NULL,
        Indice SYSNAME NOT NULL,
        Ordinal TINYINT NOT NULL,
        Columna SYSNAME NOT NULL,
        EsUnico BIT NOT NULL,
        Filtro NVARCHAR(300) NULL
    );
    INSERT INTO @ContratoIndice VALUES
        (N'EstadoPago',N'PK_EstadoPago',1,N'EstadoPagoId',1,NULL),
        (N'EstadoPago',N'UQ_EstadoPago_Codigo',1,N'Codigo',1,NULL),
        (N'EstadoPago',N'UQ_EstadoPago_Nombre',1,N'Nombre',1,NULL),
        (N'MetodoPago',N'PK_MetodoPago',1,N'MetodoPagoId',1,NULL),
        (N'MetodoPago',N'UQ_MetodoPago_Codigo',1,N'Codigo',1,NULL),
        (N'MetodoPago',N'UQ_MetodoPago_Nombre',1,N'Nombre',1,NULL),
        (N'Pago',N'PK_Pago',1,N'PagoId',1,NULL),
        (N'Pago',N'UX_Pago_ReferenciaProveedor',1,N'ReferenciaProveedor',1,N'[ReferenciaProveedor] IS NOT NULL'),
        (N'Pago',N'IX_Pago_Estado_Fecha',1,N'EstadoPagoId',0,NULL),
        (N'Pago',N'IX_Pago_Estado_Fecha',2,N'FechaCreacion',0,NULL),
        (N'DetallePago',N'PK_DetallePago',1,N'DetallePagoId',1,NULL),
        (N'DetallePago',N'UQ_DetallePago_Reserva_Pago',1,N'ReservaId',1,NULL),
        (N'DetallePago',N'UQ_DetallePago_Reserva_Pago',2,N'PagoId',1,NULL),
        (N'DetallePago',N'IX_DetallePago_Pago',1,N'PagoId',0,NULL);

    IF EXISTS (
        SELECT 1
        FROM (SELECT DISTINCT Tabla, Indice, EsUnico, Filtro FROM @ContratoIndice) esperado
        LEFT JOIN sys.indexes i ON i.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(esperado.Tabla)) AND i.name = esperado.Indice
        WHERE i.index_id IS NULL
           OR i.is_unique <> esperado.EsUnico
           OR ISNULL(LOWER(REPLACE(REPLACE(REPLACE(i.filter_definition,N' ',N''),N'(',N''),N')',N'')),N'')
                <> ISNULL(LOWER(REPLACE(REPLACE(REPLACE(esperado.Filtro,N' ',N''),N'(',N''),N')',N'')),N'')
           OR (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal > 0)
                <> (SELECT COUNT(*) FROM @ContratoIndice col WHERE col.Tabla=esperado.Tabla AND col.Indice=esperado.Indice)
           OR EXISTS (
                SELECT 1 FROM @ContratoIndice col
                WHERE col.Tabla=esperado.Tabla AND col.Indice=esperado.Indice
                  AND NOT EXISTS (
                      SELECT 1 FROM sys.index_columns ic
                      JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                      WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id
                        AND ic.key_ordinal=col.Ordinal AND c.name=col.Columna)))
        THROW 51043, 'Las columnas, unicidad o filtro de un índice no coinciden con el esquema esperado.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
        JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
        WHERE fk.name=N'FK_Pago_MetodoPago' AND fk.parent_object_id=OBJECT_ID(N'dbo.Pago')
          AND fk.referenced_object_id=OBJECT_ID(N'dbo.MetodoPago') AND pc.name=N'MetodoPagoId' AND rc.name=N'MetodoPagoId'
          AND fk.delete_referential_action=0 AND fk.is_disabled=0 AND fk.is_not_trusted=0
          AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=fk.object_id)=1)
       OR NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
        JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
        WHERE fk.name=N'FK_Pago_EstadoPago' AND fk.parent_object_id=OBJECT_ID(N'dbo.Pago')
          AND fk.referenced_object_id=OBJECT_ID(N'dbo.EstadoPago') AND pc.name=N'EstadoPagoId' AND rc.name=N'EstadoPagoId'
          AND fk.delete_referential_action=0 AND fk.is_disabled=0 AND fk.is_not_trusted=0
          AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=fk.object_id)=1)
       OR NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
        JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
        WHERE fk.name=N'FK_DetallePago_Reserva' AND fk.parent_object_id=OBJECT_ID(N'dbo.DetallePago')
          AND fk.referenced_object_id=OBJECT_ID(N'dbo.Reserva') AND pc.name=N'ReservaId' AND rc.name=N'ReservaId'
          AND fk.delete_referential_action=0 AND fk.is_disabled=0 AND fk.is_not_trusted=0
          AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=fk.object_id)=1)
       OR NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
        JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
        WHERE fk.name=N'FK_DetallePago_Pago' AND fk.parent_object_id=OBJECT_ID(N'dbo.DetallePago')
          AND fk.referenced_object_id=OBJECT_ID(N'dbo.Pago') AND pc.name=N'PagoId' AND rc.name=N'PagoId'
          AND fk.delete_referential_action=0 AND fk.is_disabled=0 AND fk.is_not_trusted=0
          AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=fk.object_id)=1)
        THROW 51044, 'Una clave foránea no coincide con la relación de pagos esperada.', 1;

    IF EXISTS (
        SELECT 1
        FROM (VALUES
            (N'MetodoPago',N'CK_MetodoPago_Categoria',N'[Categoria]=N''Otro''OR[Categoria]=N''Efectivo''OR[Categoria]=N''Transferencia''OR[Categoria]=N''BilleteraDigital''OR[Categoria]=N''Tarjeta'''),
            (N'Pago',N'CK_Pago_Importe',N'[Importe]>0'),
            (N'Pago',N'CK_Pago_Moneda',N'[Moneda]LIKE''[A-Z][A-Z][A-Z]'''),
            (N'Pago',N'CK_Pago_ReferenciaProveedor',N'[ReferenciaProveedor]ISNULLORLEN(LTRIM(RTRIM([ReferenciaProveedor])))>0'),
            (N'DetallePago',N'CK_DetallePago_MontoAplicado',N'[MontoAplicado]>0')
        ) esperado(Tabla,Restriccion,Definicion)
        LEFT JOIN sys.check_constraints cc ON cc.parent_object_id=OBJECT_ID(N'dbo.' + QUOTENAME(esperado.Tabla)) AND cc.name=esperado.Restriccion
        WHERE cc.object_id IS NULL OR cc.is_disabled=1 OR cc.is_not_trusted=1
           OR LOWER(REPLACE(REPLACE(REPLACE(cc.definition,N' ',N''),N'(',N''),N')',N''))
                <> LOWER(REPLACE(REPLACE(REPLACE(esperado.Definicion,N' ',N''),N'(',N''),N')',N'')))
        THROW 51045, 'La definición de una restricción CHECK no coincide con el contrato de pagos.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.EstadoPago') AND name = N'PK_EstadoPago' AND type = N'PK')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.EstadoPago') AND name = N'UQ_EstadoPago_Codigo' AND type = N'UQ')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.EstadoPago') AND name = N'UQ_EstadoPago_Nombre' AND type = N'UQ')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MetodoPago') AND name = N'PK_MetodoPago' AND type = N'PK')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MetodoPago') AND name = N'UQ_MetodoPago_Codigo' AND type = N'UQ')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MetodoPago') AND name = N'UQ_MetodoPago_Nombre' AND type = N'UQ')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Pago') AND name = N'PK_Pago' AND type = N'PK')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.DetallePago') AND name = N'PK_DetallePago' AND type = N'PK')
       OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.DetallePago') AND name = N'UQ_DetallePago_Reserva_Pago' AND type = N'UQ')
       OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Pago') AND name = N'FK_Pago_MetodoPago')
       OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Pago') AND name = N'FK_Pago_EstadoPago')
       OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.DetallePago') AND name = N'FK_DetallePago_Reserva')
       OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.DetallePago') AND name = N'FK_DetallePago_Pago')
       OR NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MetodoPago') AND name = N'CK_MetodoPago_Categoria')
       OR NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Pago') AND name = N'CK_Pago_Importe')
       OR NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Pago') AND name = N'CK_Pago_Moneda')
       OR NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Pago') AND name = N'CK_Pago_ReferenciaProveedor')
       OR NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.DetallePago') AND name = N'CK_DetallePago_MontoAplicado')
       OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Pago') AND name = N'UX_Pago_ReferenciaProveedor' AND is_unique = 1 AND has_filter = 1)
       OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Pago') AND name = N'IX_Pago_Estado_Fecha')
       OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DetallePago') AND name = N'IX_DetallePago_Pago')
        THROW 51042, 'Faltan claves, índices o restricciones requeridas en el esquema de pagos.', 1;

    -- Catálogos idempotentes: conserva cualquier configuración existente por código.
    INSERT INTO dbo.EstadoPago (Codigo, Nombre, Descripcion, EsFinal)
    SELECT v.Codigo, v.Nombre, v.Descripcion, v.EsFinal
    FROM (VALUES
        (N'PENDIENTE', N'Pendiente', N'Registro creado y pendiente de validación.', CONVERT(bit,0)),
        (N'EN_PROCESO', N'En proceso', N'Registro en proceso de validación manual.', CONVERT(bit,0)),
        (N'APROBADO', N'Aprobado', N'Estado aprobado dentro de la simulación; no confirma un cobro real.', CONVERT(bit,1)),
        (N'RECHAZADO', N'Rechazado', N'Registro simulado rechazado.', CONVERT(bit,1)),
        (N'CANCELADO', N'Cancelado', N'Registro simulado cancelado.', CONVERT(bit,1)),
        (N'REEMBOLSO_PARCIAL', N'Reembolso parcial', N'Reembolso parcial simulado.', CONVERT(bit,1)),
        (N'REEMBOLSADO', N'Reembolsado', N'Reembolso simulado.', CONVERT(bit,1))
    ) v(Codigo, Nombre, Descripcion, EsFinal)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.EstadoPago e WHERE e.Codigo = v.Codigo);

    INSERT INTO dbo.MetodoPago (Codigo, Nombre, Categoria)
    SELECT v.Codigo, v.Nombre, v.Categoria
    FROM (VALUES
        (N'TARJETA_CREDITO', N'Tarjeta de crédito', N'Tarjeta'),
        (N'TARJETA_DEBITO', N'Tarjeta de débito', N'Tarjeta'),
        (N'YAPE', N'Yape', N'BilleteraDigital'),
        (N'PLIN', N'Plin', N'BilleteraDigital'),
        (N'TRANSFERENCIA', N'Transferencia bancaria', N'Transferencia'),
        (N'EFECTIVO', N'Efectivo', N'Efectivo'),
        (N'OTRO', N'Otro medio de pago', N'Otro')
    ) v(Codigo, Nombre, Categoria)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.MetodoPago m WHERE m.Codigo = v.Codigo);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
