SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.AsignacionHuesped', N'DatosHuespedJson') IS NULL
    ALTER TABLE dbo.AsignacionHuesped ADD DatosHuespedJson nvarchar(max) NULL;
COMMIT;
