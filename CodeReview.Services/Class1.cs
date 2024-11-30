using SharpCompress.Common;

namespace CodeReview.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;

public static class Constants
{
    public const string ApiUrl = "http://84.201.152.196:8020/v1/completions";
    public const string ApiKey = "N5b8IWHxnuy5bm53lDRJwLVpcjws6lOt";
}

/// <summary>
/// 1. Получение файлов
/// 2. Получение структуры файлов
/// 3. Ревью структуры файлов
/// </summary>
///
///
///
public class TaskResult(string name, string result, Func<string, Task<TaskResult>> func)
{
    public string Name { get; set; } = name;
    public string Result { get; set; } = result;
    public DateTime TimeEnd { get; set; } = DateTime.Now;

    public Func<string, Task<TaskResult>> Func = func;
}

public interface IPrompt
{
    Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; }

    string Prompt { get; set; }
    void Execute(ZipArchive zipArchive, List<Task<TaskResult>> returnedTasks, CancellationToken token);
}

public abstract class BaseDotNetPrompt : IPrompt
{
    public abstract Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; }

    private static string DefaultSystemPrompt =>
        "Отвечай на русском языке. Давай ответы в формате .md. Ты мастер бот для ревью C# проектов. Не давай комментариев, если не нашел замечаний.";

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

public class DotNetProjectStructure : BaseDotNetPrompt
{
    public override Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; } = new();

    public override string Prompt { get; set; } =
        "Пожалуйста, проверь его структуру (папки и файлы) на соответствие стандартным практикам организации C# проверь его структуру (папки и файлы) на соответствие стандартным практикам организации C# проекта. Обрати внимание на следующие элементы:Главная структура:Есть ли корневая папка, содержащая проекты, например, src или source? В проекте все ли компоненты организованы в отдельные папки с логичными и осмысленными именами? Папка с исходным кодом: Есть ли папка с исходным кодом, например, src, где хранятся все проекты? Если проект многокомпонентный, разделены ли компоненты по папкам? Основные папки: Есть ли следующие папки для организации кода: Models — для классов модели данных. Services — для бизнес-логики или сервисов. Repositories — для работы с данными (например, интерфейсы для взаимодействия с БД). Utilities или Helpers — для вспомогательных классов или методов. Tests — для юнит-тестов и интеграционных тестов. Конфигурационные файлы: Есть ли в проекте стандартные конфигурационные файлы, например, appsettings.json, settings.config или другие? Файлы проекта: Есть ли в каждом проекте файл проекта, например, .csproj? Правильно ли настроены зависимости для каждого компонента? Ресурсы: Если проект использует внешние ресурсы (например, изображения, текстовые файлы), есть ли для них соответствующая папка, например, Resources, Assets или подобная? Структура тестирования: Есть ли отдельная папка для тестов, например, Tests или аналогичная? Если тесты находятся в отдельном проекте, то соответствуют ли имена проекта и папок общим соглашениям? Соглашения по именованию: Проверить, следуют ли папки и файлы стандартным соглашениям по именованию: Папки — PascalCase или camelCase, в зависимости от стандартов команды. Файлы — имена файлов с кодом и тестами должны быть осмысленными и соответствовать их содержимому. Организация по слоям: Если проект использует архитектуру с разделением на слои, например, слои для бизнес-логики, представления и данных, то они четко разделены по папкам? Логирование и исключения: Есть ли папка для логирования, например, Logs, или структура, обеспечивающая обработку ошибок? Дополнительные элементы: Есть ли дополнительные папки или файлы, такие как Documentation, Scripts или Migrations, если они необходимы для данного проекта?";


    protected override void FillTasks(ZipArchive zipArchive)
    {
        Tasks.Add(nameof(DotNetProjectStructure), async taskName =>
        {
            Console.WriteLine($"Задача {taskName} начата");
            try
            {
                var uniqueProjectStructure = DotNetFileReviewer.ArchiveAnalyzer.GetUniqueArchiveStructure(zipArchive, 3);
                var contentJson = GenerateRequestContent(uniqueProjectStructure);

                var result = await new DotNetFileReviewer.LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(contentJson);

                Console.WriteLine($"Задача {taskName} успешно окончена");
                return new TaskResult(taskName, result, Tasks[taskName]);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Задача завершилась с ошибкой с ошибкой {taskName} " + e);
                return new TaskResult(taskName, $"# Модуль анализа структуры проекта пропущен в связи с недоступностью нейросети\nОшибка: {e.Message}", Tasks[taskName]);
            }
        });
    }
}

