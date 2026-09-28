-- RF04: catálogo maestro administrado únicamente en SQL. Este script es
-- idempotente: actualiza las sedes conocidas e inserta las que aún no existen.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Sede_Categoria')
        ALTER TABLE dbo.Sede DROP CONSTRAINT CK_Sede_Categoria;

    ALTER TABLE dbo.Sede WITH CHECK ADD CONSTRAINT CK_Sede_Categoria
        CHECK (Categoria IN (N'Premium', N'Select', N'Standard', N'Asociado'));

    DECLARE @Sedes TABLE (
        Codigo NVARCHAR(20) NOT NULL,
        Nombre NVARCHAR(150) NOT NULL,
        Region NVARCHAR(30) NOT NULL,
        Ciudad NVARCHAR(100) NOT NULL,
        Direccion NVARCHAR(250) NULL,
        Categoria NVARCHAR(30) NOT NULL,
        Telefono NVARCHAR(20) NULL
    );

    INSERT INTO @Sedes (Codigo, Nombre, Region, Ciudad, Direccion, Categoria, Telefono) VALUES
        (N'TRU-STD', N'Casa Andina Standard Trujillo Plaza', N'Costa', N'Trujillo', N'Jr. Diego de Almagro 586, Trujillo', N'Standard', N'+51 44 481018'),
        (N'MIR-PRE', N'Casa Andina Premium Miraflores', N'Costa', N'Lima', N'Av. La Paz 463, Miraflores, Lima', N'Premium', N'+51 1 213 9739'),
        (N'SIS-PRE', N'Casa Andina Premium San Isidro', N'Costa', N'Lima', N'Calle Las Orquídeas 505-527, San Isidro, Lima', N'Premium', N'+51 1 391 6500'),
        (N'NAZ-STD', N'Casa Andina Standard Nasca', N'Costa', N'Nazca (Ica)', N'Jr. Bolognesi 367, Nazca, Ica', N'Standard', N'+51 56 523563'),
        (N'ARQ-PRE', N'Casa Andina Premium Arequipa', N'Sierra', N'Arequipa', N'Centro histórico de Arequipa', N'Premium', N'+51 54 000000'),
        (N'CUS-PRE', N'Casa Andina Premium Cusco', N'Sierra', N'Cusco', N'Cerca de la Plaza de Armas, Cusco', N'Premium', N'+51 84 000000'),
        (N'PIU-PRE', N'Casa Andina Premium Piura', N'Costa', N'Piura', N'Av. Ramón Mujica S/N, Urb. San Eduardo, Piura', N'Premium', N'+51 73 000000'),
        (N'HYO-ASO', N'Hotel de Turistas Huancayo - Hotel Asociado Casa Andina', N'Sierra', N'Huancayo (Junín)', N'Jr. Ancash 729, Huancayo', N'Asociado', N'+51 64 000000'),
        (N'HRZ-ASO', N'Hotel Andino Club - Hotel Asociado Casa Andina', N'Sierra', N'Huaraz (Áncash)', N'Jr. Pedro Cochachín 357, Huaraz', N'Asociado', N'+51 43 421662'),
        (N'CHI-SEL', N'Casa Andina Select Chiclayo', N'Costa', N'Chiclayo (Lambayeque)', N'Av. Federico Villarreal 115, Chiclayo', N'Select', N'+51 74 000000');

    IF (SELECT COUNT(*) FROM @Sedes) <> 10
        THROW 51000, 'El catálogo debe contener exactamente 10 sedes.', 1;

    MERGE dbo.Sede WITH (HOLDLOCK) AS destino
    USING @Sedes AS origen ON destino.Codigo = origen.Codigo
    WHEN MATCHED THEN UPDATE SET
        Nombre = origen.Nombre,
        Region = origen.Region,
        Ciudad = origen.Ciudad,
        Direccion = origen.Direccion,
        Categoria = origen.Categoria,
        Telefono = origen.Telefono,
        Activo = 1
    WHEN NOT MATCHED THEN INSERT
        (Nombre, Codigo, Region, Ciudad, Direccion, Categoria, Telefono, Activo)
        VALUES
        (origen.Nombre, origen.Codigo, origen.Region, origen.Ciudad,
         origen.Direccion, origen.Categoria, origen.Telefono, 1);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT SedeId, Codigo, Nombre, Region, Ciudad, Categoria, Activo
FROM dbo.Sede
ORDER BY Ciudad, Nombre;
