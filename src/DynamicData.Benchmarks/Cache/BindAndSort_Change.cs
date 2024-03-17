using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using DynamicData.Binding;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class BindAndSortChange: IDisposable
{
    private readonly Random _random = new();
    private record Item(string Name, int Id, int Ranking);

    private readonly SortExpressionComparer<Item> _comparer = SortExpressionComparer<Item>
        .Ascending(i => i.Ranking)
        .ThenByAscending(i => i.Name);

    Subject<IChangeSet<Item, int>> _oldSubject = new();
    Subject<IChangeSet<Item, int>> _newSubject = new();

    private IDisposable? _cleanUp;

    private ReadOnlyObservableCollection<Item>? _newList;
    private ReadOnlyObservableCollection<Item>? _oldList;


    [Params(10, 100, 1_000, 10_000, 50_000)]
    public int Count { get; set; }


    [GlobalSetup]
    public void SetUp()
    {
        _oldSubject = new Subject<IChangeSet<Item, int>>();
        _newSubject = new Subject<IChangeSet<Item, int>>();

        var options = BindAndSortOptions.Default with
        {
            InitialCapacity = Count,
            UseBinarySearch = true
        };

        _cleanUp = new CompositeDisposable
        (
            _newSubject.BindAndSort(out var list1, _comparer).Subscribe(),
            _oldSubject.Sort(_comparer).Bind(out var list2).Subscribe()
        );

        _newList = list1;
        _oldList = list2;



        var changeSet = new ChangeSet<Item, int>(Count);
        foreach (var i in Enumerable.Range(1, Count))
        {
            var item = new Item($"Item{i}", i, _random.Next(1, 1000));
            changeSet.Add(new Change<Item, int>(ChangeReason.Add, i, item));
        }

        _newSubject.OnNext(changeSet);
        _oldSubject.OnNext(changeSet);

    }

    [Benchmark(Baseline = true)]
    public void Old()
    {
        var original = _oldList![Count / 2];
        var updated = original with { Ranking = _random.Next(1, 1000) };

        _oldSubject.OnNext(new ChangeSet<Item, int>
        {
            new(ChangeReason.Update, original.Id, updated, original)
        });
    }

    [Benchmark]
    public void New()
    {
        var original = _newList![Count / 2];
        var updated = original with { Ranking = _random.Next(1, 1000) };

        _newSubject.OnNext(new ChangeSet<Item, int>
        {
            new(ChangeReason.Update, original.Id, updated, original)
        });
    }

    public void Dispose() => _cleanUp?.Dispose();
}