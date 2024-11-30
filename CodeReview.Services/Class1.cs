﻿using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services;

/// <summary>
/// 1. Получение файлов
/// 2. Получение структуры файлов
/// 3. Ревью структуры файлов
/// </summary>
///
///
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
public interface IPrompt
{
    string Prompt { get; set; }
    void Execute(ZipArchive zipArchive, List<Task<string>> returnedTasks);
}

public abstract class BaseDotNetPrompt : IPrompt
{
    private string DefaultSystemPrompt =>
        "Отвечай на русском языке. Давай ответы в формате .md. Ты мастер бот для ревью C# проектов. Не давай комментариев, если не нашел замечаний.";

    public abstract string Prompt { get; set; }
    public abstract void Execute(ZipArchive zipArchive, List<Task<string>> returnedTasks);

    protected string GenerateRequestContent(string stringForReview)
    {
        var requestObject = new
        {
            model = "mistral-nemo-instruct-2407",
            messages = new[]
            {
                new { role = "system", content = DefaultSystemPrompt },
                new { role = "user", content = Prompt },
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
    public override string Prompt { get; set; } =
        "Пожалуйста, проверь его структуру (папки и файлы) на соответствие стандартным практикам организации C# проверь его структуру (папки и файлы) на соответствие стандартным практикам организации C# проекта. Обрати внимание на следующие элементы:Главная структура:Есть ли корневая папка, содержащая проекты, например, src или source? В проекте все ли компоненты организованы в отдельные папки с логичными и осмысленными именами? Папка с исходным кодом: Есть ли папка с исходным кодом, например, src, где хранятся все проекты? Если проект многокомпонентный, разделены ли компоненты по папкам? Основные папки: Есть ли следующие папки для организации кода: Models — для классов модели данных. Services — для бизнес-логики или сервисов. Repositories — для работы с данными (например, интерфейсы для взаимодействия с БД). Utilities или Helpers — для вспомогательных классов или методов. Tests — для юнит-тестов и интеграционных тестов. Конфигурационные файлы: Есть ли в проекте стандартные конфигурационные файлы, например, appsettings.json, settings.config или другие? Файлы проекта: Есть ли в каждом проекте файл проекта, например, .csproj? Правильно ли настроены зависимости для каждого компонента? Ресурсы: Если проект использует внешние ресурсы (например, изображения, текстовые файлы), есть ли для них соответствующая папка, например, Resources, Assets или подобная? Структура тестирования: Есть ли отдельная папка для тестов, например, Tests или аналогичная? Если тесты находятся в отдельном проекте, то соответствуют ли имена проекта и папок общим соглашениям? Соглашения по именованию: Проверить, следуют ли папки и файлы стандартным соглашениям по именованию: Папки — PascalCase или camelCase, в зависимости от стандартов команды. Файлы — имена файлов с кодом и тестами должны быть осмысленными и соответствовать их содержимому. Организация по слоям: Если проект использует архитектуру с разделением на слои, например, слои для бизнес-логики, представления и данных, то они четко разделены по папкам? Логирование и исключения: Есть ли папка для логирования, например, Logs, или структура, обеспечивающая обработку ошибок? Дополнительные элементы: Есть ли дополнительные папки или файлы, такие как Documentation, Scripts или Migrations, если они необходимы для данного проекта?";


    public override void Execute(ZipArchive zipArchive, List<Task<string>> returnedTasks)
    {
        returnedTasks.Add(Task.Run(async () =>
        {
            try
            {
                var uniqueProjectStructure = ArchiveAnalyzer.GetUniqueArchiveStructure(zipArchive, 3);
                var contentJson = GenerateRequestContent(uniqueProjectStructure);

                return await new LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(contentJson);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "# Модуль анализа структуры проекта пропущен в связи с недоступностью нейросети";
            }
        }));
    }
}

public class DotNetDependencyReviewer : BaseDotNetPrompt
{
    public override string Prompt { get; set; } =
        "В соответствии с указанными требованиями проверь данные, которые тебе отправил пользователь. Требования: 1. Nuget пакеты, по возможности, должны быть обновлены до последних версий и не иметь уязвимостей. 2. Если транзитивный Nuget пакет имеет уязвимость, значит он должен быть включен в проект и обновлен до актуальной версии. 3. Проект не должен иметь лишних зависимостей или зависимостей, ссылающихся на локальные файлы (для таких зависимостей прописан абсолютный путь).";

    public override void Execute(ZipArchive zipArchive, List<Task<string>> returnedTasks)
    {
        returnedTasks.Add(Task.Run(async () =>
        {
            try
            {
                var contentJson = DependencyPromptGenerator.GeneratePromptFromZip(zipArchive);
                return await new LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(GenerateRequestContent(contentJson));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "# Модуль ревью зависимостей пропущен в связи с недоступностью нейросети";
            }
        }));
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

        response.EnsureSuccessStatusCode();

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
    Task<StringBuilder> ExecutePrompts(ZipArchive archive, StringBuilder sb);
}

public class DotNetPromptsExecutor : IPromptsExecutor<BaseDotNetPrompt>
{
    public List<BaseDotNetPrompt> Prompts { get; set; } = [new DotNetProjectStructure(), new DotNetDependencyReviewer()];

    public async Task<StringBuilder> ExecutePrompts(ZipArchive archive, StringBuilder sb)
    {
        var returnedTasks = new List<Task<string>>();
        foreach (var prompt in Prompts)
        {
            prompt.Execute(archive, returnedTasks);
        }

        await Task.WhenAll(returnedTasks);

        sb.AppendLine($"Дата отчета: {DateTime.Now}");
        foreach (var task in returnedTasks.Where(task => !task.IsFaulted))
        {
            sb.AppendLine(task.Result);
            sb.AppendLine("");
            sb.AppendLine("---");
            sb.AppendLine("");
        }


        return sb;
    }
}