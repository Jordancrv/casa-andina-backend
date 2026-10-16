using CasaAndina.Application.Common.Exceptions;
using FluentValidation.Results;

namespace CasaAndina.Application.Reservas.Commands;

public static class ReservaCompraRules
{
    public const decimal MaximoImportePersistible = 99_999_999.99m;

    public static decimal ValidarImportePersistible(decimal importe, string concepto)
    {
        if (importe < 0 || importe > MaximoImportePersistible)
        {
            throw new ValidationException([new ValidationFailure(
                concepto,
                $"El importe de {concepto} debe estar entre 0 y {MaximoImportePersistible:N2}.")]);
        }

        return importe;
    }
}
