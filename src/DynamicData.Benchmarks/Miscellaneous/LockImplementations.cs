using System.Threading;
using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Miscellaneous;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class LockImplementations
{
    public LockImplementations()
    {
        _objectGate     = new();
        _threadingGate  = new();
    }

    [Benchmark(Baseline = true)]
    public int NoLock()
    {
        return 0;
    }

    [Benchmark]
    public int ObjectLock()
    {
        lock (_objectGate)
        {
            return 0;
        }
    }

    [Benchmark]
    public int ThreadingLock()
    {
        lock (_threadingGate)
        {
            return 0;
        }
    }

    private readonly object _objectGate;
    private readonly Lock   _threadingGate;
}
