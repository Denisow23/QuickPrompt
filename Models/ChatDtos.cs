using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuickPrompt.Models;

public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public object Content { get; set; } = string.Empty;
}

public class MessageContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageUrlWrapper? ImageUrl { get; set; }
}

public class ImageUrlWrapper
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = new();
}

public class Choice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
}

public class ModelsResponse
{
    [JsonPropertyName("data")]
    public List<ModelItem> Data { get; set; } = new();
}

public class ModelItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
