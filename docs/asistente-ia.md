# Asistente de viajes: Gemini + catálogo + clima

## Estado de esta entrega

Implementación local de `POST /api/chatbot/planificar` y pantalla `/cliente/asistente`.
Requiere JWT con política `Cliente`. No modifica reservas ni requiere cambios de esquema.

### Experiencia de chat

- Conversación en pantalla con mensajes del huésped y del asistente, preguntas sugeridas, cancelación, reintento y nuevo chat. Las respuestas anteriores conservan sus fechas, precios y fotos como referencia; se identifican como consultas anteriores.
- Cada petición puede incluir `historial`: hasta 8 mensajes `{ "rol": "usuario" | "asistente", "texto": "..." }`, con un máximo de 1000 caracteres por mensaje. El frontend envía los últimos cuatro intercambios completos. El historial no sustituye las restricciones vigentes ni los datos actuales del catálogo.
- La conversación se mantiene en memoria del frontend durante la visita; no se persiste como historial de clientes en la base de datos. El contenido enviado a Gemini queda sujeto a las condiciones del proveedor.
- `habitacionId` y `fotoUrl` proceden de la habitación disponible seleccionada, no de Gemini. Si no hay foto o no se carga, la tarjeta muestra un aviso sin usar fotos de otras habitaciones.
- `clima.dias[].codigoTiempo` contiene el código WMO de `daily.weather_code`. La interfaz muestra iconos y cambia el color al elegir un día. Sin código, muestra condición sin datos; sin pronóstico, no presenta temperaturas inventadas.
- La temperatura grande es la máxima prevista del día seleccionado, no una observación actual. Se muestran mínimas, lluvia, cobertura parcial, fecha de consulta y fuente.
- La estación es una referencia por meses del hemisferio sur (dic–feb, mar–may, jun–ago, sep–nov), identificada como tal; no afirma describir las condiciones regionales de Perú ni reemplaza el pronóstico.
- En móvil, al comenzar la conversación se prioriza el chat y se compactan los datos del viaje. No cambia la navegación de las otras pantallas.

- Selecciona habitaciones activas de sedes activas, con capacidad para los adultos indicados y dentro del presupuesto.
- Excluye mantenimiento/bloqueo y reservas no canceladas que se solapen con la estadía. Permite reservas contiguas.
- Compara una habitación disponible por sede, hasta 12 sedes; Gemini selecciona hasta tres y explica sus diferencias.
- Los importes, las fechas y la disponibilidad provienen de la BD. Gemini solo devuelve IDs existentes, motivos y resumen.
- Consulta geocodificación de ciudades de Perú y pronóstico diario de Open-Meteo. Incluye el día de salida.
- Muestra fecha de consulta, fuente, lluvia, temperaturas y cobertura parcial o ausente. No extrapola fechas fuera del horizonte de 16 días.
- La búsqueda de habitaciones recibe sede y fechas. La reserva se completa y verifica en el flujo existente.
- Si Gemini falla, falta configuración o devuelve IDs inválidos, muestra resultados del catálogo con `usaIa=false`, sin simular IA.
- Si falla el clima, conserva las opciones de alojamiento e informa que el pronóstico no está disponible.

La explicación es texto generado: validar IDs evita hoteles inventados en las tarjetas,
pero no demuestra por sí solo que todas las afirmaciones del texto sean correctas.
Debe revisarse con respuestas reales antes de la presentación.

## Configurar Gemini en desarrollo

Se necesita una **clave de Gemini API de Google AI Studio**; una cuenta en la aplicación
de Gemini no sustituye la clave de API. No guardar la clave en Git, en variables `VITE_*`,
en el frontend ni en capturas de la presentación.

