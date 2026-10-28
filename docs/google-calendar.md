# Calendario del cliente y Google Calendar

El portal muestra únicamente las reservas del cliente autenticado. Conectar Google es
opcional y no cambia su forma de iniciar sesión en Casa Andina.

## Configuración local

1. Ejecutar `scripts/migrate-google-calendar.sql` en la base del proyecto. Crea dos
   tablas nuevas sin modificar las reservas existentes. Se puede repetir.
2. En Google Cloud, habilitar **Google Calendar API** y crear un cliente OAuth de
   tipo **Aplicación web** en el mismo proyecto de la pantalla de consentimiento.
3. Registrar exactamente esta **URI de redirección autorizada**:

   `http://localhost:51141/api/calendario/google/callback`

4. En Google Auth Platform → Público / Audience, agregar las cuentas que participarán
   en la demostración como usuarios de prueba si la aplicación sigue en Testing.
5. Desde la carpeta del backend ejecutar:

   ```powershell
   ./scripts/configure-google-calendar.ps1
   ```

   El script pide Client ID y Client Secret (oculto) y los guarda mediante .NET
   User Secrets, fuera del repositorio, sin pasarlos como argumentos de proceso.
   Conserva las demás claves existentes. No se necesita una API key de Calendar
   ni la clave de Gemini. El backend no carga archivos `.env`.

   Si Vite está en otro puerto, especificarlo, por ejemplo:

   ```powershell
   ./scripts/configure-google-calendar.ps1 -FrontendUrl http://localhost:5174
   ```

6. Verificar que `Cors:AllowedOrigins` incluya el origen del frontend, reiniciar
   la API con `dotnet run --project src/CasaAndina.Api`, abrir el frontend en
   **http://localhost:5173** y entrar a **Mi calendario → Conectar Google Calendar**.
   Utilizar `localhost` tanto en el frontend como en la API, sin mezclarlo con
   `127.0.0.1`, para que llegue la cookie de correlación.

Los nombres equivalentes de variables de entorno son:

| Variable | Valor |
| --- | --- |
| `GoogleCalendar__ClientId` | Client ID OAuth |
| `GoogleCalendar__ClientSecret` | Client Secret OAuth, solo backend |
| `GoogleCalendar__RedirectUri` | URI de retorno registrada en Google |
| `GoogleCalendar__FrontendUrl` | Origen del frontend, sin ruta |

## Comportamiento

- Se solicita únicamente `https://www.googleapis.com/auth/calendar.app.created`.
  Se crea **Mis estadías · Casa Andina**, propiedad del cliente. No se consultan
  sus calendarios personales ni sus demás eventos.
- OAuth usa código de autorización con PKCE, estado aleatorio de un solo uso
  durante 10 minutos y una cookie HttpOnly de correlación. Reiniciar la API
  durante la autorización obliga a iniciar la conexión otra vez.
- El refresh token se cifra con ASP.NET Core Data Protection antes de guardarlo.
  El access token dura una operación y nunca llega al frontend. Conservar las
  claves de Data Protection entre reinicios. En un despliegue con contenedores
  usar almacenamiento persistente y protección de claves adecuada; la variable
  opcional `GoogleCalendar__DataProtectionKeysPath` permite indicar una carpeta
  privada del servidor (nunca dentro del repositorio).
- Se sincronizan reservas **Confirmada** y **Completada**, como eventos privados
  del calendario dedicado, de día completo, desde entrada hasta salida exclusiva.
  El día de salida no cuenta como una noche adicional. Incluyen sede, ciudad,
  dirección y código de reserva; no incluyen documentos, pagos o datos de huéspedes.
- Hay un recordatorio emergente 24 horas antes. Google y las notificaciones del
  dispositivo determinan si el aviso se muestra.
- Se ejecuta una primera sincronización al conectar y un ciclo cada 2 minutos
  mientras la API esté funcionando. También existe **Sincronizar ahora**.
  Los cambios de fechas se actualizan y las cancelaciones retiran sus eventos.
- La correspondencia reserva/evento se persiste antes de llamar a Google y una
  huella evita actualizaciones innecesarias. Los fallos parciales se reintentan.
- La sincronización va de Casa Andina a Google. Editar Google no cambia una reserva.
  Si el cliente elimina manualmente un evento sin cambiar la reserva, no se
  recrea en cada ciclo; al modificar la reserva se vuelve a publicar.
- Desconectar revoca el acceso y detiene la sincronización. Los eventos existentes
  permanecen en Google. La web mantiene el calendario local disponible.
- Si Google revoca o vence el permiso, la web ofrece **Volver a conectar**.
  En modo Testing, Google puede hacer caducar el refresh token a los 7 días.

La primera versión está prevista para una sola instancia de API. Para varias
réplicas, sustituir el estado OAuth en memoria y el semáforo por almacenamiento y
bloqueo distribuidos antes de habilitar el worker en todas ellas.

## Validación

Pruebas automatizadas con un servidor HTTP de Google simulado: aislamiento entre
clientes, OAuth de un solo uso y correlación, fechas y salida exclusiva,
sincronización repetida sin duplicados, cambio de fechas, cancelación, revocación
y cifrado del token. La validación real requiere configurar las credenciales y
autorizar una cuenta de prueba desde el navegador.

Documentación oficial:
- [OAuth para aplicaciones web](https://developers.google.com/identity/protocols/oauth2/web-server).
- [Permisos de Calendar](https://developers.google.com/workspace/calendar/api/auth).
- [Creación de calendarios](https://developers.google.com/workspace/calendar/api/v3/reference/calendars/insert).
