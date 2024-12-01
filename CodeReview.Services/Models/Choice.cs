using System.Text.Json.Serialization;

namespace CodeReview.Services.Models;

public class Choice
{
    [JsonPropertyName("index")] public int Index { get; set; }

    [JsonPropertyName("message")] public Message Message { get; set; }
}