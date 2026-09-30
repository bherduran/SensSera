using Microsoft.AspNetCore.Mvc;
using SensSera.Application.Llm;
using System.Text.Json;

namespace SensSera.Api.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, title, detail) = Map(ex);
            Log(ex, status, context.Request.Path);
            await WriteProblemAsync(context, status, title, detail);
        }
    }

    private static (int Status, string Title, string Detail) Map(Exception ex) => ex switch
    {
        KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", ex.Message),
        // Thrown only for failed authentication (bad credentials, missing/invalid refresh token).
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message),
        ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),
        // Provider detail (may echo auth errors) stays in the log, never in the response.
        LlmUnavailableException => (StatusCodes.Status503ServiceUnavailable, "AI insights unavailable", "The AI provider is not configured or not reachable right now."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "An unexpected error occurred. Please try again later."),
    };

    // Expected failures (4xx, provider outage) are routine: outside Development they're logged as
    // type + message only, keeping stack traces and inner exceptions out of prod logs. Unknown 500s
    // keep the full exception — those are bugs and can't be diagnosed without the stack.
    private void Log(Exception ex, int status, PathString path)
    {
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception on {Path}", path);
        else if (env.IsDevelopment())
            logger.LogWarning(ex, "Request to {Path} failed with {Status}", path, status);
        else
            logger.LogWarning("Request to {Path} failed with {Status}: {ExceptionType} {Message}",
                path, status, ex.GetType().Name, ex.Message);
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
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
