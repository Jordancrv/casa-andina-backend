-- RF04: Sede es de solo lectura desde la API. Se carga únicamente por script SQL,
-- nunca por un endpoint CRUD del backend. Ejecutar después de aplicar las migraciones.

SET IDENTITY_INSERT Sedes ON;

INSERT INTO Sedes (Id, Nombre, Ciudad, Direccion, Activo, FechaCreacion) VALUES
(1, N'Casa Andina Premium Lima Miraflores', N'Lima',     N'Av. La Paz 463, Miraflores', 1, SYSUTCDATETIME()),
(2, N'Casa Andina Select Cusco',            N'Cusco',    N'Av. El Sol 555, Cusco',      1, SYSUTCDATETIME()),
(3, N'Casa Andina Classic Trujillo Colonial', N'Trujillo', N'Jr. Independencia 616, Trujillo', 1, SYSUTCDATETIME()),
(4, N'Casa Andina Select Arequipa',         N'Arequipa', N'Av. Puente Grau 104, Arequipa', 1, SYSUTCDATETIME()),
(5, N'Casa Andina Classic Puno',            N'Puno',     N'Jr. Independencia 187, Puno',  1, SYSUTCDATETIME());

SET IDENTITY_INSERT Sedes OFF;
