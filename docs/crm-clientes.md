# CRM de clientes

## Funciones

El directorio permite buscar por nombre completo, razón social, nombre comercial,
correo de acceso, correo de notificación o documento. Incluye filtros por estado,
categoría, actividad y sede autorizada, paginación de diez filas en la interfaz,
ficha con historial, alta manual y edición del perfil comercial.

Las tres categorías se calculan en cada consulta contando reservas con estado
`Completada` dentro del alcance autorizado:

| Categoría | Estadías completadas |
| --- | --- |
| Ocasional | 0–1 |
| Frecuente | 2–4 |
| VIP | 5 o más |

Una reserva completada cuenta como una estadía aunque contenga varias habitaciones.
Las reservas pendientes, confirmadas y canceladas no aumentan la categoría.
El check-out existente marca una reserva como completada cuando salen todas sus habitaciones.
La categoría se actualiza al consultar de nuevo el CRM; no se almacena ni se edita manualmente.

## Indicadores y filtros

- **Total clientes:** perfiles comerciales del resultado, incluidos los inactivos si no se filtra su estado.
- **Con reservas activas:** clientes distintos con al menos una reserva pendiente o confirmada
  que tenga una habitación sin check-out real y salida futura, o ingreso real sin salida registrada.
  Dos reservas activas del mismo cliente cuentan como un cliente en este indicador.
- **Nuevos este mes:** fecha de registro dentro del mes civil de Perú. Se convierten los límites
  del mes a UTC para compararlos con la fecha almacenada.

Los KPI usan los mismos filtros que la tabla y se calculan antes de paginar.
Pulsar una tarjeta filtra el resultado por actividad; pulsar de nuevo la tarjeta activa
quita ese filtro. «Total clientes» quita el filtro de actividad. «Limpiar filtros» restablece
la búsqueda y todos los filtros de la pantalla.

El administrador consulta clientes registrados manualmente en su sede o con reservas en ella.
Los conteos, importes, categorías e historial de reservas se limitan a esa sede.
La sede de referencia es la de la reserva más reciente del alcance o la sede de registro.
Un intento de solicitar otra sede devuelve `403`; editar un cliente ajeno devuelve `404`.
La API mantiene la política `Administrador` existente. No se modificó la matriz de roles.

## Alta y edición

El alta crea `Persona`, su identidad natural o jurídica, documento, contactos opcionales
y `Cliente` en una sola operación de guardado. `SedeRegistroId` permite encontrar un cliente
manual antes de su primera reserva. Los perfiles previos mantienen este campo en `NULL`.

La edición permite nombre/apellidos o razón social/nombre comercial, documento principal,
correo y teléfono de notificación y estado comercial. Conserva el tipo de persona y la sede
de origen. Se valida el catálogo de documentos, su compatibilidad, longitud y formato, además
de la unicidad de documentos y correos de contacto. Se rechazan duplicados en contactos o
documentos secundarios del mismo perfil, así como los pertenecientes a otras personas.

Los datos de identidad/contacto pertenecen al perfil de persona compartido. La edición
de notificaciones no cambia `Usuario.Correo`, contraseñas ni roles. Cambiar el estado comercial
no cancela reservas. Los importes pagados del historial siguen identificados como simulados.

El alta manual no crea ni vincula una cuenta de acceso. Si ya existe una persona con el
documento indicado, el alta pide revisar el perfil existente. El vínculo o activación posterior
de cuentas sobre perfiles manuales no forma parte de este módulo.

## Actualización de base de datos

Antes de ejecutar la nueva API sobre una base existente, aplicar:

```text
scripts/migrate-crm-clientes.sql
```

El script agrega únicamente `Cliente.SedeRegistroId INT NULL`, su relación con `Sede`
y un índice. Es transaccional e idempotente y comprueba el tipo de la columna si ya existe.
No reasigna sedes a clientes anteriores. Reiniciar la API después de actualizar el esquema.

## Contrato HTTP

| Método | Ruta | Resultado |
| --- | --- | --- |
| GET | `/api/clientes` | Página de clientes y `kpis` del resultado filtrado |
| GET | `/api/clientes/catalogos` | Sedes autorizadas y tipos de documento activos |
| POST | `/api/clientes` | Alta comercial; devuelve `201` y `clienteId` |
| PUT | `/api/clientes/{id}` | Edición del perfil; devuelve `204` |

Parámetros opcionales del listado: `buscar`, `estado` (`Activo`, `Inactivo`), `sedeId`,
`categoria` (`Ocasional`, `Frecuente`, `VIP`), `actividad` (`ConReservasActivas`, `NuevosMes`),
`pageNumber` y `pageSize` (máximo 100). Se conserva el formato paginado original y se agregan
los KPI y datos del perfil. Las páginas fuera del resultado se ajustan a la última disponible.

Ejemplo de alta, con identificadores válidos del catálogo:

```json
{
  "tipoPersona": "N",
  "nombre": "Ana",
  "apellidos": "Torres",
  "nombreComercial": null,
  "tipoDocumentoId": 1,
  "numeroDocumento": "12345678",
  "correoNotificacion": "ana@example.test",
  "telefono": "+51987654321",
  "estado": "Activo",
  "sedeRegistroId": null
}
```

Para un administrador con sede asignada, el alta usa esa sede aun si el campo llega `null`.
En una edición se envía el `sedeRegistroId` original del perfil.

## Verificación

Se cubren umbrales de categoría, filtros combinados, conteos de clientes distintos, límites
mensuales de Perú, estadías vencidas con ingreso real, paginación, aislamiento por sede,
alta sin reservas, edición sin alterar acceso y conflictos por duplicados.

Las pruebas SQL optativas crean bases `CasaAndinaTestCrm_<guid>` y las eliminan al finalizar.
Verifican persistencia, traducción real de las consultas, edición y ejecución repetida de la
migración sobre registros previos. Se habilitan con `CASAANDINA_TEST_SQLSERVER`.
La revisión de interfaz emplea datos ficticios en una página temporal separada de la aplicación.

La segmentación prepara el directorio para seleccionar públicos de ofertas y promociones.
Este cambio no incorpora envío de campañas.
