namespace Match3.LevelCli.Services;

/// <summary>
/// Configuration for the LLM level designer API calls.
/// </summary>
public sealed class LlmConfig
{
    /// <summary>Anthropic API key.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Model name (default: claude-sonnet-4-20250514).</summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>API base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>Max tokens for response.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Temperature (0.0-1.0).</summary>
    public float Temperature { get; set; } = 0.7f;
}
