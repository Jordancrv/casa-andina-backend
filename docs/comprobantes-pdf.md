# Boletas y facturas PDF de reservas

## Emisión y acceso

Al confirmar una compra, el flujo existente registra el pago simulado aprobado,
la reserva y el comprobante. Ahora guarda también `Comprobante.DatosEmisionJson`
con los datos de emisión dentro de la misma transacción. La reserva no se confirma
si sus importes y los detalles del comprobante no coinciden.

El PDF utiliza esos datos guardados. Cambiar después el nombre o documento del
cliente en el CRM, las habitaciones o los precios del catálogo no altera su contenido.
La representación PDF se genera al solicitarla, fuera de la transacción de compra;
un fallo del renderizador permite reintentar sin volver a pagar ni crear otro comprobante.

```text
POST /api/reservas/{id}/comprobante/pdf
Authorization: Bearer <JWT del cliente>
```

Devuelve `application/pdf` con nombre de archivo y `Cache-Control: no-store, private`.
La política de acceso es `Cliente` y la reserva se busca por ID y cliente del JWT.
Una reserva ajena o inexistente devuelve `404`; sin identidad de cliente, `401`.

Solo habilita el PDF si la suma aplicada de pagos `APROBADO`, en PEN, cubre el total
positivo del comprobante, y este coincide con el total de la reserva. Los pagos pendientes,
rechazados, parciales o de otra moneda no habilitan el documento. No se aceptan importes
ni datos de cliente enviados por el navegador al generar el PDF.

Las respuestas de compra y de «Mis reservas» incluyen `comprobanteDisponible`,
`tipoComprobante` y `numeroComprobante`. La compra incluye ya el tipo anterior,
manteniendo compatibilidad con su contrato existente.

## Contenido y diseño

Boleta y factura comparten el logo, tipografía Hanken Grotesk, colores verdes y tabla
de detalles del proyecto. La factura muestra la razón social y RUC del perfil jurídico
validado por el checkout. Incluyen sede, emisión en horario de Perú, reserva, cliente,
fechas de estadía, habitaciones por noche, servicios por cantidad, total y pagos registrados.

Los importes corresponden exactamente a los valores del modelo. No se agregó un cálculo
de impuestos ni se inventan identificadores tributarios del emisor. Los números de los
nuevos comprobantes comienzan con B o F según su tipo y conservan la referencia única de
la reserva; no constituyen una serie autorizada por un servicio tributario.

El sistema actual usa pagos simulados: todos los PDF lo indican en la cabecera y en cada
pie de página. Esta función es un comprobante del proyecto académico; no integra emisión
electrónica ante SUNAT ni una pasarela de cobro real.

## Reservas anteriores

La migración mantiene `DatosEmisionJson` en `NULL` para comprobantes previos.
La primera solicitud de un PDF pagado recupera sus detalles actuales y los guarda una
única vez, bajo transacción serializable. El documento indica expresamente que se
reconstruyeron los datos disponibles al generar el detalle. Las solicitudes posteriores
utilizan la copia guardada. No se crea otro número ni otro registro de comprobante.

## Interfaz

- Confirmación de compra: «Previsualizar boleta/factura» y «Descargar PDF».
- «Mis reservas» → «Ver detalle»: las mismas acciones si el comprobante está disponible.
- La vista previa usa PDF.js, cargado solo al abrir el visor, con paginación y ampliación.
- La descarga usa el mismo PDF recuperado para la vista previa; el JWT viaja en el header
  HTTP y no se incorpora a enlaces públicos. Se liberan los objetos Blob y el worker del
  visor al desmontar sus componentes.

No depende del visor PDF nativo ni de un servicio externo para renderizar documentos.
Las imágenes y tipografías se incluyen en el backend y el worker PDF.js se sirve con el frontend.

## Migración y ejecución

Aplicar `scripts/migrate-comprobante-pdf.sql` antes de ejecutar la API actualizada sobre
una base existente. Agrega `DatosEmisionJson NVARCHAR(MAX) NULL`; es transaccional e
idempotente y conserva los comprobantes previos. Reiniciar la API después de actualizarla.

QuestPDF 2026.9.1 se incluye como dependencia del backend. Se configura Community para
este proyecto académico/individual. La licencia de Hanken Grotesk está junto a la fuente
en `src/CasaAndina.Infrastructure/Assets/OFL-HankenGrotesk.txt`.
PDF.js 6.4.299 está declarado en `package.json` y `package-lock.json`.

## Verificación

Pruebas de emisión al reservar, identidad y RUC, congelación de datos, pagos parciales y
rechazados, suma de pagos aprobados, moneda, acceso a reservas ajenas, datos inválidos e
idempotencia. Las pruebas SQL usan bases temporales `CasaAndinaTestPdf_<guid>` y verifican
compras de boleta y factura, consulta del indicador de disponibilidad, PDF y migración repetida.

Los ejemplos visuales usan datos ficticios. Se comprobaron ambos PDF, texto y montos,
el documento de varias páginas, la vista previa en escritorio/móvil, ampliación y descarga.

Fuentes técnicas:

- [QuestPDF](https://www.questpdf.com/quick-start.html)
- [Licencia de QuestPDF](https://github.com/QuestPDF/QuestPDF/blob/main/LICENSE.md)
- [Hanken Grotesk y licencia OFL](https://github.com/google/fonts/tree/main/ofl/hankengrotesk)
- [PDF.js](https://mozilla.github.io/pdf.js/examples/)
