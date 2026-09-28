# Contrato de datos

## Fuente de verdad

El esquema contractual está en `casa-andina-frontend/BD CASA ANDINA.txt`. Es un
script destructivo para desarrollo: elimina y vuelve a crear las tablas. No debe
ejecutarse sobre una base con información que se necesite conservar.

La carga de sedes tiene una única fuente canónica:
`scripts/seed-sedes.sql`. El script es idempotente y se ejecuta después del
esquema. El archivo `llenado de datos 2.0.txt` se conserva como material de
trabajo histórico y no debe formar parte de una instalación automatizada.

Orden local de ejecución:

1. Ejecutar `BD CASA ANDINA.txt` para crear el esquema vacío y los catálogos
   básicos.
2. Ejecutar `scripts/seed-sedes.sql` para insertar o actualizar las 10 sedes.
3. Configurar la cadena `ConnectionStrings:DefaultConnection` de la API.
4. Iniciar la API y verificar los endpoints desde Swagger.

## Cobertura del modelo

| Tabla | Estado en EF Core | Actividad responsable |
| --- | --- | --- |
| Sede | Mapeada | Catálogo de sedes |
| Rol | Pendiente | Autenticación y roles |
| TipoHabitacion | Mapeada | Habitaciones |
| Comodidad | Mapeada | Habitaciones |
| Habitacion | Mapeada | Habitaciones |
| HabitacionComodidad | Mapeada como relación | Habitaciones |
| Servicio | Mapeada | Servicios |
| ServicioSede | Mapeada como relación | Servicios |
| Usuario | Parcial: faltan sede y datos operativos | Autenticación y roles |
| Cliente | Mapeada | CRM clientes |
| Reserva | Mapeada | Reservas |
| ReservaServicio | Pendiente | Reservas y servicios |
| Comprobante | Pendiente | Reservas y voucher |
| ConsultaChatbot | Pendiente | Chatbot y Dashboard BI |

## Decisiones

- Una reserva referencia una sola habitación mediante `Reserva.HabitacionId`.
  No existe una tabla `ReservaHabitacion` en el esquema actual.
- Las sedes no tienen operaciones de escritura en la API.
- Los estados "Frecuente" y "Nuevo" de clientes son valores calculados y no
  columnas de `Cliente`.
- No se deben generar migraciones hasta completar el mapeo de las tablas que
  intervienen en la actividad en desarrollo.

## Próxima alineación

La actividad de autenticación debe incorporar la entidad `Rol`, la relación
`Usuario-Rol`, la sede opcional del usuario y los campos `Telefono` y
`UltimoAcceso`. Los nombres utilizados por JWT y frontend deben ser estables y
no depender de identificadores numéricos insertados en un orden específico.
