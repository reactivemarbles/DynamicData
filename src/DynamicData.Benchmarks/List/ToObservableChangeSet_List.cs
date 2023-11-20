using System;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;

namespace DynamicData.Benchmarks.List;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[HideColumns(Column.Ratio, Column.RatioSD, Column.AllocRatio)]
public class ToObservableChangeSet_List
{
    [Benchmark]
    [Arguments(0, -1)]
    [Arguments(0, 0)]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(10, -1)]
    [Arguments(10, 1)]
    [Arguments(10, 5)]
    [Arguments(10, 10)]
    [Arguments(100, -1)]
    [Arguments(100, 10)]
    [Arguments(100, 50)]
    [Arguments(100, 100)]
    [Arguments(1_000, -1)]
    [Arguments(1_000, 100)]
    [Arguments(1_000, 500)]
    [Arguments(1_000, 1_000)]
    public void AddsUpdatesAndFinalization(int itemCount, int sizeLimit)
    {
        using var source = new Subject<int>();

        using var subscription = source
            .ToObservableChangeSet(limitSizeTo: sizeLimit)
            .Subscribe();

        var indexModulus = (itemCount / 2) + 1;
        for(var i = 0; i < itemCount; ++i)
            source.OnNext(i);
        source.OnCompleted();
    }
}
