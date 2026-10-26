# Reporte del dashboard

El botón **Generar reporte** del dashboard abre una vista previa con paginación, zoom y descarga PDF. Usa el periodo seleccionado: últimos 7 días, últimos 30 días o mes actual. El servidor vuelve a consultar los datos al generar el archivo.

## API

`GET /api/dashboard/reporte/pdf?periodo=30d`

- Autenticación JWT y política `Administrador`, igual que el dashboard existente.
- Alcance exclusivo de la sede asignada en la sesión. No acepta cifras ni identificadores de sede proporcionados por el navegador.
- Periodos válidos: `7d`, `30d`, `mes`. Un periodo inválido devuelve validación y un administrador sin sede no puede generar el reporte.
- Devuelve `application/pdf`, nombre de descarga y `Cache-Control: no-store, private`.
- No requiere migraciones ni nuevas dependencias. Genera el documento con QuestPDF y los recursos locales compartidos con los comprobantes.

## Contenido y lectura

El PDF incluye cuatro KPI, cifras del periodo anterior, reservas por semana, cancelaciones, ocupación, canales, ticket y estadía promedio. Todas las fechas civiles y el corte usan Perú (UTC−5). La metadata del periodo se calcula junto con los indicadores para compartir el mismo corte.

Los conteos del periodo usan fecha de creación y estado actual. El importe reservado excluye cancelaciones y no representa pagos cobrados. Los canales también excluyen cancelaciones; canales diferentes a Directo y OTA aparecen como Otros. La estadía promedio se calcula por habitación reservada. La serie semanal cubre seis semanas, independientemente del filtro; la ocupación corresponde a hoy. Clientes cuenta identificadores distintos con reservas no canceladas.

Predicciones y uso del asistente IA se indican como no disponibles mientras no existan fuentes conectadas. El reporte es un documento operativo interno, no un comprobante tributario.

## Verificación

`DashboardReportTests` comprueba periodos, aislamiento de sede, validación, cancelación y generación con actividad y sin datos. El resto de las pruebas del dashboard comprueba sus cálculos.

Para generar ejemplos sintéticos durante las pruebas, definir `CASAANDINA_DASHBOARD_PDF_QA_DIR` a un directorio de salida y ejecutar `dotnet test --filter FullyQualifiedName~DashboardReportTests.Pdf`. Sin esta variable las pruebas no guardan archivos.
