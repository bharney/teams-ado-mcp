namespace TeamsBot.Configuration;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";
    public string Endpoint { get; set; } = "https://talkingpoints.openai.azure.com/"; // https://your-openai-resource.openai.azure.com/
    public string ChatDeployment { get; set; } = "gpt-4.1"; // e.g. gpt-4o-mini or custom deployment name
    public string ApiVersion { get; set; } = "2024-08-01-preview"; // keep configurable for future updates
    public int MaxOutputTokens { get; set; } = 512;
    public float Temperature { get; set; } = 0.2f;
    public bool Enabled { get; set; } = true;
}
