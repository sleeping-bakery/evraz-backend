using System.Text;
using CodeReview.Services.Helpers;
using CodeReview.Services.Models.Prompts;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services.Models.Executors;

public class DotNetPromptsExecutor : IPromptsExecutor<BaseDotNetPrompt>
{
    public List<BaseDotNetPrompt> Prompts { get; set; } =
    [
        new DotNetProjectStructure(),
        new DotNetDependencyReviewer(),
        new DotNetFileReviewer()
    ];

    public async Task<StringBuilder> ExecutePrompts(Stream stream, StringBuilder sb, CancellationToken token)
    {
        var returnedTasks = new List<Task<TaskResult>>();
        var clonedStreams = Prompts.Select(_ => StreamCloner.CloneStream(stream)).ToList();
        try
        {
            for (var index = 0; index < Prompts.Count; index++) Prompts[index].Execute(ZipArchive.Open(clonedStreams[index]), returnedTasks, token);

            var uniqueTasks = new Dictionary<string, Task<TaskResult>>();

            while (returnedTasks.Where(returnedTask => !returnedTask.IsCanceled).Any(returnedTask => !returnedTask.Result.Success))
            {
                await Task.WhenAll(returnedTasks);

                foreach (var returnedTask in returnedTasks)
                    if (!uniqueTasks.TryGetValue(returnedTask.Result.Name, out var existingTask) ||
                        (existingTask.Result.TimeEnd < returnedTask.Result.TimeEnd && returnedTask.Result.Success))
                        uniqueTasks[returnedTask.Result.Name] = returnedTask;

                var filteredTasks = returnedTasks.Where(task => task is { IsCanceled: false, Result.Success: false })
                    .Select(faultedTask => Task.Run(async () => await faultedTask.Result.Func(faultedTask.Result.Name), token)).ToList();
                if (filteredTasks.Count == returnedTasks.Count)
                    throw new Exception("На данный момент сервер LLM не принимает сообщения, попробуйте позже");

                returnedTasks = filteredTasks;
            }

            foreach (var clonedStream in clonedStreams) await clonedStream.DisposeAsync();

            GenerateReport(sb, uniqueTasks);


            return sb;
        }
        finally
        {
            foreach (var clonedStream in clonedStreams) await clonedStream.DisposeAsync();
        }
    }

    public static void GenerateReport(StringBuilder sb, Dictionary<string, Task<TaskResult>> uniqueTasks)
    {
        sb.AppendLine($"Дата отчета: {DateTime.Now}");

        try
        {
            sb.AppendLine(uniqueTasks[nameof(DotNetDependencyReviewer)].Result.Result);
            uniqueTasks.Remove(nameof(DotNetDependencyReviewer));
        }
        catch
        {
            // ignored
        }

        try
        {
            sb.AppendLine(uniqueTasks[nameof(DotNetProjectStructure)].Result.Result);
            uniqueTasks.Remove(nameof(DotNetProjectStructure));
        }
        catch
        {
            // ignored
        }

        foreach (var uniqueTask in uniqueTasks.Where(uniqueTask => !uniqueTask.Value.IsCanceled).OrderByDescending(uniqueTask => uniqueTask.Value.Result.TimeEnd))
        {
            sb.AppendLine(uniqueTask.Value.Result.Result);
            sb.AppendLine("");
            sb.AppendLine("---");
            sb.AppendLine("");
        }
    }
}