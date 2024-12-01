using CodeReview.Services.Helpers;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services.Models.Prompts;

public class DotNetFileReviewer : BaseDotNetPrompt
{
    private static readonly HashSet<string> IgnoredDirectories = new()
    {
        "bin", "obj", "node_modules", "test-results", "packages", "migrations", "Thumbs.db", ".DS_Store"
    };

    public override Dictionary<string, Func<string, Task<TaskResult>>> Tasks { get; set; } = new();

    public override string Prompt { get; set; } =
        "Если код соответствует пунктам, ИГНОРИРУЙ. Предлагай исправления и указывай номер строки файла, где нашел замечания.Проверь предоставленный код на следующие пункты:1.Нет неразрешённых TODO, не должно быть var, закомментированного или неиспользуемого кода,а также атрибутов Obsolete(удалить,если возможно).Комментарии обязательны для моделей,сущностей и методов.Неиспользуемые переменные и возвраты должны быть устранены.2.Логические ошибки:2.1.Дублирование сообщений при логировании исключений.2.2.Объединение строк через +,вместо использования необработанных строк.2.3.Прямые вычисления вместо методов конвертации.2.4.Лишние проверки для арифметических операций.3.Ошибки архитектуры и дизайна:3.1.Неправильная регистрация HostedService в IoC(должен быть Singleton).3.2.Возврат пустой коллекции вместо null.3.3.Коллекция не должна содержать null элементов.3.4.Проверка аргументов проводится в методе,а не в месте вызова.3.5.При исключении вместо null метод должен бросать исключение.3.6.Использование интерфейсов коллекций внутри метода вместо конкретной реализации.4.Ошибки LINQ:4.1.Использование Skip().Take() вместо Chunk().4.2.Применение Union(), Except(), Intersect(), Distinct(), SequenceEqual() без переопределения Equals() и GetHashCode() или реализации IEquatable<T>.4.3.Лишний вызов Distinct() после Union().4.4.Избыточные вызовы ToArray() или ToList().5.Ошибки Entity Framework:5.1.Использование синхронной материализации вместо асинхронной.5.2.Удаление сущностей в цикле.5.3.Лишняя материализация при удалении элементов.5.4.Частые вызовы SaveChangesAsync() вместо пакетной обработки.5.5.Выполнение фильтрации на стороне приложения вместо базы данных. 5.6.Использование AddAsync() или AddRangeAsync() безqlServerValueGenerationStrategy.SequenceHiLo.6.Общие критерии:6.1.Соответствие кодстайлу и стандартам разработки.6.2.Оптимизация кода под LINQ и Entity Framework.6.3.Исправление распространённых ошибок и обеспечение корректности архитектуры.";

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
            var csFileString = new StreamReader(entryStream).ReadToEnd();

            Tasks.Add(entry.Key!, async taskName =>
            {
                Console.WriteLine($"Задача {taskName} начата");
                try
                {
                    var result = await new LlmClient(Constants.ApiUrl, Constants.ApiKey).SendRequestAsync(GenerateRequestContent(csFileString));
                    Console.WriteLine($"Задача {taskName} успешно окончена {DateTime.Now}");

                    return new TaskResult(taskName, $"# Файл {entry.Key}\n" + result, Tasks[taskName], true);
                }
                catch (Exception e)
                {
                    var defaultSuccess = false;

                    if (e.Message.Contains("max_tokens"))
                        defaultSuccess = true;
                    Console.WriteLine($"Задача завершилась с ошибкой с ошибкой {taskName} " + e);
                    return new TaskResult(taskName,
                        $"# Файл {entry.Key} был пропущен\nПо причине: {e.Message} Если ошибка гласит о превышении количества токенов, то и без нейросети ответ: разбейте код на несколько модулей и предоставьте их в виде нескольких файлов",
                        Tasks[taskName], defaultSuccess);
                }
            });
        }
    }
}