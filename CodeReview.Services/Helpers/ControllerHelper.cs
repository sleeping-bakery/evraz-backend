using System.IO.Compression;
using System.Text;
using CodeReview.Services.Models;
using CodeReview.Services.Models.Executors;
using CodeReview.Services.Models.Prompts;
using Markdig;
using Microsoft.AspNetCore.Http;
using PuppeteerSharp;

namespace CodeReview.Services.Helpers;

public class ControllerHelper
{
    public static async Task<MemoryStream> GeneratePdfFromMarkdown(string markdownContent)
    {
        // Преобразуем Markdown в HTML
        var htmlContent = Markdown.ToHtml(markdownContent);

        await new BrowserFetcher().DownloadAsync(); // Загружаем нужную версию Chromium

        var launchOptions = new LaunchOptions
        {
            Headless = true, // Убедитесь, что используется headless-режим
            Args = ["--no-sandbox", "--disable-setuid-sandbox"] // Параметры для работы на Linux
        };


        await using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();
        // Устанавливаем HTML-контент на странице
        await page.SetContentAsync(htmlContent);

        // Генерируем PDF и возвращаем в виде потока
        try
        {
            var pdfStream = new MemoryStream(await page.PdfDataAsync());
            return pdfStream;
        }
        catch (Exception e)
        {
            Console.WriteLine("Ошибка при генерации PDF: " + e);
            return new MemoryStream("PDF генератор не выдержал такого объема :("u8.ToArray());
        }
    }

    public static async Task<List<FileDto>> GetReports(string mdString)
    {
        var pdfStream = await GeneratePdfFromMarkdown(mdString);

        // Генерация MD файла (просто передаем как текст)
        var mdStream = new MemoryStream();
        var writer = new StreamWriter(mdStream);
        writer.Write(mdString);
        writer.Flush();
        mdStream.Position = 0;

#if DEBUG
        await using (var fileStream = new FileStream(@"C:\Users\Vadim\Desktop\check.md", FileMode.Create, FileAccess.Write))
        {
            // Копируем данные из MemoryStream в файл
            await mdStream.CopyToAsync(fileStream);
        }

        await using (var fileStream = new FileStream(@"C:\Users\Vadim\Desktop\check.pdf", FileMode.Create, FileAccess.Write))
        {
            // Копируем данные из MemoryStream в файл
            await pdfStream.CopyToAsync(fileStream);
        }
#endif

        // Устанавливаем позиции потоков в начало
        mdStream.Position = 0;
        pdfStream.Position = 0;

        var mdBase64 = Convert.ToBase64String(mdStream.ToArray());
        var pdfBase64 = Convert.ToBase64String(pdfStream.ToArray());

        var files = new List<FileDto>
        {
            new() { FileName = "mdFile.md", FileContentBase64 = mdBase64 },
            new() { FileName = "pdfFile.pdf", FileContentBase64 = pdfBase64 }
        };
        return files;
    }

    public static async Task<StringBuilder> GetReportFromFiles(List<IFormFile> file, int timeout, CancellationToken token)
    {
        var cancellationTokenSource = new CancellationTokenSource(timeout * 60 * 1000);

        // Создаем комбинированный токен (связываем token и cancellationTokenSource.Token)
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationTokenSource.Token).Token;
        var memoryStream = new MemoryStream();

        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Добавляем файлы в архив
            foreach (var formFile in file.Where(f => f.Length > 0))
            {
                // Создаем запись в архиве для каждого файла
                var entry = archive.CreateEntry(formFile.FileName, CompressionLevel.Optimal);

                await using var entryStream = entry.Open();
                await using var fileStream = formFile.OpenReadStream();

                // Копируем данные из файла в запись архива
                await fileStream.CopyToAsync(entryStream, combinedToken);
            }
        }

        await memoryStream.FlushAsync(combinedToken);


        var tasks = new List<Task<TaskResult>>();

        // Передаем memoryStream непосредственно в ZipArchive
        new DotNetFileReviewer().Execute(SharpCompress.Archives.Zip.ZipArchive.Open(memoryStream), tasks, combinedToken);

        // Ожидаем завершения всех задач
        await Task.WhenAll(tasks);

        // Генерация отчета
        var sb = new StringBuilder();
        DotNetPromptsExecutor.GenerateReport(
            sb,
            tasks.Where(task => !task.IsCanceled)
                .ToDictionary(
                    task => task.Result.Name,
                    task => task
                ));
        return sb;
    }
}