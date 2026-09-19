// Copyright (c) 2011-2026 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
public class ToCollection
{
    private IChangeSet<int, int>[] _changes = null!;

    [Params(1, 100)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var cache = new ChangeAwareCache<int, int>();
        _changes = new IChangeSet<int, int>[Count];
        for (var i = 0; i < Count; i++)
        {
            cache.AddOrUpdate(i, i);
            _changes[i] = cache.CaptureChanges();
        }
    }

    [Benchmark(Baseline = true)]
    public int QueryProjection()
    {
        using var source = new Signal<IChangeSet<int, int>>();
        var count = 0;
        using var subscription = source
            .QueryWhenChanged(query => new ReadOnlyCollectionLight<int>(query.Items))
            .Subscribe(items => count += items.Count);
        foreach (var change in _changes)
        {
            source.OnNext(change);
        }

        return count;
    }

    [Benchmark]
    public int DirectSnapshot()
    {
        using var source = new Signal<IChangeSet<int, int>>();
        var count = 0;
        using var subscription = source.ToCollection().Subscribe(items => count += items.Count);
        foreach (var change in _changes)
        {
            source.OnNext(change);
        }

        return count;
    }
}
