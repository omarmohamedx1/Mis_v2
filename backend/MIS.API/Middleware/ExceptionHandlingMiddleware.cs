using System.Net;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using Npgsql;

namespace MIS.API.Middleware;

public sealed class ExceptionHandlingMiddleware
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (HrValidationException exception)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure(exception.Message, exception.Errors));
        }
        catch (HrNotFoundException exception)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure(exception.Message));
        }
        catch (HrConflictException exception)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure(exception.Message));
        }
        catch (HrForbiddenException exception)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure(exception.Message));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException &&
                                                   postgresException.SqlState is PostgresErrorCodes.UniqueViolation or
                                                       PostgresErrorCodes.ExclusionViolation or
                                                       PostgresErrorCodes.SerializationFailure)
        {
            _logger.LogWarning(exception, "A database conflict rejected an API request.");
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure(
                "The request conflicts with existing data. Refresh and try again."));
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or
                                                   PostgresErrorCodes.DeadlockDetected)
        {
            _logger.LogWarning(exception, "A concurrent database operation rejected an API request.");
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure(
                "The data changed during this request. Refresh and try again."));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException &&
                                                   postgresException.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation)
        {
            _logger.LogWarning(exception, "A database integrity constraint rejected an API request.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure("The request violates a data integrity rule."));
        }
        catch (Exception exception)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(exception,
                "Unhandled API exception. TraceId: {TraceId}; Request: {Method} {Path}",
                traceId, context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            var error = ApiErrorResponse.Failure("An unexpected error occurred.");
            await context.Response.WriteAsJsonAsync(new
            {
                error.Success,
                error.Message,
                error.Errors,
                traceId
            });
        }
    }
}
