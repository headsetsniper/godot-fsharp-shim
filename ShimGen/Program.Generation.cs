using System.Reflection;
using System.Text;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private readonly record struct GenerationPlan(
        List<string> PlannedWrites,
        List<string> PlannedSkips,
        List<(string from, string to)> PlannedMoves,
        List<string> PlannedDeletes,
        int Scanned,
        int Annotated,
        int Written
    );

    private static (GenerationPlan plan, HashSet<string> liveTypeFullNames) GenerateForTypes(IEnumerable<Type?> types, string outDir, string? fsDir, bool dryRun, bool regenAll, HashSet<string> regenSet, bool skipWrites)
    {
        int scanned = 0, annotated = 0, written = 0;
        var plannedWrites = new List<string>();
        var plannedMoves = new List<(string from, string to)>();
        var plannedDeletes = new List<string>();
        var plannedSkips = new List<string>();
        var seenTypeFullNames = new HashSet<string>(StringComparer.Ordinal);
        var noTouch = dryRun || skipWrites;

        foreach (var type in types)
        {
            if (type is null) continue;
            scanned++;
            var spec = TryCreateSpec(type);
            if (spec is null) continue;
            annotated++;
            seenTypeFullNames.Add(spec.Value.ImplType.FullName!);

            var code = RoslynCodeGenerator.Generate(spec.Value, fsDir);
            var (path, oldPath, relForThis) = ComputeDestination(outDir, fsDir, spec.Value, code, dryRun);

            bool shouldRegen = regenAll || regenSet.Contains(spec.Value.ClassName) || regenSet.Contains(spec.Value.ImplType.FullName ?? string.Empty);
            if (shouldRegen && !string.IsNullOrEmpty(oldPath))
                path = oldPath!;

            var wouldWrite = WouldWrite(path, code);
            if (noTouch)
            {
                if (wouldWrite) plannedWrites.Add(path); else plannedSkips.Add(path);
            }
            else if (shouldRegen)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, code, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                written++;
                LogInfo($"[shimgen] Regenerated (in-place) {path}");
            }
            else if (WriteIfChanged(path, code))
            {
                written++;
                LogInfo($"[shimgen] Wrote {path}");
            }

            if (!string.IsNullOrEmpty(relForThis))
                RemoveOtherGeneratedForSource(outDir, relForThis!, path, noTouch, plannedDeletes);

            if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath!, path) && File.Exists(oldPath!))
            {
                try
                {
                    if (IsGeneratedFile(oldPath!))
                    {
                        if (!shouldRegen)
                        {
                            plannedMoves.Add((oldPath!, path));
                            if (!dryRun && !skipWrites) File.Delete(oldPath!);
                        }
                    }
                }
                catch { }
            }
        }

        var plan = new GenerationPlan(plannedWrites, plannedSkips, plannedMoves, plannedDeletes, scanned, annotated, written);
        return (plan, seenTypeFullNames);
    }

    private static (string path, string? oldPath, string? relForThis) ComputeDestination(string outDir, string? fsDir, ScriptSpec spec, string code, bool dryRun)
    {
        var destDir = outDir;
        string? relForThis = null;
        if (!string.IsNullOrEmpty(fsDir))
        {
            var (rel, _) = TryGetSourceInfo(fsDir!, spec.ImplType);
            relForThis = rel;
            if (!string.IsNullOrEmpty(rel))
            {
                var relDir = Path.GetDirectoryName(rel);
                if (!string.IsNullOrEmpty(relDir)) destDir = Path.Combine(outDir, relDir);
            }
        }
        var path = Path.Combine(destDir, spec.ClassName + ".cs");
        string? newHash = ExtractHash(code);
        string? oldPath = null;
        if (!string.IsNullOrEmpty(fsDir))
        {
            oldPath = FindExistingGeneratedPath(outDir, spec.ClassName, spec.ImplType.FullName, newHash);
            if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath!, path))
            {
                if (!dryRun)
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            }
        }
        return (path, oldPath, relForThis);
    }

    private static void PrintSummary(GenerationPlan plan, bool dryRun, bool skipWrites)
    {
        LogInfo($"[shimgen] Summary: Moves={plan.PlannedMoves.Count}, Deletes={plan.PlannedDeletes.Count}.");
        if (dryRun)
        {
            LogInfo($"[shimgen] Dry-run: Writes={plan.PlannedWrites.Count}, Skipped={plan.PlannedSkips.Count}.");
            foreach (var m in plan.PlannedMoves) LogInfo($"[shimgen] plan MOVE {m.from} -> {m.to}");
            foreach (var d in plan.PlannedDeletes) LogInfo($"[shimgen] plan DELETE {d}");
            foreach (var w in plan.PlannedWrites) LogInfo($"[shimgen] plan WRITE {w}");
        }
        else if (skipWrites)
        {
            LogInfo($"[shimgen] Skip-writes: WouldWrite={plan.PlannedWrites.Count}, Skipped={plan.PlannedSkips.Count}.");
            foreach (var m in plan.PlannedMoves) LogInfo($"[shimgen] plan MOVE {m.from} -> {m.to}");
            foreach (var d in plan.PlannedDeletes) LogInfo($"[shimgen] plan DELETE {d}");
            foreach (var w in plan.PlannedWrites) LogInfo($"[shimgen] plan WRITE {w}");
        }
        LogInfo($"[shimgen] Completed. Scanned={plan.Scanned}, Annotated={plan.Annotated}, Written={plan.Written}.");
    }
}
