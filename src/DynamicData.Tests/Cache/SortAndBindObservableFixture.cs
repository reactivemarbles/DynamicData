using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;


// Bind to a readonly observable collection
public sealed class SortAndBindObservableToReadOnlyObservableCollection : SortAndBindObservableFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var aggregator = Cache.Connect().SortAndBind(out var list, ComparerObservable).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to a list
public sealed class SortAndBindObservableToList : SortAndBindObservableFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new List<Person>(100);
        var aggregator = Cache.Connect().SortAndBind(list, ComparerObservable).AsAggregator();

        return (aggregator, list);
    }
}

public abstract class SortAndBindObservableFixture : IDisposable
{
    protected readonly ISourceCache<Person, string> Cache  = new SourceCache<Person, string>(p => p.Name);


    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person, string> _results;
    private readonly IList<Person> _boundList;
    private readonly SortExpressionComparer<Person> _oldestComparer = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);
    private readonly SortExpressionComparer<Person> _defaultComparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);


    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "By Design.")]
    protected readonly BehaviorSubject<IComparer<Person>> ComparerObservable;


    protected SortAndBindObservableFixture()
    {
        ComparerObservable = new BehaviorSubject<IComparer<Person>>(_defaultComparer);

        // It's ok in this case to call VirtualMemberCallInConstructor

#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        var args = SetUpTests();
#pragma warning restore CA2214

        // bind and sort in one hit

        _results = args.Aggregrator;
        _boundList = args.List;
    }

    protected abstract (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests();


    [Fact]
    public void SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        Cache.AddOrUpdate(people);

        var defaultOrder = people.OrderBy(p => p, _defaultComparer).ToList();
        _boundList.SequenceEqual(defaultOrder).Should().BeTrue();
    }

    
    [Fact]
    public void ChangeSort()
    {
        var people = _generator.Take(100).ToArray();
        Cache.AddOrUpdate(people);

        // change to oldest first sort
        ComparerObservable.OnNext(_oldestComparer);
        
        var oldestFirst = people.OrderBy(p => p, _oldestComparer).ToList();
        _boundList.SequenceEqual(oldestFirst).Should().BeTrue();

        // and back again
        ComparerObservable.OnNext(_defaultComparer);

        var defaultOrder = people.OrderBy(p => p, _defaultComparer).ToList();
        _boundList.SequenceEqual(defaultOrder).Should().BeTrue();
    }


    public void Dispose()
    {
        Cache.Dispose();
        _results.Dispose();
        ComparerObservable.OnCompleted();
    }

}
