using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using QuickPrompt.Models;

namespace QuickPrompt.Services;

public class OpenAiLikeClient
{
    private readonly HttpClient _httpClient = new();
    private AppSettings _settings;

    public OpenAiLikeClient(AppSettings settings)
    {
        _settings = settings;
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<List<string>> GetModelsAsync(string apiKey)
    {
        using var request = CreateRequest(HttpMethod.Get, "/models", null, apiKey);
        using var response = await _httpClient.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Models request failed ({(int)response.StatusCode}): {payload}");

        var data = JsonSerializer.Deserialize<ModelsResponse>(payload);
        return data?.Data.Select(x => x.Id).OrderBy(x => x).ToList() ?? new List<string>();
    }

    public async Task<string> SendChatAsync(ChatCompletionRequest requestBody, string apiKey)
    {
        using var request = CreateRequest(HttpMethod.Post, "/chat/completions", requestBody, apiKey);
        using var response = await _httpClient.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Chat request failed ({(int)response.StatusCode}): {payload}");

        var data = JsonSerializer.Deserialize<ChatCompletionResponse>(payload);
        var content = data?.Choices.FirstOrDefault()?.Message?.Content;
        return ParseContent(content) ?? "Пустой ответ от модели.";
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body, string apiKey)
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var req = new HttpRequestMessage(method, $"{baseUrl}{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        foreach (var (name, value) in _settings.AdditionalHeaders)
        {
            req.Headers.TryAddWithoutValidation(name, value);
        }

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return req;
    }

    private static string? ParseContent(object? content)
    {
        if (content is null) return null;
        if (content is string text) return text;

        if (content is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String) return el.GetString();

            if (el.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in el.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var t))
                        parts.Add(t.GetString() ?? string.Empty);
                }
                return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
        }

        return content.ToString();
    }
}
