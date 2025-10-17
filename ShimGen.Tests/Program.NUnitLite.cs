using System;
using System.Linq;
using NUnit.Common;
using NUnitLite;

namespace ShimGen.Tests
{
    public static class Program
    {
        // Self-hosted NUnitLite entry point to avoid vstest handshake issues in constrained CI containers.
        // Build with:  /p:UseNUnitLite=true
        // Run binary:  ./ShimGen.Tests
        public static int Main(string[] args)
        {
            // Default to detailed output and stop on first error only if explicitly requested.
            var nunitArgs = args?.ToList() ?? new System.Collections.Generic.List<string>();
            if (!nunitArgs.Any(a => a.StartsWith("--result", StringComparison.OrdinalIgnoreCase)))
            {
                nunitArgs.Add("--result=TestResult.trx;format=trx");
            }
            if (!nunitArgs.Any(a => a.Equals("--workers", StringComparison.OrdinalIgnoreCase)))
            {
                nunitArgs.Add("--workers=1");
            }

            var writer = new ExtendedTextWrapper(Console.Out);
            return new AutoRun(typeof(Program).Assembly)
                .Execute(nunitArgs.ToArray(), writer, Console.In);
        }
    }
}
