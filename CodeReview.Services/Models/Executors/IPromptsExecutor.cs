using System.Text;
using CodeReview.Services.Models.Prompts;

namespace CodeReview.Services.Models.Executors;

public interface IPromptsExecutor<T> where T : IPrompt
{
    List<T> Prompts { get; set; }
    Task<StringBuilder> ExecutePrompts(Stream stream, StringBuilder sb, CancellationToken token);
}