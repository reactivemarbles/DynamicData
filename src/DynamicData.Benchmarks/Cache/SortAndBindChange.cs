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
public class SortAndBindChange: IDisposable
{
    private readonly Random _random = new();
    private record Item(string Name, int Id, int Ranking);

    private readonly SortExpressionComparer<Item> _comparer = SortExpressionComparer<Item>
        .Ascending(i => i.Ranking)
        .ThenByAscending(i => i.Name);

 
    Subject<IChangeSet<Item, int>> _newSubject = new();
    Subject<IChangeSet<Item, int>> _newSubjectOptimised = new();
    Subject<IChangeSet<Item, int>> _oldSubject = new();
    Subject<IChangeSet<Item, int>> _oldSubjectOptimised = new();

    private IDisposable? _cleanUp;

    private ReadOnlyObservableCollection<Item>? _newList;
    private ReadOnlyObservableCollection<Item>? _newListOptimised;
    private ReadOnlyObservableCollection<Item>? _oldList;
    private ReadOnlyObservableCollection<Item>? _oldListOptimised;



    [Params(10, 100, 1_000, 10_000, 50_000)]
    public int Count { get; set; }


    [GlobalSetup]
    public void SetUp()
    {
        _oldSubject = new Subject<IChangeSet<Item, int>>();
        _oldSubjectOptimised = new Subject<IChangeSet<Item, int>>();
        _newSubject = new Subject<IChangeSet<Item, int>>();
        _newSubjectOptimised = new Subject<IChangeSet<Item, int>>();


        _cleanUp = new CompositeDisposable  
        (
            _newSubject.SortAndBind(out var newList, _comparer).Subscribe(),
            _newSubjectOptimised.SortAndBind(out var optimisedList, _comparer, new SortAndBindOptions
            {
                InitialCapacity = Count,
                UseBinarySearch = true
            }).Subscribe(),
           
            _oldSubject.Sort(_comparer).Bind(out var oldList).Subscribe(),
            _oldSubjectOptimised.Sort(_comparer, SortOptimisations.ComparesImmutableValuesOnly).Bind(out var oldOptimisedList).Subscribe()
        );

        _newList = newList;
        _newListOptimised = optimisedList;
        _oldList = oldList;
        _oldListOptimised = oldOptimisedList;



        var changeSet = new ChangeSet<Item, int>(Count);
        foreach (var i in Enumerable.Range(1, Count))
        {
            var item = new Item($"Item{i}", i, _random.Next(1, 1000));
            changeSet.Add(new Change<Item, int>(ChangeReason.Add, i, item));
        }

        _newSubject.OnNext(changeSet);
        _newSubjectOptimised.OnNext(changeSet);
        _oldSubject.OnNext(changeSet);
        _oldSubjectOptimised.OnNext(changeSet);

    }

    [Benchmark(Baseline = true)]
    public void Old() => RunTest(_oldSubject, _oldList!);


    [Benchmark]
    public void OldOptimized() => RunTest(_oldSubjectOptimised, _oldListOptimised!);

    [Benchmark]
    public void New() => RunTest(_newSubject, _newList!);

    [Benchmark]
    public void NewOptimized() => RunTest(_newSubjectOptimised, _newListOptimised!);


    void RunTest(Subject<IChangeSet<Item, int>> subject, ReadOnlyObservableCollection<Item> list)
    {
        var original = list[Count / 2];
        var updated = original with { Ranking = _random.Next(1, 1000) };

        subject.OnNext(new ChangeSet<Item, int>
        {
            new(ChangeReason.Update, original.Id, updated, original)
        });
    }


    public void Dispose() => _cleanUp?.Dispose();
}