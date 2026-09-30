using Microsoft.AspNetCore.Mvc;
using SensSera.Application.Llm;
using System.Text.Json;

namespace SensSera.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, ex);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status,title, detail) = ex switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", ex.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),
            // Provider detail (may echo auth errors) stays in the log, never in the response.
            LlmUnavailableException => (StatusCodes.Status503ServiceUnavailable, "AI insights unavailable", "The AI provider is not configured or not reachable right now."),
            _=> (StatusCodes.Status500InternalServerError,"An unexpected error occured" ,"An unexpected error occured. Please try again later.")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

}