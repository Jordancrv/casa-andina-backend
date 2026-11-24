SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Cliente','RecordarPlaya') IS NULL
    ALTER TABLE dbo.Cliente ADD RecordarPlaya bit NOT NULL CONSTRAINT DF_Cliente_RecordarPlaya DEFAULT 0;
IF COL_LENGTH('dbo.Cliente','RecordarEvitarLluvia') IS NULL
    ALTER TABLE dbo.Cliente ADD RecordarEvitarLluvia bit NOT NULL CONSTRAINT DF_Cliente_RecordarEvitarLluvia DEFAULT 0;
IF OBJECT_ID('dbo.ValoracionViaje','U') IS NULL
    CREATE TABLE dbo.ValoracionViaje (
        Id int IDENTITY PRIMARY KEY, ClienteId int NOT NULL REFERENCES dbo.Cliente(ClienteId),
        FechaUtc datetime2 NOT NULL, Util bit NOT NULL, Motivo nvarchar(500) NULL,
        Ciudad nvarchar(100) NULL, PlanGeneradoEn datetimeoffset NOT NULL,
        UNIQUE (ClienteId,PlanGeneradoEn)
    );
COMMIT;
