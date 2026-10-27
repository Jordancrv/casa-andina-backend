using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Chatbot.Queries;

namespace CasaAndina.Application.Common.Interfaces;

public interface ITravelConversationClient
{
    Task<InterpretacionViajeDto?> InterpretarAsync(ConversarViajeQuery consulta, IReadOnlyList<string> ciudades,
        DateTime hoy, CancellationToken cancellationToken);
}
