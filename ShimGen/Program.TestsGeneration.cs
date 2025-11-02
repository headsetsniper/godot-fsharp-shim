using System.Reflection;
using System.Text;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private static readonly string GdUnit4TestSuiteAttr = "GdUnit4.TestSuiteAttribute";
    private static readonly string GdUnit4BeforeTestAttr = "GdUnit4.BeforeTestAttribute";
    private static readonly string GdUnit4AfterTestAttr = "GdUnit4.AfterTestAttribute";
    private static readonly string GdUnit4TestCaseAttr = "GdUnit4.TestCaseAttribute";

    internal sealed record TestClassSpec(Type ImplType, string ClassName, TestMethodSpec? Before, TestMethodSpec? After, List<TestMethodSpec> Tests);
    internal sealed record TestMethodSpec(string Name, bool ReturnsTask);

    private static (GenerationPlan plan, HashSet<string> liveTypeFullNames) GenerateTestShims(Assembly asm, string outDir, string? fsDir, bool dryRun, bool skipWrites)
    {
        var types = SafeGetTypes(asm);
        var specs = new List<TestClassSpec>();
        foreach (var t in types)
        {
            if (t is null) continue;
            if (IsCompilerGeneratedLike(t)) { LogInfo($"[shimgen][tests][skip-cg] {t.FullName}"); continue; }
            bool suiteCandidate = IsGdUnitTestSuite(t);
            LogInfo($"[shimgen][tests][scan] Type={t.FullName} candidate={suiteCandidate}");
            if (!suiteCandidate) continue;
            var tc = CollectTestClassSpec(t);
            if (tc is not null)
            {
                LogInfo($"[shimgen][tests][accept] {t.FullName} tests={tc.Tests.Count}");
                specs.Add(tc);
            }
            else
            {
                LogInfo($"[shimgen][tests][reject-no-tests] {t.FullName}");
            }
        }
        LogInfo($"[shimgen][tests] Assembly '{asm.GetName().Name}': scanned={types.Count()} suites={specs.Count}");
        foreach (var s in specs)
            LogInfo($"[shimgen][tests] Suite: {s.ImplType.FullName} -> Shim={s.ClassName}, tests={s.Tests.Count}, before={(s.Before != null)}, after={(s.After != null)}");
        if (specs.Count == 0)
            LogInfo("[shimgen][tests] no suites discovered (ensure classes end with 'Tests' or have [TestSuite] attribute)");

        int scanned = types.Count();
        int annotated = specs.Count;
        int written = 0;
        var plannedWrites = new List<string>();
        var plannedMoves = new List<(string from, string to)>();
        var plannedDeletes = new List<string>();
        var plannedSkips = new List<string>();
        var seenTypeFullNames = new HashSet<string>(StringComparer.Ordinal);
        var noTouch = dryRun || skipWrites;

        foreach (var spec in specs)
        {
            seenTypeFullNames.Add(spec.ImplType.FullName ?? spec.ImplType.Name);
            var code = RoslynTestShimGenerator.Generate(spec, fsDir);
            var path = Path.Combine(outDir, spec.ClassName + ".cs");
            var wouldWrite = WouldWrite(path, code);
            if (noTouch)
            {
                if (wouldWrite) plannedWrites.Add(path); else plannedSkips.Add(path);
            }
            else if (WriteIfChanged(path, code))
            {
                written++;
                LogInfo($"[shimgen] Wrote test shim {path}");
            }
        }

        var plan = new GenerationPlan(plannedWrites, plannedSkips, plannedMoves, plannedDeletes, scanned, annotated, written);
        return (plan, seenTypeFullNames);
    }

    private static bool IsGdUnitTestSuite(Type t)
    {
        try
        {
            if (t.GetCustomAttributesData().Any(a => a.AttributeType.FullName == GdUnit4TestSuiteAttr))
                return true;
            if (t.GetCustomAttributesData().Any(a => a.AttributeType.Name == "TestSuiteAttribute"))
                return true;
        }
        catch { /* attribute type resolution may fail; fall through to heuristics */ }
        // Heuristic fallback: treat classes ending with "Tests" in an assembly named *.Tests as suites
        try
        {
            var asmName = t.Assembly.GetName().Name ?? string.Empty;
            if (asmName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) && t.Name.EndsWith("Tests", StringComparison.Ordinal)) return true;
            // Additional heuristic: any public class in *.Tests assembly with at least one public method containing 'Test'
            if (asmName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) && t.IsClass && t.IsPublic)
            {
                var ms = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (ms.Any(m => m.Name.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
                // Attribute name heuristic: method attributes ending with TestCaseAttribute
                foreach (var m in ms)
                {
                    try
                    {
                        if (m.GetCustomAttributesData().Any(a => a.AttributeType.Name == "TestCaseAttribute"))
                            return true;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return false;
    }

    private static TestClassSpec? CollectTestClassSpec(Type t)
    {
        string shimClass = SanitizeTypeIdentifier(t.Name) + "_TestsShim";
        var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        TestMethodSpec? before = null;
        TestMethodSpec? after = null;
        var tests = new List<TestMethodSpec>();
        bool anyAttrResolved = false;
        foreach (var m in methods)
        {
            bool hasAttr(string full)
            {
                try { return m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == full); }
                catch { return false; }
            }
            if (hasAttr(GdUnit4BeforeTestAttr)) { before = new TestMethodSpec(m.Name, ReturnsTask(m)); anyAttrResolved = true; }
            if (hasAttr(GdUnit4AfterTestAttr)) { after = new TestMethodSpec(m.Name, ReturnsTask(m)); anyAttrResolved = true; }
            if (hasAttr(GdUnit4TestCaseAttr)) { tests.Add(new TestMethodSpec(m.Name, ReturnsTask(m))); anyAttrResolved = true; }
        }
        if (tests.Count == 0)
        {
            // Fallback: if we couldn't resolve attributes, generate shims for public instance Task/void methods with no params, excluding Setup/Teardown
            if (!anyAttrResolved)
            {
                foreach (var m in methods.Where(mi => !mi.IsSpecialName && HasZeroOrUnitParam(mi) && (mi.IsPublic || mi.IsFamily || mi.IsFamilyOrAssembly)))
                {
                    var n = m.Name;
                    if (string.Equals(n, "Setup", StringComparison.OrdinalIgnoreCase) || string.Equals(n, "Teardown", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(n, "Setup", StringComparison.OrdinalIgnoreCase)) before ??= new TestMethodSpec(m.Name, ReturnsTask(m));
                        if (string.Equals(n, "Teardown", StringComparison.OrdinalIgnoreCase)) after ??= new TestMethodSpec(m.Name, ReturnsTask(m));
                        continue;
                    }
                    // Treat remaining as test cases
                    if (m.ReturnType == typeof(void) || ReturnsTask(m))
                        tests.Add(new TestMethodSpec(m.Name, ReturnsTask(m)));
                }
            }
        }
        if (tests.Count == 0) return null;
        return new TestClassSpec(t, shimClass, before, after, tests);
    }

    private static bool ReturnsTask(MethodInfo m)
        => m.ReturnType.FullName == "System.Threading.Tasks.Task" ||
           (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition().FullName == "System.Threading.Tasks.Task`1");

    private static bool HasZeroOrUnitParam(MethodInfo m)
    {
        try
        {
            var ps = m.GetParameters();
            if (ps.Length == 0) return true;
            if (ps.Length == 1)
            {
                var p0 = ps[0].ParameterType;
                var fn = p0.FullName ?? string.Empty;
                if (fn == "Microsoft.FSharp.Core.Unit" || fn.EndsWith(".FSharp.Core.Unit", StringComparison.Ordinal))
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static bool IsCompilerGeneratedLike(Type t)
    {
        try
        {
            var n = t.Name;
            if (string.IsNullOrEmpty(n)) return true;
            if (n.StartsWith("<") || n.StartsWith("$")) return true; // F# startup / generated artifacts
            // Ignore nested display classes / closures
            if (n.Contains("@")) return true;
        }
        catch { }
        return false;
    }

    private static string SanitizeTypeIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "TestSuite";
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var ident = new string(chars);
        if (!char.IsLetter(ident[0]) && ident[0] != '_') ident = "_" + ident;
        return ident;
    }
}
