# Contrato de datos

## Fuente de verdad

El DDL de instalación está en `casa-andina-frontend/BD CASA ANDINA.txt`. Es destructivo y solo se usa para una base vacía o descartable. Las bases con información se actualizan mediante scripts versionados de `scripts/`; para el modelo normalizado final, consultar [`personas-reservas-model.md`](personas-reservas-model.md) y aplicar `migrate-estructura-final.sql` después de las migraciones previas que falten.

Las sedes son datos maestros y su carga canónica está en [`scripts/seed-sedes.sql`](../scripts/seed-sedes.sql).

## Reglas del modelo

- `Usuario` referencia a `Persona`; `SedeId` es nullable para clientes y cuentas corporativas.
- `Usuario.Correo` es el único correo de inicio de sesión, obligatorio y único entre cuentas. Login, registro y edición de acceso consultan este campo; no se autentica con correos de `PersonaContacto`.
- Los permisos se asignan mediante `UsuarioRol`, no con una columna de rol en `Usuario`.
- Documentos y contactos de notificación pertenecen a `Persona`; los correos de `PersonaContacto` son únicos, pero no identifican cuentas para el login. Cambiar el correo de acceso no modifica contactos. `Cliente` y `Huesped` son perfiles, no duplicados de sus datos personales.
- `Reserva` exige un `ClienteId` y un `SedeId`.
- Las habitaciones y fechas de una operación residen en `ReservaHabitacion`; cada reserva debe pertenecer a una sola sede.
- `AsignacionHuesped` identifica ocupantes por habitación reservada.
- `Comprobante` se relaciona 1:1 con `Reserva` y guarda si se emite boleta o factura; RUC/razón social se consultan desde Persona y sus documentos, no se repiten.
- `Habitacion` debe tener sede y tipo; las comodidades se relacionan mediante `HabitacionComodidad`.
- `Auditoria` mantiene el historial técnico. Su tabla no implica que todos los eventos de aplicación ya estén instrumentados.
- Los estados de cliente derivados, como frecuente/nuevo, se calculan y no se almacenan como duplicados.
- El CRM calcula Ocasional (0–1), Frecuente (2–4) y VIP (5 o más) a partir de reservas completadas en el alcance autorizado. El alta manual conserva `Cliente.SedeRegistroId` para dar visibilidad al perfil antes de su primera reserva. Aplicar [`migrate-crm-clientes.sql`](../scripts/migrate-crm-clientes.sql) sobre bases existentes; ver [contrato del CRM](crm-clientes.md).

La actualización de bases existentes se realiza con [`migrate-usuario-correo-acceso.sql`](../scripts/migrate-usuario-correo-acceso.sql). El script copia cada correo principal actual a `Usuario.Correo`, valida cuentas incompletas/duplicadas y agrega el índice único; no elimina ni altera `PersonaContacto`.
