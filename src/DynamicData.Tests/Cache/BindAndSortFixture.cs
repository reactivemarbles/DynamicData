using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;


// Bind to a list
public sealed class BindAndSortToList: BindAndSortFixture

{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list  = new List<Person>(100);
        var aggregator = _source.Connect().BindAndSort(list, _comparer).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to an observable collection
public sealed class BindAndSortToObservableCollection : BindAndSortFixture

{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new ObservableCollection<Person>(new List<Person>(100));
        var aggregator = _source.Connect().BindAndSort(list, _comparer).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to a readonly observable collection
public sealed class BindAndSortToReadOnlyObservableCollection: BindAndSortFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var aggregator = _source.Connect().BindAndSort(out var list, _comparer).AsAggregator();

        return (aggregator, list);
    }
}



public abstract class BindAndSortFixture : IDisposable
{

    private readonly RandomPersonGenerator _generator = new();
    private readonly ChangeSetAggregator<Person, string> _results;
    private readonly IList<Person> _boundList;

    protected readonly IComparer<Person> _comparer;
    protected readonly ISourceCache<Person, string> _source;


    public BindAndSortFixture()
    {
        _comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);
        _source = new SourceCache<Person, string>(p => p.Key);

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
    public void AppendAtBeginning()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        // check initial data set is sorted
        _boundList.Count.Should().Be(100);
        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();

        //create age 0 to ensure it is inserted first
        var insert = new Person("_Aaron", 0);
        _source.AddOrUpdate(insert);

        _boundList.Count.Should().Be(101);
        var firstItem = _boundList[0];

        firstItem.Should().Be(insert);
    }

    [Fact]
    public void AppendAtEnd()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toInsert = new Person("zzzzz", 1000);

        _source.AddOrUpdate(toInsert);

        _boundList.Count.Should().Be(101);
        
        var last = _boundList[^1];
        last.Should().Be(toInsert);

    }

    [Fact]
    public void AppendInMiddle()
    {
        _source.AddOrUpdate(Enumerable.Range(0,100).Select(i=> new Person($"P{i}",i)));

        //create age 0 to ensure it is inserted first
        var insert = new Person("Marvin", 50);

        _source.AddOrUpdate(insert);

        _boundList.Count.Should().Be(101);

        var index = _boundList.IndexOf(insert);

        index.Should().Be(50);
    }

    [Fact]
    public void BatchOfVariousChanges()
    {
        var people = Enumerable.Range(0, 100).Select(i => new Person($"P{i}", i)).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = people[3];

        // mixture of add, updates and removed
        _source.Edit(innerCache =>
        {
            innerCache.Remove(people[0].Key);
            innerCache.AddOrUpdate(new Person("Mr", "Z", 50, "M"));
            innerCache.Remove(people[1].Key);
            innerCache.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
            innerCache.Remove(people[7].Key);
        });

        // mirror actions to caches
        var expected = Enumerable.Range(0, 100).Select(i => new Person($"P{i}", i)).ToList();
        expected.RemoveAt(IndexFromKey(people[0].Key));
        expected.RemoveAt(IndexFromKey(people[1].Key));
        expected.Add(new Person("Mr", "Z", 50, "M"));
        expected[IndexFromKey(toupdate.Key)] = new Person(toupdate.Name, toupdate.Age - 24);
        expected.RemoveAt(IndexFromKey(people[7].Key));

        int IndexFromKey(string key) => expected.FindIndex(p => p.Key == key);

        var expectedInOrder = expected.OrderBy(p => p, _comparer).ToList();

        expectedInOrder.SequenceEqual(_boundList).Should().BeTrue();
    }


    [Fact]
    public void BatchOfVariousEndingInClear()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        _source.Edit(updater =>
            {
                updater.Clear();
                updater.AddOrUpdate(_generator.Take(10).ToArray());
                updater.Clear();
            });

        _boundList.Count.Should().Be(0);

    }


    [Fact]
    public void LargeBatchChange()
    {
        // this should produce what are effectively 2 resets for the bound collection
        _source.AddOrUpdate(Enumerable.Range(0, 100).Select(i => new Person($"P{i}", i)));
        _source.AddOrUpdate(Enumerable.Range(100, 100).Select(i => new Person($"P{i}", i)));


        _boundList.Count.Should().Be(200);

    }


    [Fact]
    public void BatchUpdateShiftingIndicies()
    {
        var testData = new[]
        {
            new Person("A", 3),
            new Person("B", 5),
            new Person("C", 7),
            new Person("D", 8),
            new Person("E", 10),
            new Person("F", 12),
            new Person("G", 14)
        };
        _source.AddOrUpdate(testData);

        _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person(testData[0].Name, 6));
                updater.AddOrUpdate(new Person(testData[3].Name, 2));
            });

        var expected = new[]
        {
            new Person("D", 2),
            new Person("B", 5),
            new Person("A", 6),
            new Person("C", 7),
            new Person("E", 10),
            new Person("F", 12),
            new Person("G", 14)
        };

        _boundList.SequenceEqual(expected).Should().BeTrue();
    }



    [Fact]
    public void RemoveFirst()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        _boundList.Count.Should().Be(100);

        _source.Remove(people[0].Key);
        _boundList.Count.Should().Be(99);

        people.RemoveAt(0);

        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();

    }

    [Fact]
    public void RemoveFromEnd()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        _boundList.Count.Should().Be(100);

        _source.Remove(people[99].Key);
        _boundList.Count.Should().Be(99);


        people.RemoveAt(99);
        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();
    }

    [Fact]
    public void RemoveFromMiddle()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        _boundList.Count.Should().Be(100);

        _source.Remove(people[50].Key);
        _boundList.Count.Should().Be(99);


        people.RemoveAt(IndexFromKey(people[50].Key));
        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);

    
        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();
    }


    [Fact]
    public void SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);
      
        _boundList.Count.Should().Be(100);
        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();
    }

    [Fact]
    public void UpdateFirst()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        var toUpdate = _boundList[0];

        var update = new Person(toUpdate.Name, toUpdate.Age + 5);


        _source.AddOrUpdate(new Person(toUpdate.Name, toUpdate.Age + 5));

        people[IndexFromKey(update.Key)] = new Person(toUpdate.Name, toUpdate.Age + 5);
       
        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);


        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();
    }

    [Fact]
    public void UpdateLast()
    {
        //TODO: fixed Text

        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        var toUpdate = _boundList[^1];

        _source.AddOrUpdate(new Person(toUpdate.Name, toUpdate.Age + 5));

        people[IndexFromKey(toUpdate.Key)] = new Person(toUpdate.Name, toUpdate.Age + 5);

        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);

        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();

    }


    [Fact]
    public void UpdateMiddle()
    {
        //TODO: fixed Text

        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        var toUpdate = _boundList[50];

        _source.AddOrUpdate(new Person(toUpdate.Name, toUpdate.Age + 5));

        people[IndexFromKey(toUpdate.Key)] = new Person(toUpdate.Name, toUpdate.Age + 5);

        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);

        people.OrderBy(p => p, _comparer).SequenceEqual(_boundList).Should().BeTrue();

    }




    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

}