public class DotNetDependencyReviewer : BaseDotNetPrompt
{
    public override Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; } = new();

    public override string Prompt { get; set; } =
        "В соответствии с указанными требованиями проверь данные, которые тебе отправил пользователь. Требования: 1. Nuget пакеты, по возможности, должны быть обновлены до последних версий и не иметь уязвимостей. 2. Если транзитивный Nuget пакет имеет уязвимость, значит он должен быть включен в проект и обновлен до актуальной версии. 3. Проект не должен иметь лишних зависимостей или зависимостей, ссылающихся на локальные файлы (для таких зависимостей прописан абсолютный путь).";

    protected override void FillTasks(ZipArchive zipArchive)
    {
        Tasks.Add(nameof(DotNetDependencyReviewer), async taskName =>
        {
            Console.WriteLine($"Задача {taskName} начата");
            try
            {
                var contentJson = DependencyPromptGenerator.GeneratePromptFromZip(zipArchive);
                var result = await new DotNetFileReviewer.LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(GenerateRequestContent(contentJson));

                Console.WriteLine($"Задача {taskName} успешно окончена");
                return new TaskResult(taskName, result, Tasks[taskName]);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Задача завершилась с ошибкой с ошибкой {taskName} " + e);
                return new TaskResult(taskName, $"# Модуль ревью зависимостей пропущен в связи с недоступностью нейросети\nОшибка: {e.Message}", Tasks[taskName]);
            }
        });
    }
}

public class DotNetFileReviewer : BaseDotNetPrompt
{
    private static readonly HashSet<string> IgnoredDirectories = new()
    {
        "bin", "obj", "node_modules", "test-results", "packages", "migrations", "Thumbs.db", ".DS_Store"
    };

