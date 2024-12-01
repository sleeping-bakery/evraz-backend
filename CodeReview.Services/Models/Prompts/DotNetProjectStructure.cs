using CodeReview.Services.Helpers;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services.Models.Prompts;

public class DotNetProjectStructure : BaseDotNetPrompt
{
    public override Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; } = new();

    public override string Prompt { get; set; } =
        "Проверь его структуру (папки и файлы) на соответствие стандартным практикам организации C# проверь его структуру (папки и файлы) на соответствие стандартным практикам организации C# проекта. Обрати внимание на следующие элементы:Главная структура:Есть ли корневая папка, содержащая проекты, например, src или source? В проекте все ли компоненты организованы в отдельные папки с логичными и осмысленными именами? Папка с исходным кодом: Есть ли папка с исходным кодом, например, src, где хранятся все проекты? Если проект многокомпонентный, разделены ли компоненты по папкам? Основные папки: Есть ли следующие папки для организации кода: Models — для классов модели данных. Services — для бизнес-логики или сервисов. Repositories — для работы с данными (например, интерфейсы для взаимодействия с БД). Utilities или Helpers — для вспомогательных классов или методов. Tests — для юнит-тестов и интеграционных тестов. Конфигурационные файлы: Есть ли в проекте стандартные конфигурационные файлы, например, appsettings.json, settings.config или другие? Файлы проекта: Есть ли в каждом проекте файл проекта, например, .csproj? Правильно ли настроены зависимости для каждого компонента? Ресурсы: Если проект использует внешние ресурсы (например, изображения, текстовые файлы), есть ли для них соответствующая папка, например, Resources, Assets или подобная? Структура тестирования: Есть ли отдельная папка для тестов, например, Tests или аналогичная? Если тесты находятся в отдельном проекте, то соответствуют ли имена проекта и папок общим соглашениям? Соглашения по именованию: Проверить, следуют ли папки и файлы стандартным соглашениям по именованию: Папки — PascalCase или camelCase, в зависимости от стандартов команды. Файлы — имена файлов с кодом и тестами должны быть осмысленными и соответствовать их содержимому. Организация по слоям: Если проект использует архитектуру с разделением на слои, например, слои для бизнес-логики, представления и данных, то они четко разделены по папкам? Логирование и исключения: Есть ли папка для логирования, например, Logs, или структура, обеспечивающая обработку ошибок? Дополнительные элементы: Есть ли дополнительные папки или файлы, такие как Documentation, Scripts или Migrations, если они необходимы для данного проекта?";


    protected override void FillTasks(ZipArchive zipArchive)
    {
        Tasks.Add(nameof(DotNetProjectStructure), async taskName =>
        {
            Console.WriteLine($"Задача {taskName} начата");
            try
            {
                var uniqueProjectStructure = ArchiveAnalyzer.GetUniqueArchiveStructure(zipArchive, 3);
                var contentJson = GenerateRequestContent(uniqueProjectStructure);

                var result = await new LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(contentJson);

                Console.WriteLine($"Задача {taskName} успешно окончена");
                return new TaskResult(taskName, result, Tasks[taskName], true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Задача завершилась с ошибкой с ошибкой {taskName} " + e);
                return new TaskResult(taskName, $"# Модуль анализа структуры проекта пропущен в связи с недоступностью нейросети\nОшибка: {e.Message}", Tasks[taskName], false);
            }
        });
    }
}