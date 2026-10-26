using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace CasaAndina.Infrastructure.Services;

public sealed class ComprobantePdfService : IComprobantePdfService
{
    private const string Verde = "#173F35";
    private const string Suave = "#EDF5F0";
    private const string Borde = "#DAE4DE";
    private const string Gris = "#667570";
    private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");
    public byte[] Generar(ComprobanteDocumentoDto d)
    {
        var logo = PdfBranding.Logo;
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontFamily("Hanken Grotesk").FontSize(10).FontColor(Verde));
            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Width(110).Height(48).Image(logo).FitArea();
                        col.Item().PaddingTop(9).Text(d.SedeNombre).FontSize(10).SemiBold();
                        col.Item().PaddingTop(3).Text(d.Direccion ?? d.Ciudad).FontSize(9).FontColor(Gris);
                        if (!string.IsNullOrWhiteSpace(d.TelefonoSede)) col.Item().PaddingTop(3).Text(d.TelefonoSede).FontSize(9).FontColor(Gris);
                    });
                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text(d.Tipo.ToUpperInvariant()).FontSize(27).SemiBold();
                        col.Item().PaddingTop(6).AlignRight().Text(d.Numero).FontSize(11);
                        col.Item().PaddingTop(10).AlignRight().Text($"EMISIÓN  {FechaUtc(d.FechaEmision)}").FontSize(8).FontColor(Gris);
                        col.Item().PaddingTop(7).AlignRight().Background(Suave).PaddingHorizontal(10).PaddingVertical(5)
                            .Text("PAGO SIMULADO APROBADO").FontSize(8).SemiBold();
                    });
                });
                header.Item().PaddingTop(17).LineHorizontal(1).LineColor(Verde);
            });
            page.Content().PaddingTop(20).PaddingBottom(18).Column(body =>
            {
                body.Spacing(18);
                body.Item().Background(Suave).Padding(15).Row(row =>
                {
                    row.RelativeItem(1.8f).PaddingRight(18).Column(col =>
                    {
                        col.Item().Text(d.Tipo == "Factura" ? "DATOS DE FACTURACIÓN" : "DATOS DEL CLIENTE").FontSize(8).FontColor(Gris).SemiBold();
                        col.Item().PaddingTop(7).Text(d.ClienteNombre).FontSize(14).SemiBold();
                        col.Item().PaddingTop(5).Text(d.DocumentoNumero is null ? "Documento no registrado" : $"{d.DocumentoTipo} {d.DocumentoNumero}").FontSize(10).FontColor(Gris);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("RESERVA").FontSize(8).FontColor(Gris).SemiBold();
                        col.Item().PaddingTop(7).Text(d.CodigoReserva).FontSize(14).SemiBold();
                        col.Item().PaddingTop(5).Text($"{d.Ciudad} - Moneda: {d.Moneda}").FontSize(10).FontColor(Gris);
                    });
                });
                body.Item().Row(row =>
                {
                    Dato(row.RelativeItem(), "ENTRADA", Fecha(d.FechaEntrada));
                    Dato(row.RelativeItem(), "SALIDA", Fecha(d.FechaSalida));
                    Dato(row.RelativeItem(), "ESTADÍA", $"{(d.FechaSalida - d.FechaEntrada).Days} noche(s)");
                });
                body.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.ConstantColumn(65); cols.ConstantColumn(88); cols.ConstantColumn(88); });
                    table.Header(h =>
                    {
                        h.Cell().Element(Cabecera).Text("DESCRIPCIÓN");
                        h.Cell().Element(Cabecera).AlignRight().Text("CANTIDAD");
                        h.Cell().Element(Cabecera).AlignRight().Text("P. UNITARIO");
                        h.Cell().Element(Cabecera).AlignRight().Text("IMPORTE");
                    });
                    foreach (var linea in d.Lineas)
                    {
                        table.Cell().Element(Celda).Text(linea.Descripcion).FontSize(10);
                        var unidad = linea.Unidad == "noche" ? (linea.Cantidad == 1 ? "noche" : "noches") : (linea.Cantidad == 1 ? "unidad" : "unidades");
                        table.Cell().Element(Celda).AlignRight().Text($"{linea.Cantidad} {unidad}").FontSize(9).FontColor(Gris);
                        table.Cell().Element(Celda).AlignRight().Text(Monto(linea.PrecioUnitario)).FontSize(9);
                        table.Cell().Element(Celda).AlignRight().Text(Monto(linea.Importe)).FontSize(9).SemiBold();
                    }
                });
                body.Item().Element(c => d.Pagos.Count <= 5 ? c.ShowEntire() : c).Column(cierre =>
                {
                cierre.Spacing(18);
                cierre.Item().AlignRight().Width(235).ShowEntire().Column(total =>
                {
                    total.Spacing(8);
                    Importe(total.Item(), "Alojamiento", d.Lineas.Where(l => l.Unidad == "noche").Sum(l => l.Importe));
                    Importe(total.Item(), "Servicios", d.Lineas.Where(l => l.Unidad != "noche").Sum(l => l.Importe));
                    total.Item().PaddingTop(4).Background(Verde).Padding(12).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text("TOTAL").FontColor(Colors.White).FontSize(10).SemiBold();
                        row.RelativeItem(2).AlignRight().Text(Monto(d.Total)).FontColor(Colors.White).FontSize(20).SemiBold();
                    });
                    Importe(total.Item(), "Pagado (simulado)", d.Pagos.Sum(p => p.MontoAplicado));
                    Importe(total.Item(), "Saldo pendiente", 0);
                });
                cierre.Item().Column(pagos =>
                {
                    pagos.Item().PaddingBottom(8).Text("PAGOS REGISTRADOS").FontSize(8).FontColor(Gris).SemiBold();
                    foreach (var pago in d.Pagos)
                        pagos.Item().PaddingBottom(6).Row(row =>
                        {
                            row.RelativeItem().Text($"{pago.Metodo} - {FechaUtc(pago.Fecha)}").FontSize(9).FontColor(Gris);
                            row.ConstantItem(100).AlignRight().Text(Monto(pago.MontoAplicado)).FontSize(9);
                        });
                    pagos.Item().PaddingTop(12).Text("Gracias por elegir Casa Andina.").FontSize(13).SemiBold();
                });
                });
                if (d.DetalleReconstruido)
                    body.Item().BorderLeft(2).BorderColor("#B18A3C").Background("#FAF5E9").Padding(12)
                        .Text("Reserva anterior a la emisión detallada de PDF. La identidad, sede y descripciones se recuperaron de los datos disponibles al generar este documento.")
                        .FontSize(9).FontColor(Gris);
            });
            page.Footer().BorderTop(0.5f).BorderColor(Borde).PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("PROYECTO ACADÉMICO - PAGO SIMULADO").FontSize(7).SemiBold().FontColor(Gris);
                    col.Item().PaddingTop(3).Text("Este documento no acredita un cobro real.").FontSize(7).FontColor(Gris);
                });
                row.ConstantItem(80).AlignRight().AlignBottom().Text(t =>
                { t.DefaultTextStyle(s => s.FontSize(8).FontColor(Gris)); t.Span("Página "); t.CurrentPageNumber(); t.Span(" de "); t.TotalPages(); });
            });
        })).GeneratePdf();
    }

    private static IContainer Cabecera(IContainer c) => c.Background(Suave).PaddingVertical(10).PaddingHorizontal(8).DefaultTextStyle(t => t.FontSize(8).SemiBold().FontColor(Gris));
    private static IContainer Celda(IContainer c) => c.ShowEntire().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(12).PaddingHorizontal(8);
    private static void Dato(IContainer c, string titulo, string valor) => c.Column(col =>
    { col.Item().Text(titulo).FontSize(8).FontColor(Gris).SemiBold(); col.Item().PaddingTop(6).Text(valor).FontSize(11); });
    private static void Importe(IContainer c, string titulo, decimal valor) => c.Row(row =>
    { row.RelativeItem().Text(titulo).FontSize(9).FontColor(Gris); row.ConstantItem(105).AlignRight().Text(Monto(valor)).FontSize(10); });
    private static string Monto(decimal valor) => $"S/ {valor.ToString("N2", Peru)}";
    private static string Fecha(DateTime valor) => valor.ToString("dd MMM yyyy", Peru);
    private static string FechaUtc(DateTime valor) => PeruBusinessTime.FromUtc(valor).ToString("dd MMM yyyy, HH:mm", Peru);
}
