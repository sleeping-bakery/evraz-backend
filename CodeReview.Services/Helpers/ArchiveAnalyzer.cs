using System.Text;
using SharpCompress.Archives.Zip;

namespace CodeReview.Services.Helpers;

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

            if (seen.Add(path)) sb.AppendLine(path);
        }

        return sb.ToString();
    }
}