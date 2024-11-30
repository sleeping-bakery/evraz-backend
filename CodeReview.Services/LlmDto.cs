namespace CodeReview.Services;

using System.Text.Json.Serialization;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class LlmAnswer
{
    [JsonPropertyName("request_id")]
    public int RequestId { get; set; }

    [JsonPropertyName("response_id")]
    public int ResponseId { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; }

    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; }

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; }

    [JsonPropertyName("timestamps")]
    public Timestamps Timestamps { get; set; }
}

public class Choice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public Message Message { get; set; }
}

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}

public class Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("tokens_per_second")]
    public double TokensPerSecond { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

public class Timestamps
{
    [JsonPropertyName("request_time")]
    public DateTime RequestTime { get; set; }

    [JsonPropertyName("start_time_generation")]
    public DateTime StartTimeGeneration { get; set; }

    [JsonPropertyName("end_time_generation")]
    public DateTime EndTimeGeneration { get; set; }

    [JsonPropertyName("queue_wait_time")]
    public double QueueWaitTime { get; set; }

    [JsonPropertyName("generation_time")]
    public double GenerationTime { get; set; }
}
