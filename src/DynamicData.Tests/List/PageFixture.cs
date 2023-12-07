using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class PageFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ISubject<PageRequest> _requestSubject = new BehaviorSubject<PageRequest>(new PageRequest(1, 25));

    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public PageFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().Page(_requestSubject).AsAggregator();
    }

    public void Dispose()
    {
        _requestSubject.OnCompleted();
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void InsertAfterPageProducesNothing()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        var expected = people.Take(25).ToArray();

        _source.InsertRange(_generator.Take(100), 50);
        _results.Data.Items.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void InsertInPageReflectsChange()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var newPerson = new Person("A", 1);
        _source.Insert(10, newPerson);

        var message = _results.Messages[1].ElementAt(0);
        var removedPerson = people.ElementAt(24);

        _results.Data.Items.ElementAt(10).Should().Be(newPerson);
        message.Item.Current.Should().Be(removedPerson);
        message.Reason.Should().Be(ListChangeReason.Remove);
    }

    [Fact]
    public void MoveToNextPage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        _requestSubject.OnNext(new PageRequest(2, 25));

        var expected = people.Skip(25).Take(25).ToArray();
        _results.Data.Items.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void MoveWithinSamePage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        var personToMove = people[0];
        _source.Move(0, 10);

        var actualPersonAtIndex10 = _results.Data.Items.ElementAt(10);
        actualPersonAtIndex10.Should().Be(personToMove);
    }

    [Fact]
    public void MoveWithinSamePage2()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        var personToMove = people[10];
        _source.Move(10, 0);

        var actualPersonAtIndex0 = _results.Data.Items.ElementAt(0);
        actualPersonAtIndex0.Should().Be(personToMove);
    }

    [Fact]
    public void RemoveBeforeShiftsPage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        _requestSubject.OnNext(new PageRequest(2, 25));
        _source.RemoveAt(0);
        var expected = people.Skip(26).Take(25).ToArray();

        _results.Data.Items.Should().BeEquivalentTo(expected);

        var removedMessage = _results.Messages[2].ElementAt(0);
        var removedPerson = people.ElementAt(25);
        removedMessage.Item.Current.Should().Be(removedPerson);
        removedMessage.Reason.Should().Be(ListChangeReason.Remove);

        var addedMessage = _results.Messages[2].ElementAt(1);
        var addedPerson = people.ElementAt(50);
        addedMessage.Item.Current.Should().Be(addedPerson);
        addedMessage.Reason.Should().Be(ListChangeReason.Add);
    }

    [Fact]
    public void VirtualiseInitial()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        var expected = people.Take(25).ToArray();
        _results.Data.Items.Should().BeEquivalentTo(expected);
    }
}

public class PageFixtureWithNoInitialData
{
    private readonly Animal[] _items =
    [
        new("Holly", "Cat", AnimalFamily.Mammal),
        new("Rover", "Dog", AnimalFamily.Mammal),
        new("Rex", "Dog", AnimalFamily.Mammal),
        new("Whiskers", "Cat", AnimalFamily.Mammal),
        new("Nemo", "Fish", AnimalFamily.Fish),
        new("Moby Dick", "Whale", AnimalFamily.Mammal),
        new("Fred", "Frog", AnimalFamily.Amphibian),
        new("Isaac", "Next", AnimalFamily.Amphibian),
        new("Sam", "Snake", AnimalFamily.Reptile),
        new("Sharon", "Red Backed Shrike", AnimalFamily.Bird),
    ];

    [Fact]
    public void SimplePaging()
    {
        using var pager = new BehaviorSubject<IPageRequest>(new PageRequest(0, 0));
        using var sourceList = new SourceList<Animal>();
        using var sut = new SimplePaging(sourceList, pager);
        // Add items to source
        sourceList.AddRange(_items);

        sut.Paged.Count.Should().Be(0);

        pager.OnNext(new PageRequest(1, 2));
        sut.Paged.Count.Should().Be(2);

        pager.OnNext(new PageRequest(1, 4));
        sut.Paged.Count.Should().Be(4);

        pager.OnNext(new PageRequest(2, 3));
        sut.Paged.Count.Should().Be(3);
    }


    [Fact]
    public void DoesNotThrowWithDuplicates()
    {
        // see https://github.com/reactivemarbles/DynamicData/issues/540

        var result = new List<string>();

        var source = new SourceList<string>();
        source.AddRange(Enumerable.Repeat("item", 10));
        source.Connect()
            .Page(new BehaviorSubject<IPageRequest>(new PageRequest(0, 3)))
            .Clone(result)
            .Subscribe();

        result.Count.Should().Be(1);
    }
}

public class SimplePaging : AbstractNotifyPropertyChanged, IDisposable
{
    private readonly IDisposable _cleanUp;

    public SimplePaging(IObservableList<Animal> source, IObservable<IPageRequest> pager)
    {
        Paged = source?.Connect()
            .Page(pager)
            .Do(changes => Console.WriteLine(changes.TotalChanges)) //added as a quick and dirty way to debug
            .AsObservableList()!;

        _cleanUp = Paged;
    }

    public IObservableList<Animal> Paged { get; }

    public void Dispose() => _cleanUp.Dispose();
}
