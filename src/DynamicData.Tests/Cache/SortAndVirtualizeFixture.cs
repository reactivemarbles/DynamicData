using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;


public class SortAndVirtualizeFixture : IDisposable
{

    private readonly SourceCache<Person, string> _source = new(p => p.Name);
    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

    private readonly ISubject<IVirtualRequest> _virtualRequests= new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 25));
    private readonly ChangeSetAggregator<Person, string, VirtualContext<Person>> _aggregator;

    public SortAndVirtualizeFixture()
    {
        _aggregator = _source.Connect()
            .SortAndVirtualize(_comparer, _virtualRequests)
            .AsAggregator();
    }


    [Fact]
    public void Initialise()
    {
        var people = Enumerable.Range(1, 1000).Select(i => new Person($"P{i:000}", i)).OrderBy(p=>Guid.NewGuid());
        _source.AddOrUpdate(people);

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);

        actualResult.Should().BeEquivalentTo(expectedResult);


        _virtualRequests.OnNext(new VirtualRequest(25,50));

        expectedResult = people.OrderBy(p => p, _comparer).Skip(25).Take(50).ToList();
         actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    public void Dispose()
    {
        _source.Dispose();
        _aggregator.Dispose();
    }
}
