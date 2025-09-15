public partial class Program { }

public static class EntryPoint
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHealthChecks();

        // Configuration binding for Azure DevOps options (placeholder values handled in service)
        builder.Services.Configure<McpServer.Configuration.AzureDevOpsOptions>(builder.Configuration.GetSection("AzureDevOps"));

        // Register Azure DevOps service with HttpClient factory
        builder.Services.AddHttpClient<McpServer.Services.AzureDevOpsService>();
        builder.Services.AddScoped<McpServer.Services.IAzureDevOpsService, McpServer.Services.AzureDevOpsService>();

        // Tool registry and tools
        builder.Services.AddSingleton<McpServer.Services.IMcpToolRegistry, McpServer.Services.McpToolRegistry>();
        builder.Services.AddScoped<McpServer.Tools.CreateWorkItemTool>();

        // Populate tool registry after DI container is built
        builder.Services.AddTransient<IStartupFilter, ToolRegistryStartupFilter>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.MapControllers();
        app.MapHealthChecks("/health");

        app.Run();
    }
}

public class ToolRegistryStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<McpServer.Services.IMcpToolRegistry>();
            var createTool = scope.ServiceProvider.GetRequiredService<McpServer.Tools.CreateWorkItemTool>();
            registry.RegisterTool(createTool);
            next(app);
        };
    }
}