    public override Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; } = new();

    public override string Prompt { get; set; } =
        "Проверь предоставленный код на следующие пункты:1.Нет неразрешённых TODO,закомментированного или неиспользуемого кода,а также атрибутов Obsolete(удалить,если возможно).Комментарии обязательны для моделей,сущностей и методов.Неиспользуемые переменные и возвраты должны быть устранены.2.Логические ошибки:2.1.Дублирование сообщений при логировании исключений.2.2.Объединение строк через +,вместо использования необработанных строк.2.3.Прямые вычисления вместо методов конвертации.2.4.Лишние проверки для арифметических операций.3.Ошибки архитектуры и дизайна:3.1.Неправильная регистрация HostedService в IoC(должен быть Singleton).3.2.Возврат пустой коллекции вместо null.3.3.Коллекция не должна содержать null элементов.3.4.Проверка аргументов проводится в методе,а не в месте вызова.3.5.При исключении вместо null метод должен бросать исключение.3.6.Использование интерфейсов коллекций внутри метода вместо конкретной реализации.4.Ошибки LINQ:4.1.Использование Skip().Take() вместо Chunk().4.2.Применение Union(), Except(), Intersect(), Distinct(), SequenceEqual() без переопределения Equals() и GetHashCode() или реализации IEquatable<T>.4.3.Лишний вызов Distinct() после Union().4.4.Избыточные вызовы ToArray() или ToList().5.Ошибки Entity Framework:5.1.Использование синхронной материализации вместо асинхронной.5.2.Удаление сущностей в цикле.5.3.Лишняя материализация при удалении элементов.5.4.Частые вызовы SaveChangesAsync() вместо пакетной обработки.5.5.Выполнение фильтрации на стороне приложения вместо базы данных. 5.6.Использование AddAsync() или AddRangeAsync() безqlServerValueGenerationStrategy.SequenceHiLo.6.Общие критерии:6.1.Соответствие кодстайлу и стандартам разработки.6.2.Оптимизация кода под LINQ и Entity Framework.6.3.Исправление распространённых ошибок и обеспечение корректности архитектуры.Предлагай исправления по замечаниям к коду и указывай номер строки файла, где нашел замечания. Если код соответствует пунктам, ИГНОРИРУЙХ ПУНКТЫ";

    protected override void FillTasks(ZipArchive zipArchive)
    {
        var entries = zipArchive.Entries
            .Where(entry => !entry.IsDirectory && entry.Key!.EndsWith(".cs"))
            .ToList();

        foreach (var entry in entries)
        {
            if (IgnoredDirectories.Any(ignoredDirectory => entry.Key!.ToUpper().Contains(ignoredDirectory.ToUpper())))
                continue;

            using var entryStream = entry.OpenEntryStream();
            var seekableStream = DotNetPromptsExecutor.MakeStreamSeekable(entryStream);
            var csFileString = new StreamReader(seekableStream).ReadToEnd();
            
            Tasks.Add(entry.Key!, async taskName =>
            {
                Console.WriteLine($"Задача {taskName} начата");
                try
                {
                    var result = await new LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(GenerateRequestContent(csFileString));
                    Console.WriteLine($"Задача {taskName} успешно окончена");

                    return new TaskResult(taskName, $"# Файл {entry.Key}\n" + result, Tasks[taskName]);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Задача завершилась с ошибкой с ошибкой {taskName} " + e);
                    return new TaskResult(taskName,
                        $"# Файл {entry.Key} был пропущен\nПо причине: {e.Message} Если ошибка гласит о превышении количества токенов, то и без нейросети ответ: разбейте код на несколько модулей и предоставьте их в виде нескольких файлов",
                        Tasks[taskName]);
                }
            });
        }
    }

    public class LlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _authorizationKey;

        public LlmClient(string apiUrl, string authorizationKey)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(60);
            _apiUrl = apiUrl;
            _authorizationKey = authorizationKey;
        }

        public async Task<string> SendRequestAsync(string contentJson)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);

            request.Headers.Add("Authorization", _authorizationKey);
            request.Content = new StringContent(contentJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(await response.Content.ReadAsStringAsync());

            var llmAnswer = JsonSerializer.Deserialize<LlmAnswer>(responseBody);

            if (llmAnswer == null || llmAnswer.Choices.Count == 0)
            {
                throw new InvalidOperationException("Invalid response from LLM API.");
            }

            return System.Text.RegularExpressions.Regex.Unescape(llmAnswer.Choices[0].Message.Content);
        }
    }

    public static class ArchiveAnalyzer
    {
        private static readonly HashSet<string> IgnoredPaths = new()
        {
            "bin", "obj", ".vs", ".vscode", ".idea", "node_modules", "test-results", "packages", "project.lock.json",
            "*.user", "*.suo", "*.userosscache", "*.sln.docstates", "*.dbmdl", "*.bak", "*.log", "*.swp", "Thumbs.db", ".DS_Store"
        };

        private static readonly char[] Separator = { '/', '\\' };


        public static string GetUniqueArchiveStructure(ZipArchive archive, int maxDepth)
        {
            var sb = new StringBuilder();
            var seen = new HashSet<string>();

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var parts = entry.Key?
                    .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
                    .Take(maxDepth);

                if (parts == null) continue;
                var path = string.Join("/", parts);

                if (IgnoredPaths.Any(ignored => path.Contains(ignored, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (seen.Add(path))
                {
                    sb.AppendLine(path);
                }
            }

            return sb.ToString();
        }
    }

    public interface IPromptsExecutor<T> where T : IPrompt
    {
        List<T> Prompts { get; set; }
        Task<StringBuilder> ExecutePrompts(Stream stream, StringBuilder sb, CancellationToken token);
    }


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
            var streams = Prompts.Select(prompt => StreamCloner.CloneStream(stream)).ToList();


            for (var index = 0; index < Prompts.Count; index++)
            {
                Prompts[index].Execute(ZipArchive.Open(streams[index]), returnedTasks, token);
            }

            var uniqueTasks = new Dictionary<string, Task<TaskResult>>();

            while (returnedTasks.Any(returnedTask => !returnedTask.IsCompleted))
            {
                await Task.WhenAll(returnedTasks);

                foreach (var returnedTask in returnedTasks.Where(task => !task.IsCanceled))
                {
                    if (!uniqueTasks.TryGetValue(returnedTask.Result.Name, out var existingTask) ||
                        existingTask.Result.TimeEnd < returnedTask.Result.TimeEnd)
                        uniqueTasks[returnedTask.Result.Name] = returnedTask;
                }

                returnedTasks.AddRange(returnedTasks.Where(returnedTask => returnedTask.IsFaulted)
                    .Select(faultedTask => Task.Run(async () => await faultedTask.Result.Func(faultedTask.Result.Name), token)));
            }

            
            sb.AppendLine($"Дата отчета: {DateTime.Now}");
            foreach (var task in returnedTasks.Where(task => !task.IsCanceled))
            {
                sb.AppendLine(task.Result.Result);
                sb.AppendLine("");
                sb.AppendLine("---");
                sb.AppendLine("");
            }


            return sb;
        }

        public static Stream MakeStreamSeekable(Stream inputStream)
        {
            if (inputStream.CanSeek)
                return inputStream;

            var memoryStream = new MemoryStream();
            inputStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}