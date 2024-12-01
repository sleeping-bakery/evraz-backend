using CodeReview.Services.Helpers;
using CodeReview.Services.Models.Executors;
using CodeReview.Services.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.API.Controllers;

/// <summary>
///     Рефакторил на скорую руку, нормальную организацию и код можете посмотреть в репозиториях организации)
/// </summary>
[ApiController]
[Route("[controller]")]
public class ReviewController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile? file, [FromForm] int timeout, CancellationToken token)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        // Получаем поток из загруженного файла
        await using var stream = file.OpenReadStream();
        var mdString = await new CodeReviewService(new DotNetPromptsExecutor()).DotNetReviewStreamZipFile(stream, timeout * 60 * 1000, token);

        var reports = await ControllerHelper.GetReports(mdString);

        // Возвращаем список файлов как JSON
        return Ok(reports);
    }


    [HttpPost("Multiple")]
    public async Task<IActionResult> UploadFiles(List<IFormFile> file, [FromForm] int timeout, CancellationToken token)
    {
        var sb = await ControllerHelper.GetReportFromFiles(file, timeout, token);

        // Возвращаем результат
        return Ok(await ControllerHelper.GetReports(sb.ToString()));
    }
}