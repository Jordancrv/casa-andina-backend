using System.Net;
using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using ValidationException = CasaAndina.Application.Common.Exceptions.ValidationException;

namespace CasaAndina.Api.Middleware;

/// <summary>
/// Traduce excepciones de Application/Domain a códigos HTTP consistentes, para que
/// los controllers no tengan try/catch repetido en cada acción.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado procesando {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, payload) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                (object)new { title = "Error de validación", errors = validationEx.Errors }),

            NotFoundException => (
                HttpStatusCode.NotFound,
                new { title = exception.Message }),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                new { title = exception.Message }),

            _ => (
                HttpStatusCode.InternalServerError,
                new { title = "Ocurrió un error inesperado." })
        };

        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
