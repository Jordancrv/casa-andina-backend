-- Ejecutar antes de desplegar el backend que utiliza el estado Reservado.
-- Conservar una copia de seguridad y detener escrituras durante la migración.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Reserva', N'U') IS NULL
        THROW 50001, 'No existe dbo.Reserva en la base seleccionada.', 1;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Reserva') AND name = N'CK_Reserva_Estado')
        ALTER TABLE dbo.Reserva DROP CONSTRAINT CK_Reserva_Estado;
    UPDATE dbo.Reserva SET Estado = N'Reservado' WHERE Estado = N'Confirmada';
    ALTER TABLE dbo.Reserva WITH CHECK ADD CONSTRAINT CK_Reserva_Estado
        CHECK (Estado IN (N'Pendiente', N'Reservado', N'Bloqueada', N'Completada', N'Cancelada'));
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
