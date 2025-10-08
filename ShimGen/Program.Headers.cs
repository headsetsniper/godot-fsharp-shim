using System.Text;
using System.Text.RegularExpressions;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private static string? ExtractHash(string text)
        => ExtractHeaderValue(text, "SourceHash");

    private static string? ExtractShimGenVersion(string text)
        => ExtractHeaderValue(text, "ShimGenVersion");

    private static string? ExtractSourceFileRel(string text)
        => ExtractHeaderValue(text, "SourceFile");

    private static string? ExtractSourceTypeFullName(string text)
        => ExtractHeaderValue(text, "Source F# type");

    private static string? ExtractHeaderValue(string text, string key)
    {
        try
        {
            foreach (var line in ReadHeaderLines(text))
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("// ")) continue;
                var payload = trimmed.Substring(3);
                if (payload.StartsWith(key + ":", StringComparison.Ordinal))
                {
                    var v = payload.Substring(key.Length + 1).Trim();
                    return v.Length == 0 ? null : v;
                }
            }
        }
        catch { }
        return null;
    }

    private static IEnumerable<string> ReadHeaderLines(string text)
    {
        using var sr = new StringReader(text);
        string? line; int count = 0;
        while ((line = sr.ReadLine()) != null && count < 20)
        {
            yield return line;
            count++;
            if (line.Contains("</auto-generated>", StringComparison.OrdinalIgnoreCase)) break;
        }
    }

    private static bool IsOlderVersion(string? oldV, string? newV)
    {
        if (string.IsNullOrWhiteSpace(newV)) return false;
        var a = ParseVer(oldV ?? "0.0.0");
        var b = ParseVer(newV);
        for (int i = 0; i < 3; i++)
        {
            if (a[i] < b[i]) return true;
            if (a[i] > b[i]) return false;
        }
        return false;
    }

    private static int[] ParseVer(string v)
    {
        try
        {
            var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var a = new int[3];
            for (int i = 0; i < a.Length && i < parts.Length; i++) int.TryParse(parts[i], out a[i]);
            return a;
        }
        catch { return new[] { 0, 0, 0 }; }
    }
}
