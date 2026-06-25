#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

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
    public void Add()
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
    public void Filter()
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
    public void Refresh()
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
    public void RefreshRemoveKeyFilter()
    {
        var people = _generator.Take(100).ToArray();
        var average = people.Average(x => x.Age);
        ReadOnlyObservableCollection<Person> collection;
        _cleanup.Add(
            _source.Connect()
                .AutoRefresh(x => x.Age)
                .RemoveKey()
                .Filter(x => x.Age < average)
                .Bind(out collection)
                .Subscribe()
        );
        _source.AddOrUpdate(people);

        Assert.Equivalent(people.Where(x => x.Age < average), collection);

        foreach (var person in people)
        {
            person.Age = person.Age + 1;
        }
        Assert.Equivalent(people.Where(x => x.Age < average), collection);
    }

    [Fact]
    public void RefreshFilterRemoveKey()
    {
        var people = _generator.Take(100).ToArray();
        var average = people.Average(x => x.Age);
        ReadOnlyObservableCollection<Person> collection;
        _cleanup.Add(
            _source.Connect()
                .AutoRefresh(x => x.Age)
                .Filter(x => x.Age < average)
                .RemoveKey()
                .Bind(out collection)
                .Subscribe()
        );
        _source.AddOrUpdate(people);

        Assert.Equivalent(people.Where(x => x.Age < average), collection);

        foreach (var person in people)
        {
            person.Age = person.Age + 1;
        }
        Assert.Equivalent(people.Where(x => x.Age < average), collection);
    }
}
