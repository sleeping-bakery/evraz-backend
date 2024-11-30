using System.IO.Compression;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace CodeReview.Services;

public class CodeReviewService(IPromptsExecutor<BaseDotNetPrompt> dotNetPromptsExecutor)
{
    public async Task<string> DotNetReviewStreamZipFile(Stream streamZipFile)
    {
        using var zipArchive = ZipArchive.Open(streamZipFile);

        // Особенность используемой библиотеки, не сразу подгружает все файлы в архив
        if (zipArchive.Entries.Count < 3)
            await Task.Delay(500);
        var sb = new StringBuilder();

        await dotNetPromptsExecutor.ExecutePrompts(zipArchive, sb);
        await StaticCodeAnalyze(streamZipFile, sb);

        return sb.ToString();
    }

    private static async Task StaticCodeAnalyze(Stream streamZipFile, StringBuilder sb)
    {
        streamZipFile.Position = 0;
        var projectGuid = @"\" + Guid.NewGuid();
        ZipFile.ExtractToDirectory(streamZipFile, Path.Combine(ProjectsPath + projectGuid));
        var slnFiles = Directory.GetFiles(Path.Combine(ProjectsPath + projectGuid), "*.sln", SearchOption.AllDirectories);
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

                // Структура + зависимости
                // foreach (var document in project.Documents)
                // {
                //     var syntaxTree = document.GetSyntaxTreeAsync().Result;
                //     var root = syntaxTree?.GetRoot();
                //
                //     // Ищем зависимости между классами и методами
                //     var classDeclarations = root?.DescendantNodes().OfType<ClassDeclarationSyntax>();
                //     if (classDeclarations == null) continue;
                //     foreach (var classDecl in classDeclarations)
                //     {
                //         Console.Write($"Class:{classDecl.Identifier.Text}");
                //         var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
                //
                //         foreach (var method in methods)
                //         {
                //             var dependencies = GetDependencies(method);
                //             if (dependencies.Count != 0)
                //             {
                //                 Console.Write($"{method.Identifier.Text}-{string.Join(",", dependencies)}".Split('\n')[0] );
                //             }
                //         }
                //     }
                // }
            }
        }
    }

    // private static List<string> GetDependencies(MethodDeclarationSyntax method)
    // {
    //     // Пример поиска зависимостей (поля, методы, другие классы)
    //     var classDeclarations = method.DescendantNodes().OfType<ClassDeclarationSyntax>();
    //     var dependencies = classDeclarations.Select(classDecl => classDecl.Identifier.Text).ToList();
    //
    //     var methodCalls = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
    //     dependencies.AddRange(methodCalls.Select(invocation => invocation.Expression.ToString()));
    //
    //     return dependencies;
    // }

    private static string ProjectsPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Projects");
}