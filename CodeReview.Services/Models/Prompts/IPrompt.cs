using SharpCompress.Archives.Zip;

namespace CodeReview.Services.Models.Prompts;

public interface IPrompt
{
    Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; }

    string Prompt { get; set; }
    void Execute(ZipArchive zipArchive, List<Task<TaskResult>> returnedTasks, CancellationToken token);
}