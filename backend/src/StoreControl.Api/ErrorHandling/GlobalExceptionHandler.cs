using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Application.Common.Exceptions;

namespace StoreControl.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, errorCode) = exception switch
        {
            RequestValidationException => (
                StatusCodes.Status400BadRequest,
                "A solicitação contém dados inválidos.",
                "validation_error"),
            ResourceNotFoundException => (
                StatusCodes.Status404NotFound,
                "Recurso não encontrado.",
                "resource_not_found"),
            ResourceConflictException => (
                StatusCodes.Status409Conflict,
                "A operação conflita com o estado atual.",
                "resource_conflict"),
            OperationForbiddenException => (
                StatusCodes.Status403Forbidden,
                "A operação não é permitida.",
                "operation_forbidden"),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Ocorreu um erro inesperado.",
                "unexpected_error")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled request failure at {RequestMethod} {RequestPath}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(
                "Request rejected with {StatusCode} at {RequestMethod} {RequestPath}: {Reason}",
                status,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.Message);
        }

        httpContext.Response.StatusCode = status;
        ProblemDetails problemDetails = exception is RequestValidationException validationException
            ? new ValidationProblemDetails(validationException.Errors.ToDictionary(
                error => error.Key,
                error => error.Value))
            : new ProblemDetails();
        problemDetails.Status = status;
        problemDetails.Title = title;
        problemDetails.Detail = status >= StatusCodes.Status500InternalServerError
            ? "A solicitação não pôde ser concluída."
            : exception.Message;
        problemDetails.Type = $"https://httpstatuses.com/{status}";
        problemDetails.Extensions["errorCode"] = errorCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }
}
