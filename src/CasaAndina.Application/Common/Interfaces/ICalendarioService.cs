namespace CasaAndina.Application.Common.Interfaces;

public record EstadoCalendario(bool Configurado, bool Conectado, bool RequiereReconectar,
    DateTime? UltimaSincronizacionUtc, string? UrlCalendario);
public record ResultadoSincronizacion(int Actualizados, int Eliminados, DateTime SincronizadoUtc);

public interface ICalendarioService
{
    Task<EstadoCalendario> EstadoAsync(int clienteId, CancellationToken ct);
    string CrearAutorizacion(int usuarioId, int clienteId, string correlacion);
    Task<int> CompletarAutorizacionAsync(string state, string code, string correlacion, CancellationToken ct);
    Task<ResultadoSincronizacion> SincronizarAsync(int clienteId, CancellationToken ct);
    Task DesconectarAsync(int clienteId, CancellationToken ct);
}
