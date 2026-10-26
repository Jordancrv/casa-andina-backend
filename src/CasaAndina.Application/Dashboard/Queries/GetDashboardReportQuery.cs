using CasaAndina.Application.Common.Interfaces;
using MediatR;

namespace CasaAndina.Application.Dashboard.Queries;

public sealed record DashboardReportFile(byte[] Contenido, string Nombre);
public sealed record GetDashboardReportQuery(string Periodo = "30d") : IRequest<DashboardReportFile>;

public sealed class GetDashboardReportQueryHandler(ISender sender, IDashboardReportPdfService pdf)
    : IRequestHandler<GetDashboardReportQuery, DashboardReportFile>
{
    public async Task<DashboardReportFile> Handle(GetDashboardReportQuery request, CancellationToken cancellationToken)
    {
        // Reutiliza validación, cálculo y alcance de sede del dashboard; no recibe cifras del navegador.
        var dashboard = await sender.Send(new GetDashboardBiQuery(request.Periodo), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var fecha = dashboard.Periodo!.Fin;
        return new DashboardReportFile(pdf.Generar(dashboard), $"Reporte-CasaAndina-{fecha:yyyyMMdd}-{request.Periodo}.pdf");
    }
}
