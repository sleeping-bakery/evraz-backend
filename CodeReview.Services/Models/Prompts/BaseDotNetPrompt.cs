using System.Text.Json;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services.Models.Prompts;

public abstract class BaseDotNetPrompt : IPrompt
{
    private static string DefaultSystemPrompt =>
        "Отвечай на русском языке. Давай ответы в формате .md. Ты мастер бот для ревью C# проектов. Не давай комментариев, если не нашел замечаний.";

    public abstract Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; }

    public abstract string Prompt { get; set; }

    public void Execute(ZipArchive zipArchive, List<Task<TaskResult>> returnedTasks, CancellationToken token)
    {
        FillTasks(zipArchive);
        returnedTasks.AddRange(Tasks.Select(task => Task.Run(async () => await task.Value(task.Key), token)));
    }

    protected abstract void FillTasks(ZipArchive zipArchive);

    protected string GenerateRequestContent(string stringForReview)
    {
        var requestObject = new
        {
            model = "mistral-nemo-instruct-2407",
            messages = new[]
            {
                new { role = "system", content = DefaultSystemPrompt },
                new { role = "system", content = Prompt },
                new { role = "user", content = stringForReview }
            },
            max_tokens = 1024,
            temperature = 0.1
        };

        return JsonSerializer.Serialize(requestObject, new JsonSerializerOptions { WriteIndented = false });
    }
}