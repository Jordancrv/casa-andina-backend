using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Dashboard.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace CasaAndina.Infrastructure.Services;

public sealed class DashboardReportPdfService : IDashboardReportPdfService
{
    private const string Green = "#173F35", Muted = "#667570", Soft = "#EDF5F0", Border = "#DAE4DE";
    private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");
    private static string Number(decimal value) => value.ToString("N0", Peru);
    private static string Money(decimal value) => $"S/ {value.ToString("N2", Peru)}";
    private static string Date(DateTime value) => value.ToString("dd/MM/yyyy", Peru);
    private static string Range(DateTime start, DateTime end) => $"{Date(start)} al {Date(end)}";

    public byte[] Generar(DashboardBiDto d)
    {
        var p = d.Periodo ?? throw new ArgumentException("El reporte requiere las fechas de la consulta.", nameof(d));
        var logo = PdfBranding.Logo;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                Setup(page, d, p, logo, "RESUMEN OPERATIVO");
                page.Content().PaddingVertical(16).Column(body =>
                {
                    body.Spacing(12);
                    body.Item().Text("Indicadores del periodo").FontSize(17).SemiBold();
                    body.Item().Row(row =>
                    {
                        row.Spacing(12);
                        Metric(row.RelativeItem(), "RESERVAS", Number(d.Kpis.TotalReservas), $"Anterior: {Number(d.Kpis.TotalReservasPeriodoAnterior)}");
                        Metric(row.RelativeItem(), "IMPORTE RESERVADO", Money(d.Kpis.ImporteReservadoSoles), $"Anterior: {Money(d.Kpis.ImporteReservadoPeriodoAnterior)}");
                    });
                    body.Item().Row(row =>
                    {
                        row.Spacing(12);
                        Metric(row.RelativeItem(), "OCUPACIÓN ACTUAL", $"{d.Kpis.OcupacionActualPct:N1}%", $"Al {Date(p.Fin)} · independiente del periodo");
                        Metric(row.RelativeItem(), "CLIENTES CON RESERVAS", Number(d.Kpis.TotalClientesActivos), $"Anterior: {Number(d.Kpis.ClientesPeriodoAnterior)}");
                    });
                    body.Item().Text($"Comparación: {Range(p.InicioAnterior, p.FinAnterior)}. Importe reservado sin canceladas; no equivale a pagos aprobados.").FontSize(9).FontColor(Muted);
                    body.Item().PaddingTop(5).Text("Reservas por semana").FontSize(17).SemiBold();
                    body.Item().Text($"Últimas 6 semanas · {Range(p.InicioSemanas, p.Fin)} · por fecha de creación y estado actual").FontSize(9).FontColor(Muted);
                    body.Item().Row(row =>
                    {
                        row.Spacing(18);
                        row.AutoItem().Text("VERDE  Reservadas").FontSize(8).FontColor(Green);
                        row.AutoItem().Text("GRIS  Canceladas").FontSize(8).FontColor(Muted);
                    });
                    body.Item().Column(chart =>
                    {
                        chart.Spacing(10);
                        var maximum = Math.Max(1, d.ReservasSemana.SelectMany(s => new[] { s.Confirmadas, s.Canceladas }).DefaultIfEmpty().Max());
                        foreach (var week in d.ReservasSemana)
                            chart.Item().Row(row =>
                            {
                                row.ConstantItem(43).AlignMiddle().Text(week.SemanaLabel).FontSize(9);
                                row.RelativeItem().Column(bars =>
                                {
                                    bars.Spacing(3);
                                    Bar(bars.Item(), week.Confirmadas, maximum, Green);
                                    Bar(bars.Item(), week.Canceladas, maximum, "#A5B7AF");
                                });
                                row.ConstantItem(70).AlignRight().AlignMiddle().Text($"{Number(week.Confirmadas)} / {Number(week.Canceladas)}").FontSize(9);
                            });
                    });
                    body.Item().Background(Soft).Padding(11).Text($"{Number(d.CanceladasPeriodo)} reservas canceladas creadas en el periodo seleccionado. La serie semanal usa su propio rango de seis semanas.").FontSize(10);
                });
            });
            document.Page(page =>
            {
                Setup(page, d, p, logo, "CANALES Y OCUPACIÓN");
                page.Content().PaddingVertical(16).Column(body =>
                {
                    body.Spacing(12);
                    body.Item().Text("Ocupación de la sede").FontSize(17).SemiBold();
                    foreach (var site in d.OcupacionSedes)
                        body.Item().Background(Soft).Padding(15).Column(col =>
                        {
                            col.Spacing(6);
                            col.Item().Text($"{site.SedeNombre} · {site.Ciudad}").SemiBold();
                            col.Item().Text($"{site.OcupacionPct:N1}% · {Number(site.Cantidad)} habitaciones ocupadas").FontSize(15);
                            col.Item().Text($"Corte al {Date(p.Fin)}; sobre habitaciones activas de la sede asignada.").FontSize(9).FontColor(Muted);
                        });
                    body.Item().Text("Canales de reserva").FontSize(17).SemiBold();
                    body.Item().Text("Reservas no canceladas creadas en el periodo seleccionado.").FontSize(9).FontColor(Muted);
                    body.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(); cols.RelativeColumn(); });
                        table.Header(header => { Cell(header.Cell(), "CANAL", true); Cell(header.Cell(), "RESERVAS", true); Cell(header.Cell(), "PARTICIPACIÓN", true); });
                        var channels = new[] { ("Directo", d.Canales.ReservasDirectas), ("OTA", d.Canales.ReservasOTA), ("Otros", Math.Max(0, d.Canales.TotalReservas - d.Canales.ReservasDirectas - d.Canales.ReservasOTA)) };
                        foreach (var (name, count) in channels)
                        {
                            Cell(table.Cell(), name); Cell(table.Cell(), Number(count));
                            Cell(table.Cell(), d.Canales.TotalReservas == 0 ? "0.0%" : $"{(decimal)count * 100 / d.Canales.TotalReservas:N1}%");
                        }
                        Cell(table.Cell(), "TOTAL", true); Cell(table.Cell(), Number(d.Canales.TotalReservas), true);
                        Cell(table.Cell(), d.Canales.TotalReservas == 0 ? "0.0%" : "100.0%", true);
                    });
                    body.Item().Row(row =>
                    {
                        row.Spacing(12);
                        Metric(row.RelativeItem(), "TICKET PROMEDIO", Money(d.Canales.TicketPromedioSoles), "Por reserva no cancelada");
                        Metric(row.RelativeItem(), "ESTADÍA PROMEDIO", $"{d.Canales.EstadiaPromedioNoches:N1} noches", "Por habitación reservada");
                    });
                    body.Item().Column(notes =>
                    {
                        notes.Spacing(10);
                        Note(notes.Item(), "Cómo leer los datos", "Fuente: reservas de la sede asignada, por fecha de creación y estado al consultar. Reservas incluye todos los estados; clientes cuenta personas distintas con reservas no canceladas. Ocupación: habitaciones activas ocupadas o con reserva vigente hoy, sin incluir el día de check-out.");
                        Note(notes.Item(), "Disponibilidad de datos", "Cero indica ausencia de actividad registrada. La proyección de ocupación se consulta en el dashboard y no está incluida en este PDF. Sin métricas de uso del asistente IA registradas.");
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Setup(PageDescriptor page, DashboardBiDto d, DashboardPeriodoDto p, byte[] logo, string section)
    {
        page.Size(PageSizes.A4); page.Margin(38);
        page.DefaultTextStyle(t => t.FontFamily("Hanken Grotesk").FontSize(10).FontColor(Green));
        page.Header().Column(header =>
        {
            header.Spacing(8);
            header.Item().Row(row =>
            {
                row.ConstantItem(105).Height(45).Image(logo).FitArea();
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text("REPORTE DE DASHBOARD").FontSize(18).SemiBold();
                    col.Item().PaddingTop(5).AlignRight().Text(section).FontSize(8).FontColor(Muted);
                });
            });
            header.Item().Text(d.SedeNombre).FontSize(13).SemiBold();
            header.Item().Text($"Periodo: {Range(p.Inicio, p.Fin)}  ·  {(p.Codigo == "7d" ? "Últimos 7 días" : p.Codigo == "mes" ? "Este mes" : "Últimos 30 días")}").FontSize(9).FontColor(Muted);
            header.Item().LineHorizontal(1).LineColor(Green);
        });
        page.Footer().BorderTop(1).BorderColor(Border).PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Text($"Generado: {PeruBusinessTime.FromUtc(p.GeneradoUtc):dd/MM/yyyy HH:mm} · Perú (UTC−5)\nUso interno · Casa Andina").FontSize(8).FontColor(Muted);
            row.AutoItem().AlignBottom().Text(text => { text.Span("Página "); text.CurrentPageNumber(); text.Span(" de "); text.TotalPages(); });
        });
    }
    private static void Metric(IContainer container, string title, string value, string caption) => container
        .Border(1).BorderColor(Border).Background(Soft).Padding(11).Column(col =>
        {
            col.Spacing(7); col.Item().Text(title).FontSize(8).FontColor(Muted);
            col.Item().Text(value).FontSize(20).SemiBold();
            col.Item().Text(caption).FontSize(8).FontColor(Muted);
        });
    private static void Bar(IContainer container, int value, int max, string color) => container.Height(8).Row(row =>
    {
        if (value > 0) row.RelativeItem(value).Background(color);
        if (max > value) row.RelativeItem(max - value).Background(Soft);
    });
    private static void Cell(IContainer container, string text, bool emphasis = false) => container
        .BorderBottom(1).BorderColor(Border).Background(emphasis ? Soft : Colors.White).Padding(8).Text(text).FontSize(9);
    private static void Note(IContainer container, string title, string text) => container.Column(col =>
    {
        col.Spacing(3); col.Item().Text(title).SemiBold().FontSize(10); col.Item().Text(text).FontSize(9).FontColor(Muted);
    });
}
