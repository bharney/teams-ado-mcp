using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using TeamsBot.Handlers;
using TeamsBot.Configuration;
using TeamsBot.Services;
using McpServer.Services; // Use consolidated Azure DevOps service from McpServer
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure authentication following MCP patterns
// Use Managed Identity in Azure environments, fallback to DefaultAzureCredential for local dev
builder.Services.AddSingleton<DefaultAzureCredential>();

// Add secure configuration provider following MCP patterns
builder.Services.AddSecureConfiguration();

// Bind Azure DevOps options so consolidated service receives correct org/project
builder.Services.Configure<McpServer.Configuration.AzureDevOpsOptions>(
    builder.Configuration.GetSection(McpServer.Configuration.AzureDevOpsOptions.SectionName));

// Bind Azure OpenAI options (optional enablement)
builder.Services.Configure<TeamsBot.Configuration.AzureOpenAIOptions>(
    builder.Configuration.GetSection(TeamsBot.Configuration.AzureOpenAIOptions.SectionName));

// Register Azure OpenAI client (uses DefaultAzureCredential) if endpoint configured
builder.Services.AddSingleton<IAzureOpenAIClient>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamsBot.Configuration.AzureOpenAIOptions>>().Value;
    var cred = sp.GetRequiredService<DefaultAzureCredential>();
    if (string.IsNullOrWhiteSpace(opts.Endpoint) || !opts.Enabled)
    {
        return null!; // DI will pass null to optional parameter (handled in service constructor)
    }
    return new AzureOpenAIClient(
        sp.GetRequiredService<ILogger<AzureOpenAIClient>>(),
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamsBot.Configuration.AzureOpenAIOptions>>(),
        cred);
});

// Bot Framework Authentication (standard)
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Add Bot Framework Adapter
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Add Teams AI Bot - using advanced implementation with intent detection
builder.Services.AddTransient<IBot, TeamsAIActivityHandler>();

// Add custom services for Azure DevOps and conversation intelligence
builder.Services.AddScoped<IConversationIntelligenceService, ConversationIntelligenceService>();

// Register consolidated Azure DevOps service implementation from McpServer
builder.Services.AddHttpClient<McpServer.Services.IAzureDevOpsService, McpServer.Services.AzureDevOpsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add additional services for full MCP integration
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IActionItemExtractor, ActionItemExtractor>();
builder.Services.AddHttpClient<IMcpClientService, McpClientService>();

// Add Microsoft Identity Web API authentication for additional endpoints
// Following SFI-compliant federated identity patterns from MCP research
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// In Development only, relax audience validation temporarily (do NOT enable in production)
var relax = builder.Environment.IsDevelopment() ||
            string.Equals(builder.Configuration["AzureAd:RelaxAudienceValidation"], "true", StringComparison.OrdinalIgnoreCase);
if (relax)
{
    builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        // Log that audience validation is relaxed
        options.TokenValidationParameters.ValidateAudience = false;
    });
}

// Add authorization policies for Teams bot permissions
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TeamsFacilitator", policy =>
        policy.RequireClaim("roles", "TeamsFacilitator"));
    options.AddPolicy("TeamsUser", policy =>
        policy.RequireAuthenticatedUser());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks for monitoring and diagnostics
builder.Services.AddHealthChecks();

var app = builder.Build();

// Diagnostic startup logging (do NOT log secret value)
var appIdPresent = !string.IsNullOrWhiteSpace(app.Configuration["MicrosoftAppId"]);
app.Logger.LogInformation("Bot startup appIdPresent={AppIdPresent}", appIdPresent);

// Raw request logging middleware for /api/messages (dev only, trimmed)
if (app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api/messages") && string.Equals(ctx.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                ctx.Request.EnableBuffering();
                using var reader = new StreamReader(ctx.Request.Body, leaveOpen: true);
                var raw = await reader.ReadToEndAsync();
                ctx.Request.Body.Position = 0;
                if (raw.Length > 1000) raw = raw.Substring(0, 1000) + "..."; // avoid huge payloads
                app.Logger.LogDebug("Incoming activity payload (trimmed): {Payload}", raw);
                if (string.IsNullOrWhiteSpace(ctx.Request.Headers["Authorization"]))
                {
                    app.Logger.LogWarning("/api/messages POST missing Authorization header (Emulator must supply AppId & Password). BodyLength={Len}", raw.Length);
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to log raw request body");
            }
        }
        await next();
    });
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Add health check endpoint
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.MapControllers();

app.Run();

// Make the Program class public for testing inside TeamsBot namespace to avoid collision with McpServer Program
namespace TeamsBot
{
    public partial class Program { }
}
