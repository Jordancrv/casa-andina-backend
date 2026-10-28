-- Ejecutar una vez en la base de Casa Andina. Se puede repetir sin borrar datos.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Cliente', N'U') IS NULL OR OBJECT_ID(N'dbo.Reserva', N'U') IS NULL
        THROW 51000, 'Se requieren las tablas Cliente y Reserva.', 1;
    IF OBJECT_ID(N'dbo.ConexionCalendario', N'U') IS NULL
        CREATE TABLE dbo.ConexionCalendario (
            ClienteId INT NOT NULL CONSTRAINT PK_ConexionCalendario PRIMARY KEY,
            CalendarioId NVARCHAR(1024) NULL,
            RefreshTokenProtegido NVARCHAR(4000) NULL,
            RequiereReconectar BIT NOT NULL CONSTRAINT DF_Calendario_Reconectar DEFAULT 0,
            UltimaSincronizacionUtc DATETIME2 NULL,
            CONSTRAINT FK_Calendario_Cliente FOREIGN KEY (ClienteId) REFERENCES dbo.Cliente(ClienteId)
        );
    IF OBJECT_ID(N'dbo.EventoCalendario', N'U') IS NULL
        CREATE TABLE dbo.EventoCalendario (
            ClienteId INT NOT NULL,
            ReservaId INT NOT NULL,
            EventoId NVARCHAR(64) NOT NULL,
            Huella NVARCHAR(64) NULL,
            CONSTRAINT PK_EventoCalendario PRIMARY KEY (ClienteId, ReservaId),
            CONSTRAINT FK_Evento_Conexion FOREIGN KEY (ClienteId) REFERENCES dbo.ConexionCalendario(ClienteId),
            CONSTRAINT FK_Evento_Reserva FOREIGN KEY (ReservaId) REFERENCES dbo.Reserva(ReservaId)
        );
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
