using System;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace ShimGen.Tests;

internal static class ProcessUtil
{
    public sealed class Result
    {
        public int ExitCode { get; init; }
        public string Stdout { get; init; } = string.Empty;
        public string Stderr { get; init; } = string.Empty;
    }

    public static Result Run(string fileName, string arguments, string? workingDirectory = null, bool echoToProgress = false)
    {
        if (echoToProgress)
        {
            TestContext.Progress.WriteLine($"[proc] start: {fileName} {arguments}");
            if (!string.IsNullOrEmpty(workingDirectory))
                TestContext.Progress.WriteLine($"[proc] cwd: {workingDirectory}");
        }

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = false };
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdout.AppendLine(e.Data);
            if (echoToProgress)
                TestContext.Progress.WriteLine($"[out] {e.Data}");
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderr.AppendLine(e.Data);
            if (echoToProgress)
                TestContext.Progress.WriteLine($"[err] {e.Data}");
        };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        if (echoToProgress)
            TestContext.Progress.WriteLine($"[proc] exit {p.ExitCode}");

        return new Result { ExitCode = p.ExitCode, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
    }
}
