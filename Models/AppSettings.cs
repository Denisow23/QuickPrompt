using System.Collections.Generic;

namespace QuickPrompt.Models;

public class AppSettings
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string EncryptedApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 1000;
    public Dictionary<string, string> AdditionalHeaders { get; set; } = new();
}
