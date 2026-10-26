using CasaAndina.Application.Dashboard.DTOs;

namespace CasaAndina.Application.Common.Interfaces;

public interface IDashboardReportPdfService
{
    byte[] Generar(DashboardBiDto dashboard);
}
