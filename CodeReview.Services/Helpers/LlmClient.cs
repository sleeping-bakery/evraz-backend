using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeReview.Services.Models;

namespace CodeReview.Services.Helpers;

public class LlmClient
{
    private readonly string _apiUrl;
    private readonly string _authorizationKey;
    private readonly HttpClient _httpClient;

    public LlmClient(string apiUrl, string authorizationKey)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(600);
        _apiUrl = apiUrl;
        _authorizationKey = authorizationKey;
    }

    public async Task<string> SendRequestAsync(string contentJson)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);

            request.Headers.Add("Authorization", _authorizationKey);
            request.Content = new StringContent(contentJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(await response.Content.ReadAsStringAsync());

            var llmAnswer = JsonSerializer.Deserialize<LlmAnswer>(responseBody);

            if (llmAnswer == null || llmAnswer.Choices.Count == 0) throw new InvalidOperationException("Invalid response from LLM API.");

            return Regex.Unescape(llmAnswer.Choices[0].Message.Content);
        }
        finally
        {
            _httpClient.Dispose();
        }
    }
}