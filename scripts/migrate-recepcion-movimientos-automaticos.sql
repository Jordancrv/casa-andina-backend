-- Permite movimientos automáticos sin usuario y conserva la integridad temporal.
-- Ejecutar en la base correspondiente; no modifica ni elimina reservas.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.ReservaHabitacion', N'U') IS NULL
       OR COL_LENGTH(N'dbo.ReservaHabitacion', N'FechaCheckInReal') IS NULL
       OR COL_LENGTH(N'dbo.ReservaHabitacion', N'FechaCheckOutReal') IS NULL
       OR COL_LENGTH(N'dbo.ReservaHabitacion', N'CheckInPorUsuarioId') IS NULL
       OR COL_LENGTH(N'dbo.ReservaHabitacion', N'CheckOutPorUsuarioId') IS NULL
        THROW 50001, 'Ejecuta primero la migración de recepción.', 1;

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.ReservaHabitacion') AND name = N'CK_ReservaHabitacion_Movimientos')
        ALTER TABLE dbo.ReservaHabitacion DROP CONSTRAINT CK_ReservaHabitacion_Movimientos;

    ALTER TABLE dbo.ReservaHabitacion WITH CHECK ADD CONSTRAINT CK_ReservaHabitacion_Movimientos CHECK (
        (CheckInPorUsuarioId IS NULL OR FechaCheckInReal IS NOT NULL)
        AND (CheckOutPorUsuarioId IS NULL OR FechaCheckOutReal IS NOT NULL)
        AND (FechaCheckOutReal IS NULL OR (FechaCheckInReal IS NOT NULL AND FechaCheckOutReal >= FechaCheckInReal))
    );
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
