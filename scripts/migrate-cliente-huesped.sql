SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.ClienteHuesped', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ClienteHuesped (
  ClienteHuespedId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
  ClienteId int NOT NULL REFERENCES dbo.Cliente(ClienteId),
  HuespedId int NOT NULL REFERENCES dbo.Huesped(HuespedId),
  Nombres nvarchar(200) NOT NULL, Apellidos nvarchar(100) NOT NULL,
  Dni nvarchar(8) NOT NULL, FechaNacimiento date NOT NULL, Sexo nvarchar(1) NOT NULL,
  Correo nvarchar(150) NOT NULL, Telefono nvarchar(16) NULL,
  CONSTRAINT UQ_ClienteHuesped_Huesped UNIQUE (ClienteId,HuespedId),
  CONSTRAINT UQ_ClienteHuesped_Dni UNIQUE (ClienteId,Dni)
 );
END;
COMMIT;
