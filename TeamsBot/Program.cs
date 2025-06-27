using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using TeamsBot.Handlers;
using TeamsBot.Configuration;
using TeamsBot.Services;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure authentication following MCP patterns
// Use Managed Identity in Azure environments, fallback to DefaultAzureCredential for local dev
builder.Services.AddSingleton<DefaultAzureCredential>();

// Add secure configuration provider following MCP patterns
builder.Services.AddSecureConfiguration();

// Add Bot Framework Authentication
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Add Bot Framework Adapter
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Add Teams AI Bot - using advanced implementation with intent detection
builder.Services.AddTransient<IBot, TeamsAIActivityHandler>();

// Add custom services for Azure DevOps and conversation intelligence
builder.Services.AddScoped<IConversationIntelligenceService, ConversationIntelligenceService>();

// Add Azure DevOps service with HTTP client configuration
builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>(client =>
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

// Make the Program class public for testing
public partial class Program { }