Desde la carpeta `casa-andina-backend`, en tu terminal local:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "<TU_CLAVE>" --project src/CasaAndina.Api
dotnet user-secrets set "Gemini:Model" "gemini-3.5-flash-lite" --project src/CasaAndina.Api
dotnet run --project src/CasaAndina.Api
```

Reemplaza el marcador solo en tu equipo. El proyecto ya tiene `UserSecretsId`.
El modelo puede cambiarse por otro habilitado en tu cuenta que soporte `generateContent`
y salida JSON estructurada. En despliegue se usan secretos del servidor con los nombres
`Gemini__ApiKey` y `Gemini__Model`; reiniciar la API después de configurarlos.

El proveedor predeterminado usa `gemini-3.5-flash-lite`. Confirmar el modelo y la cuota
en la cuenta que se utilizará para la demostración. La configuración se obtiene mediante
`IOptions<GeminiOptions>` y la clave viaja en el header `x-goog-api-key`.

## Contrato

```json
{
  "mensaje": "Prefiero un destino con menos lluvia. Explícame las opciones.",
  "fechaEntrada": "AAAA-MM-DD",
  "fechaSalida": "AAAA-MM-DD",
  "presupuestoPorNoche": 500,
  "adultos": 2,
  "ciudad": null,
  "historial": []
}
```

Usar fechas reales desde hoy: entrada hasta 365 días adelante, de 1 a 30 noches,
de 1 a 8 adultos en una habitación y presupuesto máximo por noche entre S/ 1 y S/ 10 000.
El mensaje admite hasta 1000 caracteres. `ciudad` debe corresponder al catálogo o ser `null`.
La ciudad, el presupuesto, los adultos y las fechas del formulario son restricciones obligatorias;
el mensaje sirve para ordenar y explicar entre las opciones que las cumplen.
Esta primera versión no incluye grupos con niños ni varias habitaciones.

La respuesta contiene `resumen`, `usaIa`, `generadoEn`, `recomendaciones` y `avisos`.
Cada recomendación incluye precio por noche, total sin extras, noches, tipo de habitación,
motivo y clima. Sin habitaciones compatibles, devuelve una lista vacía sin llamar a Gemini.

Se mantiene el GET anterior para compatibilidad y se corrige su DTO para coincidir con
el frontend. Ese GET usa bandas orientativas (económico hasta S/ 250, estándar hasta
S/ 500, lujo hasta S/ 10 000) y preferencia regional orientativa; no verifica fechas ni
constituye un pronóstico meteorológico. La nueva pantalla usa exclusivamente el POST.

## Resiliencia y rendimiento

- Hasta 6 consultas por IP por minuto. Compartir una red puede compartir este límite.
- Clima: ciudades consultadas en paralelo, caché de 30 minutos; geocodificación de 7 días.
- Geocodificación + pronóstico: espera máxima de 5 segundos por ciudad.
- Gemini: espera máxima de 20 segundos. Frontend: 35 segundos, con cancelación.
- No se reintentan automáticamente peticiones a Gemini que podrían consumir cuota otra vez.
- No se registran claves ni mensajes de huéspedes en los logs de estos servicios.

**RNF04 (<2.5 segundos) aún no está acreditado.** Estos límites evitan esperas indefinidas;
no significan que la consulta dure 2.5 segundos. Medir p50/p95 con clave real, BD y red de
la presentación; ajustar modelo, caché o flujo de respuesta si es necesario.

## Verificación de esta entrega

- Compilación backend y frontend correctas; ESLint sin errores.
- Suite backend del 27 de octubre: 226 pruebas correctas y 7 pruebas de SQL Server omitidas por su configuración opcional. Compilación en salida aislada para no detener la API activa.
- Frontend: `npm run test:assistant` comprueba códigos meteorológicos ausentes, lluvia/nieve/niebla/tormentas, cambio de estación por mes y URLs de fotos. No requiere nuevas dependencias.
- 23 casos nuevos sobre disponibilidad, capacidad, presupuesto, fechas, validación de Gemini,
  cuota, cancelación, pronóstico parcial, datos nulos y caché; 7 casos anteriores de enums también comprobados.
- Verificación HTTP de los endpoints públicos de geocodificación y pronóstico de Lima.
- Revisión visual con datos simulados en una página temporal separada de la aplicación.

Pendiente: una petición real a Gemini (no se encontró clave configurada), recorrido autenticado
contra SQL Server y evaluación de calidad/latencia. Los tests de proveedor usan respuestas HTTP
simuladas para no consumir cuota. No presentar esa simulación como evidencia de IA real.

## Guion de demostración

1. Entrar con un cliente de prueba y abrir Asistente.
2. Elegir fechas dentro de los próximos 16 días y un presupuesto que tenga inventario disponible.
3. Usar «Evitar la lluvia» y consultar. Mostrar el indicador «Explicación con Gemini», motivos,
   pronóstico por día, fuente, precios y costo total.
4. Cambiar presupuesto o destino y volver a consultar. Explicar qué filtros aplica el backend.
5. Pulsar «Ver habitaciones»: comprobar que conserva sede y fechas y que valida disponibilidad nuevamente.
6. Mostrar fechas lejanas: la interfaz debe indicar que aún no hay pronóstico.
7. Explicar que la reserva necesita confirmación del huésped y que noticias/ML son etapas posteriores.

## Fuentes técnicas

- [Modelos de Gemini](https://ai.google.dev/gemini-api/docs/models)
- [Salida estructurada de generateContent](https://ai.google.dev/gemini-api/docs/generate-content/structured-output)
- [Pronóstico de Open-Meteo](https://open-meteo.com/en/docs)
- [Geocodificación de Open-Meteo](https://open-meteo.com/en/docs/geocoding-api)

La integración meteorológica usa los endpoints públicos. Antes de un despliegue comercial,
verificar las condiciones y el plan de Open-Meteo aplicables al uso previsto.
