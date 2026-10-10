#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;

using DynamicData.Binding;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

using Randomizer = Bogus.Randomizer;

#endregion

namespace DynamicData.Tests.Cache;

public class RemoveKeyFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Handled with CompositeDisposable")]
    private readonly ISourceCache<Person, string> _source;

    private readonly CompositeDisposable _cleanup = new();

    public RemoveKeyFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
        _cleanup.Add(_source);
    }

    public void Dispose() => _cleanup.Dispose();

    [Fact]
    public void CacheRemoveKey_Add_KeyIsRemoved()
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

        Assert.Equivalent(people, collection);
    }

    [Fact]
    public void CacheRemoveKey_Filter_ItemsFilterKeyIsRemoved()
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

        Assert.Equivalent(people.Where(x => x.Age < average), collection);
    }

    [Fact]
    public void CacheRemoveKey_AutoRefreshUpdateITems_CollectionUpdated()
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

        Assert.Equivalent(people, collection);

        foreach (var person in people)
        {
            person.Age = person.Age + 1;
        }
        Assert.Equivalent(people, collection);
    }

    [Fact]
    public void UnsortedSource_EmitsChangesWithUnspecifiedIndexes()
    {
        // Arrange
        var randomizer = new Randomizer(0x1182);
        var original = new Person(randomizer.Hash(), randomizer.Int());
        var updated = new Person(original.Name, randomizer.Int());

        using var subscription = _source.Connect()
            .RemoveKey()
            .RecordValues(out var results);

        // Act
        _source.AddOrUpdate(original);
        _source.AddOrUpdate(updated);
        _source.Refresh(updated);
        _source.RemoveKey(updated.Key);

        // Assert
        results.Error.Should().BeNull();
        var changes = results.RecordedValues.SelectMany(static changes => changes).ToArray();
        changes.Select(static change => (change.Reason, change.Item.CurrentIndex, change.Item.PreviousIndex)).Should().Equal(
            (ListChangeReason.Add, -1, -1),
            (ListChangeReason.Remove, -1, -1),
            (ListChangeReason.Add, -1, -1),
            (ListChangeReason.Replace, -1, -1),
            (ListChangeReason.Remove, -1, -1));
        changes.Select(static change => change.Item.Current).Should().Equal(new[] { original, original, updated, updated, updated }, ReferenceEquals);
        changes[3].Item.Previous.Value.Should().BeSameAs(updated);
    }
}
