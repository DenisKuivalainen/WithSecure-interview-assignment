using System.Text;

namespace FileStorage.Middleware;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var logMessages = new StringBuilder();

        logMessages.AppendLine(
            $"Request: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}"
        );

        if (context.Request.Body.CanSeek)
        {
            context.Request.Body.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(context.Request.Body))
            {
                var body = await reader.ReadToEndAsync();
                logMessages.AppendLine($"Request Body: {body}");
            }
            context.Request.Body.Seek(0, SeekOrigin.Begin);
        }

        if (context.Request.HasFormContentType && context.Request.Form.Files.Any())
        {
            foreach (var file in context.Request.Form.Files)
            {
                logMessages.AppendLine($"File: {file.FileName} - {file.Length} bytes");
            }
        }
        _logger.LogInformation(logMessages.ToString());
        logMessages.Clear();

        var originalBody = context.Response.Body;
        using (var memoryStream = new MemoryStream())
        {
            context.Response.Body = memoryStream;

            await _next(context);

            logMessages.AppendLine($"Response: {context.Response.StatusCode}");

            memoryStream.Seek(0, SeekOrigin.Begin);
            using (var streamReader = new StreamReader(memoryStream))
            {
                var body = await streamReader.ReadToEndAsync();
                logMessages.AppendLine($"Response Body: {body}");

                memoryStream.Seek(0, SeekOrigin.Begin);
                await memoryStream.CopyToAsync(originalBody);
            }

            logMessages.AppendLine(
                $"Request processed in {((int)(DateTime.UtcNow - startTime).TotalMilliseconds)}ms."
            );
            _logger.LogInformation(logMessages.ToString());
        }
    }
}
