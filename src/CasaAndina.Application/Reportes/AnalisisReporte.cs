using CasaAndina.Application.Dashboard.DTOs;
namespace CasaAndina.Application.Reportes;

public sealed record AnalisisReporte(string Resumen, string Hallazgos, string Recomendaciones, string Limitaciones, DateTime GeneradoUtc, DashboardBiDto Datos);
public interface IAnalisisReporteService
{
    Task<AnalisisReporte?> ObtenerAsync(int sedeId, string periodo, DashboardBiDto datos, CancellationToken cancellationToken);
    AnalisisReporte? Consultar(int sedeId, string periodo);
}
public interface IAnalisisReportePdfService
{
    byte[] Generar(AnalisisReporte analisis);
}
