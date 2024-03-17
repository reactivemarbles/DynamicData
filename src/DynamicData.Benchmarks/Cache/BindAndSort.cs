using BenchmarkDotNet.Attributes;
using System;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData.Binding;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class BindAndSortInitial: IDisposable
{
    private readonly Random _random = new();

    private record Item(string Name, int Id, int Ranking);

    private readonly SortExpressionComparer<Item> _comparer = SortExpressionComparer<Item>.Ascending(i => i.Ranking).ThenByAscending(i => i.Name);


    private ISourceCache<Item, int> _sourceOld = null!;
    private ISourceCache<Item, int> _sourceNew = null!;

    private IDisposable? _cleanUp;

    private Item[] _items = null!;


    [Params(100, 1_000, 10_000, 50_000)]
    public int Count { get; set; }


    [GlobalSetup]
    public void SetUp()
    {
        _sourceOld = new SourceCache<Item, int>(i => i.Id);
        _sourceNew = new SourceCache<Item, int>(i => i.Id);


        _items = Enumerable.Range(1, Count)
            .Select(i => new Item($"Item{i}", i, _random.Next(1, 1000)))
            .ToArray();

        _cleanUp = new CompositeDisposable
        (
            _sourceNew.Connect().BindAndSort(out var list1, _comparer).Subscribe(),
            _sourceOld.Connect().Sort(_comparer).Bind(out var list2).Subscribe()
        );
    }


    [Benchmark(Baseline = true)]
    public void Old() => _sourceOld.AddOrUpdate(_items);

    [Benchmark]
    public void New() => _sourceNew.AddOrUpdate(_items);

    public void Dispose() => _cleanUp?.Dispose();
}