using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData.Binding;
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class Playground: IDisposable
{
    private readonly SourceCache<Person, string> _source = new(p => p.Key);

    public Playground()
    {
        var comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

        ReadOnlyObservableCollection<Person> collection;

        _source.Connect().BindAndSort(
            out collection,
            comparer);

        var array = new List<Person>();

        _source.Connect().BindAndSort(
            out collection,
            comparer);

    }

    public void Dispose() => _source.Dispose();
}
