using CasaAndina.Application.Common;
using CasaAndina.Application.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CasaAndina.Infrastructure.Services;

public sealed class AnalisisReportePdfService : IAnalisisReportePdfService
{
    private const string Green = "#173F35", Muted = "#667570", Soft = "#EDF5F0", Gold = "#B48447";
    public byte[] Generar(AnalisisReporte analysis)
    {
        var d = analysis.Datos;
        var periodo = d.Periodo ?? throw new ArgumentException("El análisis requiere un periodo.");
        var logo = PdfBranding.Logo;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(38);
            page.DefaultTextStyle(t => t.FontFamily("Hanken Grotesk").FontSize(10).FontColor(Green));
            page.Header().Column(header =>
            {
                header.Item().Row(row => { row.ConstantItem(100).Height(42).Image(logo).FitArea(); row.RelativeItem().AlignRight().Text("ANÁLISIS DE LA SEDE").FontSize(17).SemiBold(); });
                header.Item().PaddingTop(8).Text($"{d.SedeNombre} · {periodo.Inicio:dd/MM/yyyy} al {periodo.Fin:dd/MM/yyyy}").FontSize(10);
                header.Item().PaddingTop(6).LineHorizontal(1).LineColor(Green);
            });
            page.Content().PaddingVertical(15).Column(body =>
            {
                body.Spacing(13);
                body.Item().Text("Actividad y comparación").FontSize(16).SemiBold();
                body.Item().Text($"Reservas: {d.Kpis.TotalReservas} · periodo anterior: {d.Kpis.TotalReservasPeriodoAnterior}     |     Importe reservado: S/ {d.Kpis.ImporteReservadoSoles:N2}").FontSize(10);
                body.Item().Text("El importe reservado no equivale a cobros. Ocupación corresponde al día del corte, no al promedio del periodo.").FontSize(8).FontColor(Muted);
                body.Item().Text("Reservas por semana · últimas seis semanas").FontSize(12).SemiBold();
                var max = Math.Max(1, d.ReservasSemana.SelectMany(x => new[] { x.Confirmadas, x.Canceladas }).DefaultIfEmpty().Max());
                foreach (var week in d.ReservasSemana)
                    body.Item().Row(row =>
                    {
                        row.ConstantItem(48).Text(week.SemanaLabel).FontSize(9);
                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(3);
                            ChartBar(col.Item(), week.Confirmadas, max, Green);
                            ChartBar(col.Item(), week.Canceladas, max, Gold);
                        });
                        row.ConstantItem(60).AlignRight().Text($"{week.Confirmadas} / {week.Canceladas}").FontSize(9);
                    });
                body.Item().Text("Verde: reservadas · Dorado: canceladas · por fecha de creación y estado actual").FontSize(8).FontColor(Muted);
                body.Item().Background(Soft).Padding(10).Text($"Canales del periodo: {d.Canales.ReservasDirectas} directas, {d.Canales.ReservasOTA} OTA y {Math.Max(0, d.Canales.TotalReservas-d.Canales.ReservasDirectas-d.Canales.ReservasOTA)} otros. Ocupación actual: {d.Kpis.OcupacionActualPct:N1}%.").FontSize(10);
                Section(body, "Resumen ejecutivo", analysis.Resumen);
                Section(body, "Hallazgos", analysis.Hallazgos);
                Section(body, "Acciones sugeridas", analysis.Recomendaciones);
                Section(body, "Limitaciones", analysis.Limitaciones);
                body.Item().Text("Análisis generado por IA. Revisar las recomendaciones antes de tomar decisiones.").FontSize(8).FontColor(Muted);
            });
            page.Footer().BorderTop(1).BorderColor("#DAE4DE").PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text($"Análisis generado: {PeruBusinessTime.FromUtc(analysis.GeneradoUtc):dd/MM/yyyy HH:mm} · Perú (UTC-5)").FontSize(8).FontColor(Muted);
                row.AutoItem().Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
            });
        })).GeneratePdf();
    }
    private static void Section(ColumnDescriptor body, string title, string value)
    {
        body.Item().EnsureSpace(40).Column(col => { col.Spacing(5); col.Item().Text(title).FontSize(12).SemiBold(); col.Item().Text(value).FontSize(10).LineHeight(1.35f); });
    }
    private static void ChartBar(IContainer container, int value, int max, string color) => container.Height(7).Row(row =>
    {
        if (value > 0) row.RelativeItem(value).Background(color);
        if (max > value) row.RelativeItem(max - value).Background(Soft);
    });
}
