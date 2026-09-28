# Casa Andina Backend

API REST en .NET 8 organizada en las capas `Domain`, `Application`,
`Infrastructure` y `Api`.

## Catálogo de sedes

Las sedes son datos maestros de solo lectura para la aplicación. No existen
endpoints `POST`, `PUT`, `PATCH` ni `DELETE`; su administración se realiza con
[`scripts/seed-sedes.sql`](scripts/seed-sedes.sql).

Endpoints disponibles:

- `GET /api/sedes`: lista las sedes activas.
- `GET /api/sedes?region=Costa`: filtra por región.
- `GET /api/sedes?ciudad=Lima`: filtra por ciudad.
- `GET /api/sedes/{id}`: devuelve una sede activa o responde `404`.

El script puede ejecutarse más de una vez: identifica cada sede por su código,
actualiza sus datos e inserta únicamente las que no existen.

## Ejecución local

1. Configurar `ConnectionStrings:DefaultConnection` y los valores de JWT en
   `src/CasaAndina.Api/appsettings.json` o mediante secretos locales.
2. Crear la base de datos con el esquema del proyecto.
3. Ejecutar `scripts/seed-sedes.sql` en SQL Server.
4. Iniciar la API con `dotnet run --project src/CasaAndina.Api`.

Si la base ya existía antes de la implementación de roles, ejecutar una sola
vez `scripts/migrate-auth-roles.sql` en lugar de recrear las tablas.

Swagger está disponible en el entorno `Development`.

La clave JWT no se almacena en el repositorio. Para desarrollo puede definirse
como variable de entorno antes de iniciar la API:

```powershell
$env:Jwt__Key = "una-clave-local-de-al-menos-32-caracteres"
```

El inicio de sesión acepta personal interno de `Usuario` y huéspedes con cuenta
en `Cliente`. Las contraseñas deben almacenarse exclusivamente con BCrypt.

## Contrato de datos

El inventario de tablas, las reglas de mantenimiento y las brechas pendientes
del modelo se encuentran en [`docs/data-model.md`](docs/data-model.md).
