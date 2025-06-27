using McpServer.Services;
using McpServer.Tools;
using McpServer.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Azure DevOps options
builder.Services.Configure<AzureDevOpsOptions>(
    builder.Configuration.GetSection(AzureDevOpsOptions.SectionName));

// Register HTTP client for Azure DevOps service
builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();

// Register MCP services
builder.Services.AddSingleton<IMcpToolRegistry, McpToolRegistry>();

// Register MCP tools
builder.Services.AddScoped<IMcpTool, CreateWorkItemTool>();

var app = builder.Build();

// Initialize the tool registry with all registered tools using a scope
using (var scope = app.Services.CreateScope())
{
    var toolRegistry = scope.ServiceProvider.GetRequiredService<IMcpToolRegistry>();
    var tools = scope.ServiceProvider.GetServices<IMcpTool>();
    foreach (var tool in tools)
    {
        toolRegistry.RegisterTool(tool);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map controllers for MCP endpoints
app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Make Program class accessible to tests
public partial class Program { }
