-- Ejecutar antes de desplegar el backend.
IF OBJECT_ID(N'dbo.ClienteObservacionSede', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClienteObservacionSede (
        ClienteId int NOT NULL,
        SedeId int NOT NULL,
        Observacion nvarchar(2000) NOT NULL,
        ActualizadoPorUsuarioId int NULL,
        ActualizadoUtc datetime2 NOT NULL,
        CONSTRAINT PK_ClienteObservacionSede PRIMARY KEY (ClienteId, SedeId),
        CONSTRAINT FK_ClienteObservacionSede_Cliente FOREIGN KEY (ClienteId) REFERENCES dbo.Cliente(ClienteId),
        CONSTRAINT FK_ClienteObservacionSede_Sede FOREIGN KEY (SedeId) REFERENCES dbo.Sede(SedeId),
        CONSTRAINT FK_ClienteObservacionSede_Usuario FOREIGN KEY (ActualizadoPorUsuarioId) REFERENCES dbo.Usuario(UsuarioId)
    );
END;
