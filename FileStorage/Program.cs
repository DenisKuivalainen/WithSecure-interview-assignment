using FileStorage.Middleware;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (2L * 1024 + 100) * 1024 * 1024; // 3GB, it will alow 2GB file + some size will be taken by other body data
});
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Custom middleware
app.UseMiddleware<LoggingMiddleware>();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
