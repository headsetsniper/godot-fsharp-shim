namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private static bool IsQuiet()
    {
        var v = Environment.GetEnvironmentVariable("SHIMGEN_QUIET");
        if (string.IsNullOrWhiteSpace(v)) return false;
        v = v.Trim();
        return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase) || v.Equals("quiet", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogInfo(string message)
    {
        if (!IsQuiet()) Console.WriteLine(message);
    }

    private static void LogWarn(string message)
    {
        if (!IsQuiet()) Console.Error.WriteLine(message);
    }
}
