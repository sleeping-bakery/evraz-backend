using CodeReview.Services;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;

namespace CodeReview.API.Controllers;

[ApiController]
[Route("[controller]")]
public class ReviewController : ControllerBase
{

    [HttpPost]
    public async Task<ActionResult> UploadFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Получаем поток из загруженного файла
        await using var stream = file.OpenReadStream();
        var a = new CodeReviewService(new DotNetPromptsExecutor());
        var mdString =  await a.DotNetReviewStreamZipFile(stream);
            
            
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
        
        mdStream.Position = 0;
        pdfStream.Position = 0;
        
        // Возвращаем оба файла в ответе
        var result = new MultipartFormDataContent
        {
            { new StreamContent(mdStream), "mdFile", "mdFile.md" },
            { new StreamContent(pdfStream), "pdfFile", "pdfFile.pdf" }
        };

        return Ok(result);
    }
    
    private async  Task<MemoryStream> GeneratePdfFromMarkdown(string markdownContent)
    {
        // Преобразуем Markdown в HTML
        var htmlContent = Markdown.ToHtml(markdownContent);

        await new BrowserFetcher().DownloadAsync();  // Загружаем нужную версию Chromium

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
        var pdfStream = new MemoryStream(await page.PdfDataAsync());
        return pdfStream;
    }
}