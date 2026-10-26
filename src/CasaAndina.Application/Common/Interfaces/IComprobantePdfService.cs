using CasaAndina.Application.Reservas.DTOs;

namespace CasaAndina.Application.Common.Interfaces;

public interface IComprobantePdfService
{
    byte[] Generar(ComprobanteDocumentoDto documento);
}
