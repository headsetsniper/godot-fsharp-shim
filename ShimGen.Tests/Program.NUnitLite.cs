using System;
using System.Linq;
using NUnit.Common;
using NUnitLite;

namespace ShimGen.Tests
{
    public static class Program
    {
        // Entry point for self-hosted NUnitLite runs when vstest is unavailable (e.g., act containers).
        public static int Main(string[] args)
        {
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
