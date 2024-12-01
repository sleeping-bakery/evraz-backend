namespace CodeReview.Services.Models;

public class TaskResult(string name, string result, Func<string, Task<TaskResult>> func, bool success)
{
    public Func<string, Task<TaskResult>> Func = func;

    public bool Success = success;
    public string Name { get; set; } = name;
    public string Result { get; set; } = result;
    public DateTime TimeEnd { get; set; } = DateTime.Now;
}