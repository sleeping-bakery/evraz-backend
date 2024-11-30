using System.IO.Compression;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace CodeReview.Services;

public class CodeReviewService(DotNetFileReviewer.IPromptsExecutor<BaseDotNetPrompt> dotNetPromptsExecutor)
{
    public async Task<string> DotNetReviewStreamZipFile(Stream streamZipFile, int timeout)
    {
        var zipArchive = ZipArchive.Open(streamZipFile);
        if (zipArchive == null)
            throw new ArgumentException("Zip file is empty", nameof(streamZipFile));

        try
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);

            // Особенность используемой библиотеки, не сразу подгружает все файлы в архив
            await Task.Delay(5000, cancellationTokenSource.Token);
            var sb = new StringBuilder();

            var sourceStreamForPrompts = StreamCloner.CloneStream(streamZipFile);
            var streamForSca = StreamCloner.CloneStream(streamZipFile);
            
            await Task.WhenAll([
                // ReSharper disable once MethodSupportsCancellation
                Task.Run(async () => await dotNetPromptsExecutor.ExecutePrompts(sourceStreamForPrompts, sb, cancellationTokenSource.Token)),
                Task.Run(async () => await StaticCodeAnalyze(streamForSca, sb), cancellationTokenSource.Token)
            ]);

            return sb.ToString();
        }
        finally
        {
            zipArchive?.Dispose();
        }
    }

    private static async Task StaticCodeAnalyze(Stream streamZipFile, StringBuilder sb)
    {
        streamZipFile.Position = 0;
        var projectGuid = Guid.NewGuid().ToString();
        ZipFile.ExtractToDirectory(streamZipFile, Path.Combine(ProjectsPath, projectGuid));
        var slnFiles = Directory.GetFiles(Path.Combine(ProjectsPath, projectGuid), "*.sln", SearchOption.AllDirectories);
        foreach (var slnFile in slnFiles)
        {
            var solutionPath = Path.GetFullPath(slnFile); // Получаем абсолютный путь
            var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            if (!solution.Projects.Any()) continue;
            var directory = new FileInfo(slnFile).Directory!.FullName;

            foreach (var project in solution.Projects)
            {
                //SCA
                sb.AppendLine($"# Анализ проекта {project.Name}");

                var compilation = await project.GetCompilationAsync();
                var diagnostics = compilation!.GetDiagnostics();

                // Выводим диагностические сообщения (ошибки, предупреждения)
                foreach (var diagnostic in diagnostics)
                {
                    sb.AppendLine("1. " + diagnostic.ToString().Replace(directory + @"\", ""));
                }

                sb.AppendLine("---");
                sb.Append("");
            }
        }
        Console.WriteLine("Генерация SCA успешно окончена");
    }

    private static string ProjectsPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Projects");
}