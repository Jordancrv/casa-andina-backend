# Recepción: check-in y check-out por habitación

## Instalación

1. Respaldar la base de datos que se va a actualizar.
2. Ejecutar `scripts/migrate-recepcion.sql` en la base local. No usa `CREATE DATABASE` ni `USE`.
3. Reiniciar backend y frontend. Probar con reservas confirmadas de la sede de la cuenta.
4. Antes de desplegar este backend, aplicar el mismo script a Azure SQL y después publicar el código.

La migración agrega cuatro columnas opcionales, dos FK de usuario, una restricción de coherencia y un índice único filtrado que impide dos estadías abiertas de la misma habitación. Es idempotente y conserva las reservas existentes sin inventar ingresos históricos.

## Permisos y API

Solo Administrador y Recepcionista (rol JWT `Recepcion`) con usuario y sede asignados.
No se acepta una sede enviada por el cliente: se utiliza la del JWT validado.

- `GET /api/recepcion?vista=llegadas|estadia|salidas&fecha=AAAA-MM-DD&codigo=CA-...`
- `POST /api/recepcion/{reservaHabitacionId}/check-in`
- `POST /api/recepcion/{reservaHabitacionId}/check-out`

La búsqueda por código exacto ignora fecha/pestaña, pero conserva el aislamiento por sede y solo incluye reservas confirmadas. Las listas se limitan a 100 habitaciones. Llegadas muestra ingresos pendientes dentro del periodo contratado; salidas incluye salidas vencidas. Las horas reales se guardan en UTC y la interfaz las presenta en America/Lima.

## Reglas operativas

Check-in: reserva confirmada, fecha de Perú dentro de [entrada, salida), habitación activa y Disponible, sin otra estadía abierta. Identidad y preparación se confirman por el personal en la interfaz.

Check-out: requiere ingreso previo, impide duplicados y registra operador/fecha. La reserva pasa a Completada solo cuando todas sus habitaciones tienen salida real. La habitación pasa a Bloqueada hasta su preparación; el personal autorizado la cambia a Disponible desde Habitaciones. No se ha agregado un módulo de limpieza.

Las operaciones se realizan dentro de una transacción Serializable y registran auditoría UPDATE. No se procesan pagos reales ni se agregan cargos: el personal revisa los pagos existentes antes de confirmar. No hay cancelación/corrección de movimientos ni extensión automática de fechas en este alcance.

El cliente recibe `estadoEstadia` (confirmada, en curso, finalizada o cancelada) en su listado. Reservas con una habitación todavía en estadía permanecen visibles aunque venza la fecha prevista. Las reservas existentes con estado Completada siguen mostrándose finalizadas.

## Validación

Ejecutar la suite .NET y la compilación/lint del frontend. Las pruebas en memoria verifican reglas y transiciones; no sustituyen pruebas de restricciones y concurrencia contra SQL Server. Aplicar la migración y probar manualmente antes de producción.
