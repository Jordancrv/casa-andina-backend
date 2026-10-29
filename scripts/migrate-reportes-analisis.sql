-- Ejecutar en la base seleccionada antes de iniciar la versión con historial de reportes.
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID(N'dbo.ReporteAnalisis', N'U') IS NULL
 BEGIN
   CREATE TABLE dbo.ReporteAnalisis (
     ReporteAnalisisId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReporteAnalisis PRIMARY KEY,
     SedeId int NOT NULL CONSTRAINT FK_ReporteAnalisis_Sede REFERENCES dbo.Sede(SedeId),
     UsuarioId int NOT NULL CONSTRAINT FK_ReporteAnalisis_Usuario REFERENCES dbo.Usuario(UsuarioId),
     Periodo nvarchar(10) NOT NULL CONSTRAINT CK_ReporteAnalisis_Periodo CHECK (Periodo IN (N'7d', N'30d', N'mes')),
     GeneradoUtc datetime2 NOT NULL,
     ContenidoJson nvarchar(max) NOT NULL CONSTRAINT CK_ReporteAnalisis_Json CHECK (ISJSON(ContenidoJson) = 1)
   );
   CREATE INDEX IX_ReporteAnalisis_Sede_Periodo_Fecha ON dbo.ReporteAnalisis(SedeId, Periodo, GeneradoUtc DESC);
 END
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
