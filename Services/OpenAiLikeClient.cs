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
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };
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
        var items = data?.Data ?? new List<ModelItem>();
        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<string> SendChatAsync(ChatCompletionRequest requestBody, string apiKey)
    {
        using var request = CreateRequest(HttpMethod.Post, "/chat/completions", requestBody, apiKey);
        using var response = await _httpClient.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Chat request failed ({(int)response.StatusCode}): {payload}");

        var data = JsonSerializer.Deserialize<ChatCompletionResponse>(payload);
        var choices = data?.Choices ?? new List<Choice>();
        var content = choices.FirstOrDefault()?.Message?.Content;
        var parsed = ParseContent(content);
        if (!string.IsNullOrWhiteSpace(parsed))
        {
            return parsed;
        }

        if (choices.Count == 0)
        {
            throw new InvalidOperationException($"Пустой ответ сервера (нет choices). Raw payload: {payload}");
        }

        return "Пустой ответ от модели.";
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            throw new InvalidOperationException("Base URL не задан. Откройте настройки и укажите API endpoint.");
        }

        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        if (!Uri.TryCreate($"{baseUrl}{path}", UriKind.Absolute, out var targetUri))
        {
            throw new InvalidOperationException("Base URL имеет неверный формат.");
        }

        var req = new HttpRequestMessage(method, targetUri);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        foreach (var (name, value) in _settings.AdditionalHeaders ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

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
