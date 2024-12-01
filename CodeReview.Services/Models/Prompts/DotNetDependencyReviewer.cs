using CodeReview.Services.Helpers;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services.Models.Prompts;

public class DotNetDependencyReviewer : BaseDotNetPrompt
{
    public override Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; } = new();

    public override string Prompt { get; set; } =
        "В соответствии с указанными требованиями проверь проект. Требования: 1. Nuget пакеты, по возможности, должны быть обновлены до последних версий и не иметь уязвимостей. 2. Если транзитивный Nuget пакет имеет уязвимость, значит он должен быть включен в проект и обновлен до актуальной версии. 3. Проект не должен иметь лишних зависимостей или зависимостей, ссылающихся на локальные файлы (для таких зависимостей прописан абсолютный путь).";

    protected override void FillTasks(ZipArchive zipArchive)
    {
        Tasks.Add(nameof(DotNetDependencyReviewer), async taskName =>
        {
            Console.WriteLine($"Задача {taskName} начата");
            try
            {
                var contentJson = DependencyPromptGenerator.GeneratePromptFromZip(zipArchive);
                var result = await new LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(GenerateRequestContent(contentJson));

                Console.WriteLine($"Задача {taskName} успешно окончена");
                return new TaskResult(taskName, result, Tasks[taskName], true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Задача завершилась с ошибкой с ошибкой {taskName} " + e);
                return new TaskResult(taskName, $"# Модуль ревью зависимостей пропущен в связи с недоступностью нейросети\nОшибка: {e.Message}", Tasks[taskName], false);
            }
        });
    }
}