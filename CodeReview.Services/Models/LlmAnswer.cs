using System.Text.Json.Serialization;

namespace CodeReview.Services.Models;

public class LlmAnswer
{
    [JsonPropertyName("request_id")] public int RequestId { get; set; }

    [JsonPropertyName("response_id")] public int ResponseId { get; set; }

    [JsonPropertyName("model")] public string Model { get; set; }

    [JsonPropertyName("provider")] public string Provider { get; set; }

    [JsonPropertyName("choices")] public List<Choice> Choices { get; set; }

    [JsonPropertyName("usage")] public Usage Usage { get; set; }

    [JsonPropertyName("timestamps")] public Timestamps Timestamps { get; set; }
}