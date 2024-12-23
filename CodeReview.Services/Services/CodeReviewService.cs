using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CodeReview.Services.Helpers;
using CodeReview.Services.Models.Executors;
using CodeReview.Services.Models.Prompts;
using Microsoft.CodeAnalysis.MSBuild;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace CodeReview.Services.Services;

public class CodeReviewService(IPromptsExecutor<BaseDotNetPrompt> dotNetPromptsExecutor)
{
    private static string ProjectsPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Projects");

    public async Task<string> DotNetReviewStreamZipFile(Stream streamZipFile, int timeout, CancellationToken token)
    {
        var zipArchive = ZipArchive.Open(streamZipFile);
        if (zipArchive == null)
            throw new ArgumentException("Zip file is empty", nameof(streamZipFile));
        var sourceStreamForPrompts = StreamCloner.CloneStream(streamZipFile);
        var streamForSca = StreamCloner.CloneStream(streamZipFile);
        try
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);

            // Создаем комбинированный токен (связываем token и cancellationTokenSource.Token)
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationTokenSource.Token).Token;


            // Особенность используемой библиотеки, не сразу подгружает все файлы в архив
            await Task.Delay(5000, combinedToken);
            var sb = new StringBuilder();
            var sbSca = new StringBuilder();
            await Task.WhenAll([
                // ReSharper disable once MethodSupportsCancellation
                Task.Run(async () => await dotNetPromptsExecutor.ExecutePrompts(sourceStreamForPrompts, sb, combinedToken)),
                Task.Run(async () => await StaticCodeAnalyze(streamForSca, sbSca), combinedToken)
            ]);
            sb.Append(sbSca);

            return sb.ToString();
        }
        finally
        {
            zipArchive.Dispose();
            await sourceStreamForPrompts.DisposeAsync();
            await streamForSca.DisposeAsync();

            var unmanagedPointer = IntPtr.Zero;
            if (unmanagedPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unmanagedPointer);
                Console.WriteLine("Unmanaged memory has been freed.");
            }

            GC.Collect(2, GCCollectionMode.Forced);

            // Стимулируем сборку мусора с оптимизированной сборкой LOH
            GC.Collect(2, GCCollectionMode.Optimized);

            Console.WriteLine("LOH очистился (попытка).");
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
                foreach (var diagnostic in diagnostics) sb.AppendLine("1. " + diagnostic.ToString().Replace(directory + @"\", ""));

                sb.AppendLine("---");
                sb.Append("");
            }
        }

        Console.WriteLine("Генерация SCA успешно окончена");
    }
}