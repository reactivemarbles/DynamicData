﻿using BenchmarkDotNet.Attributes;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using DynamicData.Binding;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class BindAndSortInitial: IDisposable
{
    private readonly Random _random = new();

    private record Item(string Name, int Id, int Ranking);

    private readonly SortExpressionComparer<Item> _comparer = SortExpressionComparer<Item>.Ascending(i => i.Ranking).ThenByAscending(i => i.Name);


    Subject<IChangeSet<Item, int>> _oldSubject = new();
    Subject<IChangeSet<Item, int>> _newSubject = new();

    private IDisposable? _cleanUp;
    private ChangeSet<Item, int>? _changeSet;


    [Params(10, 100, 1_000, 10_000, 50_000)]
    public int Count { get; set; }


    [GlobalSetup]
    public void SetUp()
    {
        _oldSubject = new Subject<IChangeSet<Item, int>>();
        _newSubject = new Subject<IChangeSet<Item, int>>();

       var changeSet = new ChangeSet<Item, int>(Count);
        foreach (var i in Enumerable.Range(1, Count))
        {
            var item = new Item($"Item{i}", i, _random.Next(1, 1000));
            changeSet.Add(new Change<Item, int>(ChangeReason.Add, i, item));
        }

        _changeSet = changeSet;

        _cleanUp = new CompositeDisposable
        (
            _newSubject.BindAndSort(out var list1, _comparer).Subscribe(),
            _oldSubject.Sort(_comparer).Bind(out var list2).Subscribe()
        );
    }


    [Benchmark(Baseline = true)]
    public void Old() => _oldSubject.OnNext(_changeSet!);

    [Benchmark]
    public void New() => _newSubject.OnNext(_changeSet!);

    public void Dispose() => _cleanUp?.Dispose();
}