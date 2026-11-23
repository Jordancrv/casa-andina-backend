SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID(N'dbo.BloqueoHabitacion',N'U') IS NULL
 BEGIN
  CREATE TABLE dbo.BloqueoHabitacion (
   BloqueoHabitacionId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
   HabitacionId int NOT NULL REFERENCES dbo.Habitacion(HabitacionId),
   Inicio date NOT NULL, FinExclusivo date NOT NULL, Motivo nvarchar(200) NOT NULL,
   Activo bit NOT NULL, CreadoPorUsuarioId int NOT NULL REFERENCES dbo.Usuario(UsuarioId), CreadoUtc datetime2 NOT NULL,
   CanceladoPorUsuarioId int NULL REFERENCES dbo.Usuario(UsuarioId), CanceladoUtc datetime2 NULL,
   CONSTRAINT CK_BloqueoHabitacion_Fechas CHECK (FinExclusivo > Inicio),
   CONSTRAINT CK_BloqueoHabitacion_Cancelacion CHECK ((Activo=1 AND CanceladoPorUsuarioId IS NULL AND CanceladoUtc IS NULL) OR (Activo=0 AND CanceladoPorUsuarioId IS NOT NULL AND CanceladoUtc IS NOT NULL))
  );
  CREATE INDEX IX_BloqueoHabitacion_HabitacionId_Inicio_FinExclusivo ON dbo.BloqueoHabitacion(HabitacionId,Inicio,FinExclusivo);
 END;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
