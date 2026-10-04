# Modelo normalizado de personas y reservas

## Estructura final

| Tabla | Responsabilidad |
|---|---|
| `Sede` | Establecimientos del hotel. |
| `Usuario` | Cuenta de acceso vinculada 1:1 con `Persona`; `SedeId` es opcional. |
| `Rol`, `UsuarioRol` | Catálogo de roles y asignación muchos-a-muchos. |
| `Persona` | Identidad común para cuentas, clientes y huéspedes. |
| `PersonaNatural`, `PersonaJuridica` | Subtipos con atributos propios. |
| `TipoDocumento`, `PersonaDocumento` | Catálogo y documentos asociados a la persona. |
| `PersonaContacto` | Correos y teléfonos sin duplicar datos en usuario, cliente o huésped. |
| `Cliente` | Perfil comprador/titular vinculado 1:1 a una persona natural o jurídica. |
| `Huesped` | Perfil de hospedaje vinculado exclusivamente a una persona natural. |
| `Reserva` | Cabecera asociada obligatoriamente a un cliente y a una sede. |
| `ReservaHabitacion` | Habitación, fechas, ocupación, precio unitario acordado y subtotal de cada detalle. |
| `AsignacionHuesped` | Huéspedes asignados a cada habitación reservada. |
| `ReservaServicio` | Servicios vendidos, cantidad, precio unitario y subtotal calculado. |
| `Comprobante` | Comprobante 1:1 con reserva; distingue `Boleta` o `Factura`. Los datos fiscales se consultan desde la persona del cliente y sus documentos. |
| `Servicio` | Catálogo de servicios ofrecidos. |
| `Habitacion` | Habitación asociada obligatoriamente a `Sede` y `TipoHabitacion`. |
| `TipoHabitacion` | Clasificación y capacidad general de habitación. |
| `Comodidad`, `HabitacionComodidad` | Catálogo de comodidades y relación muchos-a-muchos con habitaciones. |
| `Auditoria` | Registro estructurado de cambios y eventos relevantes. |

## Relaciones y reglas

```text
Sede 1 ── N Usuario (SedeId opcional)
Usuario N ── M Rol, mediante UsuarioRol
Persona 1 ── 0..1 Usuario / 0..1 Cliente / 0..1 Huesped
Persona 1 ── 0..1 PersonaNatural o PersonaJuridica
Persona 1 ── N PersonaDocumento y PersonaContacto
Cliente 1 ── N Reserva; cada Reserva pertenece a una Sede
Reserva 1 ── N ReservaHabitacion N ── 1 Habitacion
ReservaHabitacion N ── M Huesped, mediante AsignacionHuesped
Reserva 1 ── N ReservaServicio N ── 1 Servicio
Reserva 1 ── 0..1 Comprobante (Boleta o Factura)
Sede 1 ── N Habitacion N ── 1 TipoHabitacion
Habitacion N ── M Comodidad, mediante HabitacionComodidad
Auditoria N ── 0..1 Usuario y 0..1 Sede
```

`PrecioAcordado` representa el precio unitario congelado al contratar (por noche, cuando aplique); `Subtotal` guarda el importe del detalle. Para migrar datos históricos cuyo precio unitario no estaba almacenado, el subtotal antiguo se conserva y se copia provisionalmente a `PrecioAcordado`; esa fila debe revisarse si se requiere reconstruir la tarifa por noche.

`Auditoria` es solo la estructura persistente. El sistema todavía debe incorporar el servicio/interceptor que escriba eventos; no se debe registrar contraseñas, hashes, tokens ni otros secretos en `Antes` o `Despues`.

## Instalación y actualización

`casa-andina-frontend/BD CASA ANDINA.txt` es el DDL de instalación de desarrollo y recrea las tablas. Solo debe ejecutarse en una base vacía o descartable, nunca para actualizar la base operativa.

Para una base existente en la que ya se aplicaron las migraciones previas, ejecutar desde `casa-andina-backend`:

1. `scripts/migrate-estructura-final.sql`.

Si todavía no se aplicaron, primero ejecutar `migrate-personas-reservas.sql` y luego `migrate-reservas-multiples-habitaciones.sql`. La migración final incorpora `TipoComprobante` con `Boleta` por defecto para comprobantes preexistentes y restringe el catálogo a `Boleta`/`Factura`. La normalización fue aplicada a `CasaAndinaDB` el 4 de octubre de 2026; para otros entornos, hacer backup y probar sobre una copia antes de ejecutar los scripts.

La migración se detiene si hay reservas sin habitación/sede, reservas con habitaciones de más de una sede, o códigos de rol duplicados, porque esos casos necesitan una decisión de negocio y no deben resolverse silenciosamente.
