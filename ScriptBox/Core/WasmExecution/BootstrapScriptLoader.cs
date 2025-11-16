using System.Linq;
using System.Text;

namespace ScriptBox.Core.WasmExecution;

/// <summary>
/// Helper for resolving and loading bootstrap JavaScript files.
/// Shared between the runtime builder and low-level WASM executor so the
/// search semantics stay consistent across the codebase.
/// </summary>
internal static class BootstrapScriptLoader
{
    public static string LoadScripts(IEnumerable<string> scripts)
    {
        if (scripts == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var script in scripts)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                continue;
            }

            builder.AppendLine(LoadScriptFile(script));
        }

        return builder.ToString();
    }

    public static string LoadScriptFile(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new ArgumentException("Bootstrap script path cannot be null or empty", nameof(configuredPath));
        }

        var candidates = new List<string>();

        if (Path.IsPathRooted(configuredPath))
        {
            candidates.Add(configuredPath);
        }
        else
        {
            foreach (var root in EnumerateSearchRoots())
            {
                candidates.Add(Path.Combine(root, configuredPath));
            }
        }

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }
        }

        throw new FileNotFoundException(
            $"Bootstrap script '{configuredPath}' not found. Checked:\n" +
            string.Join("\n", candidates.Select(Path.GetFullPath)));
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        foreach (var root in EnumerateAncestors(AppContext.BaseDirectory, 6))
        {
            yield return root;
        }

        foreach (var root in EnumerateAncestors(Directory.GetCurrentDirectory(), 6))
        {
            yield return root;
        }
    }

    private static IEnumerable<string> EnumerateAncestors(string start, int maxDepth)
    {
        var current = start;
        for (int i = 0; i <= maxDepth && !string.IsNullOrEmpty(current); i++)
        {
            yield return current;
            var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent) || parent == current)
            {
                break;
            }
            current = parent;
        }
    }
}
