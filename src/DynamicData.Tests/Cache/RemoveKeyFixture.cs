#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;



#endregion

namespace DynamicData.Tests.Cache;

public class RemoveKeyFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ISourceCache<Person, string> _source;

    private readonly CompositeDisposable _cleanup = new();

    public RemoveKeyFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
    }

    public void Dispose()
    {
        _cleanup.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task CacheRemoveKey_Add_KeyIsRemoved()
    {
        ReadOnlyObservableCollection<Person> collection;
        _cleanup.Add(
            _source.Connect()
                .RemoveKey()
                .Bind(out collection)
                .Subscribe()
        );
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(collection).IsEquivalentTo(people);
    }

    [Test]
    public async Task CacheRemoveKey_Filter_ItemsFilterKeyIsRemoved()
    {
        var people = _generator.Take(100).ToArray();
        var average = people.Average(x => x.Age);

        ReadOnlyObservableCollection<Person> collection;
        _cleanup.Add(
            _source.Connect()
                .RemoveKey()
                .Filter(x => x.Age < average)
                .Bind(out collection)
                .Subscribe()
        );
        _source.AddOrUpdate(people);

        await Assert.That(collection).IsEquivalentTo(people.Where(x => x.Age < average));
    }

    [Test]
    public async Task CacheRemoveKey_AutoRefreshUpdateITems_CollectionUpdated()
    {
        ReadOnlyObservableCollection<Person> collection;
        _cleanup.Add(
            _source.Connect()
                .AutoRefresh(x => x.Age)
                .RemoveKey()
                .Bind(out collection)
                .Subscribe()
        );
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(collection).IsEquivalentTo(people);

        foreach (var person in people)
        {
            person.Age = person.Age + 1;
        }
        await Assert.That(collection).IsEquivalentTo(people);
    }

}
