# Automatización de estadías

- Hora de negocio: Perú (`America/Lima`); marcas de tiempo almacenadas en UTC.
- Check-in: desde las 10:00 del día de entrada. No existe un endpoint público de check-in manual.
- Checkout: el personal puede registrarlo; si falta, se cierra a las 00:00 del día siguiente a la salida prevista. Ejemplo: salida 10/11, cierre automático 11/11 a las 00:00 de Perú (05:00 UTC).
- `EstadiasWorker` ejecuta un ciclo al arrancar y cada 30 segundos. Procesa primero salidas y después ingresos, con paginación de 200 candidatos y un contexto/transacción serializable por detalle. La ejecución efectiva depende de que el backend esté activo y de la conexión a la base. No es un reloj externo independiente del servidor.
- Solo opera reservas `Reservado`. No altera canceladas, completadas ni pendientes. Un ingreso exige habitación activa, disponible y sin otra estadía abierta. Los ingresos impedidos se reevalúan en los siguientes ciclos.
- El checkout automático libera la habitación si estaba ocupada y no hay otra estadía abierta; respeta mantenimiento, bloqueos e inactividad. El checkout manual conserva el procedimiento existente: queda bloqueada hasta que el personal la prepare y la marque disponible.
- Si hay una nueva reserva el día de salida de la anterior, se necesita checkout manual y preparación para permitir el nuevo ingreso a las 10:00. El cierre automático a medianoche no anticipa esa salida.
- Una reserva con varias habitaciones pasa a `Completada` solo al cerrarse todas. Las fechas de entrada/salida y sus tokens de concurrencia, junto con el índice único existente de estadía activa, protegen las transiciones; los errores se registran y reintentan en otro ciclo.
- La auditoría se guarda en la misma transacción. El actor es nulo y el origen `AUTOMATICO`, visible al administrador como “Sistema automático”. No acredita presencia física ni intervención del personal.
- Al arrancar tras una interrupción se procesan pendientes. Si una estadía completa venció sin ingreso registrado, se anotan los horarios programados como recuperación (`Recuperado=true` en el ingreso). Un ingreso tardío todavía vigente conserva la hora de ejecución para no solaparlo con una salida anterior.
- No se cambia el esquema ni se necesita otra migración. Es necesario reiniciar/desplegar el backend actualizado. Las pruebas automáticas en memoria cubren horarios, exclusión, recuperación, idempotencia y auditoría; queda pendiente validar concurrencia real y restricciones contra una base SQL Server de pruebas.
