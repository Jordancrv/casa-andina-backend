namespace CasaAndina.Application.Dashboard.DTOs;

public sealed record DashboardBiDto(
    string SedeNombre,
    DashboardBiKpisDto Kpis,
    IReadOnlyList<ReservaSemanaBiDto> ReservasSemana,
    IReadOnlyList<OcupacionSedeBiDto> OcupacionSedes,
    int CanceladasPeriodo,
    CanalReservasBiDto Canales);

public sealed record DashboardBiKpisDto(
    int TotalReservas,
    int TotalReservasPeriodoAnterior,
    decimal ImporteReservadoSoles,
    decimal ImporteReservadoPeriodoAnterior,
    decimal OcupacionActualPct,
    int TotalClientesActivos,
    int ClientesPeriodoAnterior);

public sealed record ReservaSemanaBiDto(
    string SemanaLabel,
    int Confirmadas,
    int Canceladas,
    decimal AlturaPctConfirmadas,
    decimal AlturaPctCanceladas);

public sealed record OcupacionSedeBiDto(
    string SedeNombre,
    string Ciudad,
    decimal OcupacionPct,
    int Cantidad);

public sealed record CanalReservasBiDto(
    int ReservasDirectas,
    int ReservasOTA,
    int TotalReservas,
    decimal TicketPromedioSoles,
    decimal EstadiaPromedioNoches);
