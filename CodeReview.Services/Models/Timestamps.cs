using System.Text.Json.Serialization;

namespace CodeReview.Services.Models;

public class Timestamps
{
    [JsonPropertyName("request_time")] public DateTime RequestTime { get; set; }

    [JsonPropertyName("start_time_generation")]
    public DateTime StartTimeGeneration { get; set; }

    [JsonPropertyName("end_time_generation")]
    public DateTime EndTimeGeneration { get; set; }

    [JsonPropertyName("queue_wait_time")] public double QueueWaitTime { get; set; }

    [JsonPropertyName("generation_time")] public double GenerationTime { get; set; }
}