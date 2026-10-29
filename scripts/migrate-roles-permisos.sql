-- Ejecutar como archivo independiente en SSMS; GO separa el ámbito de variables.
GO
-- Ejecutar antes de desplegar el backend. SQL Server; conserva asignaciones existentes.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Rol','SedeId') IS NULL
BEGIN
 ALTER TABLE dbo.Rol ADD SedeId int NULL REFERENCES dbo.Sede(SedeId), Activo bit NOT NULL CONSTRAINT DF_Rol_Activo DEFAULT 1, Protegido bit NOT NULL CONSTRAINT DF_Rol_Protegido DEFAULT 1, Version uniqueidentifier NOT NULL CONSTRAINT DF_Rol_Version DEFAULT NEWID();
END;
IF OBJECT_ID('dbo.Permiso') IS NULL
 CREATE TABLE dbo.Permiso(Codigo nvarchar(80) NOT NULL PRIMARY KEY, Seccion nvarchar(40) NOT NULL, Delegable bit NOT NULL);
IF OBJECT_ID('dbo.RolPermiso') IS NULL
 CREATE TABLE dbo.RolPermiso(RolId int NOT NULL REFERENCES dbo.Rol(RolId), PermisoCodigo nvarchar(80) NOT NULL REFERENCES dbo.Permiso(Codigo), PRIMARY KEY(RolId,PermisoCodigo));
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'dashboard.ver') INSERT dbo.Permiso VALUES(N'dashboard.ver',N'dashboard',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'calendario.ver') INSERT dbo.Permiso VALUES(N'calendario.ver',N'calendario',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'recepcion.ver') INSERT dbo.Permiso VALUES(N'recepcion.ver',N'recepcion',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'recepcion.ingreso') INSERT dbo.Permiso VALUES(N'recepcion.ingreso',N'recepcion',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'recepcion.salida') INSERT dbo.Permiso VALUES(N'recepcion.salida',N'recepcion',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'clientes.ver') INSERT dbo.Permiso VALUES(N'clientes.ver',N'clientes',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'clientes.crear') INSERT dbo.Permiso VALUES(N'clientes.crear',N'clientes',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'clientes.editar') INSERT dbo.Permiso VALUES(N'clientes.editar',N'clientes',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'usuarios.ver') INSERT dbo.Permiso VALUES(N'usuarios.ver',N'usuarios',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'usuarios.editar') INSERT dbo.Permiso VALUES(N'usuarios.editar',N'usuarios',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'usuarios.desactivar') INSERT dbo.Permiso VALUES(N'usuarios.desactivar',N'usuarios',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'usuarios.asignar') INSERT dbo.Permiso VALUES(N'usuarios.asignar',N'usuarios',0);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'habitaciones.ver') INSERT dbo.Permiso VALUES(N'habitaciones.ver',N'habitaciones',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'habitaciones.crear') INSERT dbo.Permiso VALUES(N'habitaciones.crear',N'habitaciones',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'habitaciones.editar') INSERT dbo.Permiso VALUES(N'habitaciones.editar',N'habitaciones',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'habitaciones.desactivar') INSERT dbo.Permiso VALUES(N'habitaciones.desactivar',N'habitaciones',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'servicios.ver') INSERT dbo.Permiso VALUES(N'servicios.ver',N'servicios',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'servicios.crear') INSERT dbo.Permiso VALUES(N'servicios.crear',N'servicios',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'servicios.editar') INSERT dbo.Permiso VALUES(N'servicios.editar',N'servicios',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'servicios.desactivar') INSERT dbo.Permiso VALUES(N'servicios.desactivar',N'servicios',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'comodidades.ver') INSERT dbo.Permiso VALUES(N'comodidades.ver',N'comodidades',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'comodidades.crear') INSERT dbo.Permiso VALUES(N'comodidades.crear',N'comodidades',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'reportes.ver') INSERT dbo.Permiso VALUES(N'reportes.ver',N'reportes',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'reportes.analizar') INSERT dbo.Permiso VALUES(N'reportes.analizar',N'reportes',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'reportes.exportar') INSERT dbo.Permiso VALUES(N'reportes.exportar',N'reportes',1);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'roles.ver') INSERT dbo.Permiso VALUES(N'roles.ver',N'roles',0);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'roles.gestionar') INSERT dbo.Permiso VALUES(N'roles.gestionar',N'roles',0);
IF NOT EXISTS(SELECT 1 FROM dbo.Permiso WHERE Codigo=N'sedes.ver') INSERT dbo.Permiso VALUES(N'sedes.ver',N'sedes',0);
IF NOT EXISTS (SELECT 1 FROM dbo.RolPermiso)
BEGIN
 INSERT dbo.RolPermiso SELECT r.RolId,p.Codigo FROM dbo.Rol r CROSS JOIN dbo.Permiso p WHERE r.Codigo=N'ADMINISTRADOR' AND p.Codigo IN (N'dashboard.ver',N'calendario.ver',N'recepcion.ver',N'recepcion.ingreso',N'recepcion.salida',N'clientes.ver',N'clientes.crear',N'clientes.editar',N'usuarios.ver',N'usuarios.editar',N'usuarios.desactivar',N'usuarios.asignar',N'habitaciones.ver',N'habitaciones.crear',N'habitaciones.editar',N'habitaciones.desactivar',N'servicios.ver',N'servicios.crear',N'servicios.editar',N'servicios.desactivar',N'comodidades.ver',N'comodidades.crear',N'reportes.ver',N'reportes.analizar',N'reportes.exportar',N'roles.ver',N'roles.gestionar');
 INSERT dbo.RolPermiso SELECT r.RolId,p.Codigo FROM dbo.Rol r CROSS JOIN dbo.Permiso p WHERE r.Codigo=N'SUPERADMINISTRADOR' AND p.Codigo IN (N'calendario.ver',N'habitaciones.ver',N'habitaciones.crear',N'habitaciones.editar',N'habitaciones.desactivar',N'servicios.ver',N'servicios.crear',N'servicios.editar',N'servicios.desactivar',N'comodidades.ver',N'comodidades.crear',N'roles.ver',N'roles.gestionar',N'sedes.ver');
 INSERT dbo.RolPermiso SELECT r.RolId,p.Codigo FROM dbo.Rol r CROSS JOIN dbo.Permiso p WHERE r.Codigo=N'RECEPCIONISTA' AND p.Codigo IN (N'calendario.ver',N'habitaciones.ver',N'recepcion.ver',N'recepcion.ingreso',N'recepcion.salida');
 INSERT dbo.RolPermiso SELECT r.RolId,p.Codigo FROM dbo.Rol r CROSS JOIN dbo.Permiso p WHERE r.Codigo=N'OPERACIONES' AND p.Codigo IN (N'calendario.ver',N'habitaciones.ver',N'habitaciones.crear',N'habitaciones.editar',N'habitaciones.desactivar',N'servicios.ver',N'servicios.crear',N'servicios.editar',N'servicios.desactivar',N'comodidades.ver',N'comodidades.crear');
 INSERT dbo.RolPermiso SELECT r.RolId,p.Codigo FROM dbo.Rol r CROSS JOIN dbo.Permiso p WHERE r.Codigo=N'MANTENIMIENTO' AND p.Codigo IN (N'calendario.ver',N'habitaciones.ver',N'habitaciones.crear',N'habitaciones.editar',N'habitaciones.desactivar');
 INSERT dbo.RolPermiso SELECT r.RolId,p.Codigo FROM dbo.Rol r CROSS JOIN dbo.Permiso p WHERE r.Codigo=N'RECEPCION' AND p.Codigo IN (N'calendario.ver',N'habitaciones.ver',N'recepcion.ver',N'recepcion.ingreso',N'recepcion.salida');
END;
-- Sustituir la unicidad global del nombre por unicidad dentro de la sede.
DECLARE @RolesPermisosDropNombreSql nvarchar(max)=N'';
SELECT @RolesPermisosDropNombreSql=@RolesPermisosDropNombreSql+ CASE WHEN i.is_unique_constraint=1 THEN N'ALTER TABLE dbo.Rol DROP CONSTRAINT '+QUOTENAME(i.name)+N';' ELSE N'DROP INDEX '+QUOTENAME(i.name)+N' ON dbo.Rol;' END
FROM sys.indexes i WHERE i.object_id=OBJECT_ID('dbo.Rol') AND i.is_unique=1 AND i.is_primary_key=0
AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=1
AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND c.name='Nombre');
EXEC sp_executesql @RolesPermisosDropNombreSql;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Rol') AND name='UX_Rol_Sede_Nombre') EXEC(N'CREATE UNIQUE INDEX UX_Rol_Sede_Nombre ON dbo.Rol(SedeId,Nombre)');
COMMIT;
GO
