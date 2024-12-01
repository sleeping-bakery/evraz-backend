using System.IO.Compression;
using System.Text;
using CodeReview.Services;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;
using SharpCompress.Common;
using SharpCompress.Writers;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace CodeReview.API.Controllers;

[ApiController]
[Route("[controller]")]
public class ReviewController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile? file, [FromForm] int timeout, CancellationToken token)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Получаем поток из загруженного файла
        await using var stream = file.OpenReadStream();
        var mdString = await new CodeReviewService(new DotNetFileReviewer.DotNetPromptsExecutor()).DotNetReviewStreamZipFile(stream, timeout * 60 * 1000, token);

        var reports = await GetReports(mdString);

        // Возвращаем список файлов как JSON
        return Ok(reports);
    }

    private async Task<List<FileDto>> GetReports(string mdString)
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

    [HttpPost("Multiple")]
    public async Task<IActionResult> UploadFiles(List<IFormFile> file, [FromForm] int timeout, CancellationToken token)
    {
        var memoryStream = new MemoryStream();

        
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Добавляем файлы в архив
            foreach (var formFile in file.Where(f => f.Length > 0))
            {
                // Создаем запись в архиве для каждого файла
                var entry = archive.CreateEntry(formFile.FileName, CompressionLevel.Optimal);

                await using var entryStream = entry.Open();
                await using var fileStream = formFile.OpenReadStream();

                // Копируем данные из файла в запись архива
                await fileStream.CopyToAsync(entryStream, token);
            }
        }
        
        await memoryStream.FlushAsync(token);

        
        var tasks = new List<Task<TaskResult>>();

        // Передаем memoryStream непосредственно в ZipArchive
        new DotNetFileReviewer().Execute(ZipArchive.Open(memoryStream), tasks, token);

        // Ожидаем завершения всех задач
        await Task.WhenAll(tasks);

        // Генерация отчета
        var sb = new StringBuilder();
        DotNetFileReviewer.DotNetPromptsExecutor.GenerateReport(
            sb,
            tasks.Where(task => !task.IsCanceled)
                .ToDictionary(
                    task => task.Result.Name,
                    task => task
                ));

        // Возвращаем результат
        return Ok(await GetReports(sb.ToString()));
    }

    private class FileDto
    {
        public string FileName { get; set; }
        public string FileContentBase64 { get; set; }
    }

    private async Task<MemoryStream> GeneratePdfFromMarkdown(string markdownContent)
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
            return new MemoryStream(Encoding.UTF8.GetBytes("PDF генератор не выдержал такого объема :("));
        }
    }
}