using System.Text;
using System.Xml.Linq;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services;

class DependencyPromptGenerator
{
    public static string GeneratePromptFromZip(ZipArchive archive)
    {
        
        var csprojFiles = archive.Entries
            .Where(entry => entry.Key != null && entry.Key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && !entry.IsDirectory)
            .ToList();

        var packagePropsFiles = archive.Entries
            .Where(entry => entry.Key != null && entry.Key.EndsWith("Directory.Packages.props", StringComparison.OrdinalIgnoreCase) && !entry.IsDirectory)
            .ToList();

        var globalJsonFiles = archive.Entries
            .Where(entry => entry.Key != null && entry.Key.EndsWith("global.json", StringComparison.OrdinalIgnoreCase) && !entry.IsDirectory)
            .ToList();

        var prompt = new StringBuilder();

        foreach (var content in csprojFiles.Select(ExtractFileContent))
        {
            AppendCsprojDependencies(content, prompt);
        }

        foreach (var content in packagePropsFiles.Select(ExtractFileContent))
        {
            AppendPackagePropsDependencies(content, prompt);
        }

        foreach (var content in globalJsonFiles.Select(ExtractFileContent))
        {
            AppendGlobalJsonDependencies(content, prompt);
        }

        return prompt.ToString();
    }

    private static string ExtractFileContent(SharpCompress.Archives.IArchiveEntry entry)
    {
        using var stream = entry.OpenEntryStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void AppendCsprojDependencies(string content, StringBuilder prompt)
    {
        var doc = XDocument.Parse(content);
        var packageReferences = doc.Descendants("PackageReference")
                                   .Select(pr => new
                                   {
                                       Package = pr.Attribute("Include")?.Value,
                                       Version = pr.Attribute("Version")?.Value
                                   })
                                   .Where(pr => pr.Package != null);

        foreach (var reference in packageReferences)
        {
            prompt.AppendLine($"- Пакет: {reference.Package}, Версия: {reference.Version}");
        }

        var hintPaths = doc.Descendants("HintPath")
                           .Select(hp => hp.Value)
                           .Where(hp => !string.IsNullOrWhiteSpace(hp));

        var hintPathsList = hintPaths.ToList();
        if (hintPathsList.Count == 0) return;
        prompt.AppendLine("Ссылки на локальные файлы (HintPaths):");
        foreach (var path in hintPathsList)
        {
            prompt.AppendLine($"  - {path}");
        }
    }

    private static void AppendPackagePropsDependencies(string content, StringBuilder prompt)
    {
        var doc = XDocument.Parse(content);
        var packageVersions = doc.Descendants("PackageVersion")
                                 .Select(pv => new
                                 {
                                     Package = pv.Attribute("Include")?.Value,
                                     Version = pv.Value
                                 })
                                 .Where(pv => pv.Package != null);

        foreach (var package in packageVersions)
        {
            prompt.AppendLine($"- Главный пакет: {package.Package}, Версия: {package.Version}");
        }
    }

    private static void AppendGlobalJsonDependencies(string content, StringBuilder prompt)
    {
        var doc = XDocument.Parse(content);
        var sdkVersion = doc.Descendants("version").FirstOrDefault()?.Value;

        if (!string.IsNullOrWhiteSpace(sdkVersion))
        {
            prompt.AppendLine($"- SDK Версия: {sdkVersion}");
        }
    }
}
