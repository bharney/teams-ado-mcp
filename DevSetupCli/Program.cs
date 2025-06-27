using Microsoft.Extensions.Configuration;
using System.CommandLine;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DevSetupCli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Teams-ADO MCP Development Setup CLI")
        {
            Description = "Helps configure Azure DevOps authentication and User Secrets for local development"
        };

        // Create commands
        var loginCommand = CreateLoginCommand();
        var patCommand = CreatePatCommand();
        var secretsCommand = CreateSecretsCommand();
        var statusCommand = CreateStatusCommand();

        rootCommand.AddCommand(loginCommand);
        rootCommand.AddCommand(patCommand);
        rootCommand.AddCommand(secretsCommand);
        rootCommand.AddCommand(statusCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static Command CreateLoginCommand()
    {
        var loginCommand = new Command("login", "Help with Azure DevOps authentication setup");
        
        var orgOption = new Option<string>("--organization", "Azure DevOps organization name") { IsRequired = false };
        loginCommand.AddOption(orgOption);

        loginCommand.SetHandler(async (string organization) =>
        {
            Console.WriteLine("🔐 Azure DevOps Authentication Setup");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            if (string.IsNullOrEmpty(organization))
            {
                Console.Write("Enter your Azure DevOps organization name: ");
                organization = Console.ReadLine() ?? "";
            }

            if (string.IsNullOrEmpty(organization))
            {
                Console.WriteLine("❌ Organization name is required");
                return;
            }

            Console.WriteLine($"Setting up authentication for organization: {organization}");
            Console.WriteLine();

            // Step 1: Open Azure DevOps in browser
            Console.WriteLine("📋 Step 1: Create Personal Access Token");
            Console.WriteLine("---------------------------------------");
            var patUrl = $"https://dev.azure.com/{organization}/_usersSettings/tokens";
            Console.WriteLine($"Opening Azure DevOps PAT page: {patUrl}");
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = patUrl,
                    UseShellExecute = true
                });
                Console.WriteLine("✅ Browser opened successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Could not open browser: {ex.Message}");
                Console.WriteLine($"Please manually navigate to: {patUrl}");
            }

            Console.WriteLine();
            Console.WriteLine("📝 When creating the PAT, please configure:");
            Console.WriteLine("  • Name: Teams-ADO-MCP-Local-Dev");
            Console.WriteLine("  • Organization: All accessible organizations");
            Console.WriteLine("  • Expiration: 30 days (recommended for development)");
            Console.WriteLine("  • Scopes: Full access (or at minimum 'Work Items: Read & Write')");
            Console.WriteLine();

            Console.WriteLine("After creating the PAT, run:");
            Console.WriteLine($"  devsetup pat --token YOUR_PAT_TOKEN --organization {organization}");
            Console.WriteLine();

        }, orgOption);

        return loginCommand;
    }

    static Command CreatePatCommand()
    {
        var patCommand = new Command("pat", "Configure Personal Access Token in User Secrets");
        
        var tokenOption = new Option<string>("--token", "Personal Access Token") { IsRequired = true };
        var orgOption = new Option<string>("--organization", "Azure DevOps organization name") { IsRequired = true };
        var projectOption = new Option<string>("--project", "Default project name") { IsRequired = false };
        
        patCommand.AddOption(tokenOption);
        patCommand.AddOption(orgOption);
        patCommand.AddOption(projectOption);

        patCommand.SetHandler(async (string token, string organization, string project) =>
        {
            Console.WriteLine("🔧 Configuring Personal Access Token");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            if (string.IsNullOrEmpty(project))
            {
                Console.Write("Enter your default project name (optional): ");
                project = Console.ReadLine() ?? "";
            }

            // Test the PAT first
            Console.WriteLine("🧪 Testing Personal Access Token...");
            var isValid = await TestPatAsync(token, organization);
            
            if (!isValid)
            {
                Console.WriteLine("❌ PAT validation failed. Please check your token and try again.");
                return;
            }

            Console.WriteLine("✅ PAT is valid!");
            Console.WriteLine();

            // Configure User Secrets for both projects
            var projects = new[] { "TeamsBot", "McpServer" };
            
            foreach (var proj in projects)
            {
                Console.WriteLine($"🔐 Configuring User Secrets for {proj}...");
                
                try
                {
                    await RunDotnetCommand($"user-secrets set \"AzureDevOps:PersonalAccessToken\" \"{token}\"", proj);
                    await RunDotnetCommand($"user-secrets set \"AzureDevOps:Organization\" \"{organization}\"", proj);
                    
                    if (!string.IsNullOrEmpty(project))
                    {
                        await RunDotnetCommand($"user-secrets set \"AzureDevOps:Project\" \"{project}\"", proj);
                    }
                    
                    Console.WriteLine($"✅ {proj} configured successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to configure {proj}: {ex.Message}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("🎉 Configuration complete!");
            Console.WriteLine("Run 'devsetup status' to verify your setup.");

        }, tokenOption, orgOption, projectOption);

        return patCommand;
    }

    static Command CreateSecretsCommand()
    {
        var secretsCommand = new Command("secrets", "Manage User Secrets configuration");
        
        var listCommand = new Command("list", "List all configured secrets");
        var clearCommand = new Command("clear", "Clear all secrets");
        var initCommand = new Command("init", "Initialize User Secrets for projects");

        listCommand.SetHandler(async () =>
        {
            Console.WriteLine("📋 Current User Secrets Configuration");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            var projects = new[] { "TeamsBot", "McpServer" };
            
            foreach (var project in projects)
            {
                Console.WriteLine($"🔧 {project}:");
                try
                {
                    var result = await RunDotnetCommand("user-secrets list", project, captureOutput: true);
                    if (string.IsNullOrEmpty(result.Trim()))
                    {
                        Console.WriteLine("  No secrets configured");
                    }
                    else
                    {
                        foreach (var line in result.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Console.WriteLine($"  {line.Trim()}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Error: {ex.Message}");
                }
                Console.WriteLine();
            }
        });

        clearCommand.SetHandler(async () =>
        {
            Console.WriteLine("🗑️  Clearing User Secrets");
            Console.WriteLine("========================");
            Console.WriteLine();

            Console.Write("Are you sure you want to clear all secrets? (y/N): ");
            var confirm = Console.ReadLine();
            
            if (confirm?.ToLower() != "y")
            {
                Console.WriteLine("❌ Operation cancelled");
                return;
            }

            var projects = new[] { "TeamsBot", "McpServer" };
            
            foreach (var project in projects)
            {
                Console.WriteLine($"🗑️  Clearing secrets for {project}...");
                try
                {
                    await RunDotnetCommand("user-secrets clear", project);
                    Console.WriteLine($"✅ {project} secrets cleared");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to clear {project}: {ex.Message}");
                }
            }
        });

        initCommand.SetHandler(async () =>
        {
            Console.WriteLine("🚀 Initializing User Secrets");
            Console.WriteLine("============================");
            Console.WriteLine();

            var projects = new[] { "TeamsBot", "McpServer" };
            
            foreach (var project in projects)
            {
                Console.WriteLine($"🔧 Initializing User Secrets for {project}...");
                try
                {
                    await RunDotnetCommand("user-secrets init", project);
                    Console.WriteLine($"✅ {project} initialized");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to initialize {project}: {ex.Message}");
                }
            }
        });

        secretsCommand.AddCommand(listCommand);
        secretsCommand.AddCommand(clearCommand);
        secretsCommand.AddCommand(initCommand);

        return secretsCommand;
    }

    static Command CreateStatusCommand()
    {
        var statusCommand = new Command("status", "Check configuration status");

        statusCommand.SetHandler(async () =>
        {
            Console.WriteLine("📊 Development Environment Status");
            Console.WriteLine("=================================");
            Console.WriteLine();

            // Check .NET SDK
            Console.WriteLine("🔧 .NET SDK:");
            try
            {
                var dotnetVersion = await RunDotnetCommand("--version", ".", captureOutput: true);
                Console.WriteLine($"  ✅ .NET {dotnetVersion.Trim()} installed");
            }
            catch
            {
                Console.WriteLine("  ❌ .NET SDK not found");
            }
            Console.WriteLine();

            // Check User Secrets configuration
            Console.WriteLine("🔐 User Secrets Status:");
            var projects = new[] { "TeamsBot", "McpServer" };
            
            foreach (var project in projects)
            {
                Console.WriteLine($"  {project}:");
                try
                {
                    var secrets = await RunDotnetCommand("user-secrets list", project, captureOutput: true);
                    var hasAdoPat = secrets.Contains("AzureDevOps:PersonalAccessToken");
                    var hasAdoOrg = secrets.Contains("AzureDevOps:Organization");
                    
                    Console.WriteLine($"    PAT: {(hasAdoPat ? "✅ Configured" : "❌ Missing")}");
                    Console.WriteLine($"    Organization: {(hasAdoOrg ? "✅ Configured" : "❌ Missing")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ Error: {ex.Message}");
                }
            }
            Console.WriteLine();

            // Check if projects build
            Console.WriteLine("🏗️  Build Status:");
            try
            {
                await RunDotnetCommand("build --no-restore", ".", captureOutput: true);
                Console.WriteLine("  ✅ Solution builds successfully");
            }
            catch
            {
                Console.WriteLine("  ❌ Build failed - run 'dotnet build' for details");
            }
            Console.WriteLine();

            Console.WriteLine("💡 Next steps:");
            Console.WriteLine("  • If PAT is missing: devsetup login");
            Console.WriteLine("  • To list secrets: devsetup secrets list");
            Console.WriteLine("  • For help: devsetup --help");
        });

        return statusCommand;
    }

    static async Task<bool> TestPatAsync(string token, string organization)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", 
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));

            var response = await client.GetAsync($"https://dev.azure.com/{organization}/_apis/projects?api-version=7.0");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    static async Task<string> RunDotnetCommand(string arguments, string workingDirectory, bool captureOutput = false)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start dotnet process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command failed with exit code {process.ExitCode}");
        }

        if (captureOutput)
        {
            return await process.StandardOutput.ReadToEndAsync();
        }

        return string.Empty;
    }
}
