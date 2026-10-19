-- Ejecutar antes de desplegar el backend, sobre la BD local y después Azure.
-- No crea bases ni elimina datos. Idempotente; fechas reales en UTC.
SET XACT_ABORT ON;
BEGIN TRY
  BEGIN TRANSACTION;
  IF OBJECT_ID(N'dbo.ReservaHabitacion', N'U') IS NULL OR OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
    THROW 50001, 'Faltan las tablas ReservaHabitacion o Usuario.', 1;

  IF COL_LENGTH('dbo.ReservaHabitacion', 'FechaCheckInReal') IS NULL
    EXEC(N'ALTER TABLE dbo.ReservaHabitacion ADD FechaCheckInReal datetime2(7) NULL;');
  IF COL_LENGTH('dbo.ReservaHabitacion', 'CheckInPorUsuarioId') IS NULL
    EXEC(N'ALTER TABLE dbo.ReservaHabitacion ADD CheckInPorUsuarioId int NULL;');
  IF COL_LENGTH('dbo.ReservaHabitacion', 'FechaCheckOutReal') IS NULL
    EXEC(N'ALTER TABLE dbo.ReservaHabitacion ADD FechaCheckOutReal datetime2(7) NULL;');
  IF COL_LENGTH('dbo.ReservaHabitacion', 'CheckOutPorUsuarioId') IS NULL
    EXEC(N'ALTER TABLE dbo.ReservaHabitacion ADD CheckOutPorUsuarioId int NULL;');

  IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ReservaHabitacion_CheckInUsuario' AND parent_object_id = OBJECT_ID('dbo.ReservaHabitacion'))
    EXEC(N'ALTER TABLE dbo.ReservaHabitacion WITH CHECK ADD CONSTRAINT FK_ReservaHabitacion_CheckInUsuario FOREIGN KEY(CheckInPorUsuarioId) REFERENCES dbo.Usuario(UsuarioId);');
  IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ReservaHabitacion_CheckOutUsuario' AND parent_object_id = OBJECT_ID('dbo.ReservaHabitacion'))
    EXEC(N'ALTER TABLE dbo.ReservaHabitacion WITH CHECK ADD CONSTRAINT FK_ReservaHabitacion_CheckOutUsuario FOREIGN KEY(CheckOutPorUsuarioId) REFERENCES dbo.Usuario(UsuarioId);');
  IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ReservaHabitacion_Movimientos' AND parent_object_id = OBJECT_ID('dbo.ReservaHabitacion'))
    EXEC(N'ALTER TABLE dbo.ReservaHabitacion WITH CHECK ADD CONSTRAINT CK_ReservaHabitacion_Movimientos CHECK (
      ((FechaCheckInReal IS NULL AND CheckInPorUsuarioId IS NULL) OR (FechaCheckInReal IS NOT NULL AND CheckInPorUsuarioId IS NOT NULL))
      AND ((FechaCheckOutReal IS NULL AND CheckOutPorUsuarioId IS NULL) OR (FechaCheckOutReal IS NOT NULL AND CheckOutPorUsuarioId IS NOT NULL))
      AND (FechaCheckOutReal IS NULL OR (FechaCheckInReal IS NOT NULL AND FechaCheckOutReal >= FechaCheckInReal))
    );');
  IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ReservaHabitacion_EstadiaActiva' AND object_id = OBJECT_ID('dbo.ReservaHabitacion'))
    EXEC(N'CREATE UNIQUE INDEX UX_ReservaHabitacion_EstadiaActiva ON dbo.ReservaHabitacion(HabitacionId) WHERE FechaCheckInReal IS NOT NULL AND FechaCheckOutReal IS NULL;');
  COMMIT TRANSACTION;
END TRY
BEGIN CATCH
  IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
  THROW;
END CATCH;
