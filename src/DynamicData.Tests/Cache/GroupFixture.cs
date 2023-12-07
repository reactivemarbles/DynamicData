using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class GroupFixture : IDisposable
{
    private readonly ISourceCache<Person, string> _source;

    private ReadOnlyObservableCollection<GroupViewModel>? _entries;

    public GroupFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Fact]
    public void Kaboom()
    {
        SourceCache<Mytype, int> cache = new(x => x.Key);
        List<Mytype> listWithDuplicates =
        [
            new(1, "G1"),
            new(1, "G2"),
        ];
        cache
            .Connect()
            .Group(x => x.Grouping)
            .Subscribe();

        cache.Edit(x =>
        {
            x.AddOrUpdate(listWithDuplicates);
            x.Clear();
            x.AddOrUpdate(listWithDuplicates);
        });
    }

    class Mytype(int key, string grouping)
    {
        public int Key { get; set; } = key;
        public string Grouping { get; set; } = grouping;

        public override string ToString() => $"{Key}, {Grouping}";
    }

    [Fact]
    public void Add()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(
            updates =>
            {
                updates.Count.Should().Be(1, "Should be 1 add");
                updates.First().Reason.Should().Be(ChangeReason.Add);
                called = true;
            });
        _source.AddOrUpdate(new Person("Person1", 20));

        subscriber.Dispose();
        called.Should().BeTrue();
    }

    [Fact]
    public void AddItemAfterUpdateItemProcessAdd()
    {
        var subscriber = _source.Connect().Group(x => x.Name[0].ToString()).Transform(x => new GroupViewModel(x)).Bind(out _entries).Subscribe();

        _source.Edit(x => x.AddOrUpdate(new Person("Adam", 1)));

        var firstGroup = _entries.First();
        firstGroup.Entries.Count.Should().Be(1);

        _source.Edit(
            x =>
            {
                x.AddOrUpdate(new Person("Adam", 3)); // update
                x.AddOrUpdate(new Person("Alfred", 1)); // add
            });

        firstGroup.Entries.Count.Should().Be(2);

        subscriber.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void FiresCompletedWhenDisposed()
    {
        var completed = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(updates => { }, () => completed = true);
        _source.Dispose();
        subscriber.Dispose();
        completed.Should().BeTrue();
    }

    [Fact]
    public void FiresManyValueForBatchOfDifferentAdds()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(
            updates =>
            {
                updates.Count.Should().Be(4, "Should be 4 adds");
                foreach (var update in updates)
                {
                    update.Reason.Should().Be(ChangeReason.Add);
                }

                called = true;
            });
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
                updater.AddOrUpdate(new Person("Person4", 23));
            });

        subscriber.Dispose();
        called.Should().BeTrue();
    }

    [Fact]
    public void FiresOnlyOnceForABatchOfUniqueValues()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(
            updates =>
            {
                updates.Count.Should().Be(1, "Should be 1 add");
                updates.First().Reason.Should().Be(ChangeReason.Add);
                called = true;
            });
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 20));
                updater.AddOrUpdate(new Person("Person3", 20));
                updater.AddOrUpdate(new Person("Person4", 20));
            });

        subscriber.Dispose();
        called.Should().BeTrue();
    }

    [Fact]
    public void FiresRemoveWhenEmptied()
    {
        var called = false;
        //skip first one a this is setting up the stream
        var subscriber = _source.Connect().Group(p => p.Age).Skip(1).Subscribe(
            updates =>
            {
                updates.Count.Should().Be(1, "Should be 1 update");
                foreach (var update in updates)
                {
                    update.Reason.Should().Be(ChangeReason.Remove);
                }

                called = true;
            });
        var person = new Person("Person1", 20);

        _source.AddOrUpdate(person);

        //remove
        _source.Remove(person);

        subscriber.Dispose();
        called.Should().BeTrue();
    }

    [Fact]
    public void ReceivesUpdateWhenFeederIsInvoked()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(updates => called = true);
        _source.AddOrUpdate(new Person("Person1", 20));
        subscriber.Dispose();
        called.Should().BeTrue();
    }

    [Fact]
    public void Remove()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Skip(1).Subscribe(
            updates =>
            {
                updates.Count.Should().Be(1, "Should be 1 add");
                updates.First().Reason.Should().Be(ChangeReason.Remove);
                called = true;
            });
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.Remove(new Person("Person1", 20));
        subscriber.Dispose();
        called.Should().BeTrue();
    }

    [Fact]
    public void UpdateAnItemWillChangedThegroup()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(updates => called = true);
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.AddOrUpdate(new Person("Person1", 21));
        subscriber.Dispose();
        called.Should().BeTrue();
    }

    [Fact]
    public void UpdateItemAfterAddItemProcessAdd()
    {
        var subscriber = _source.Connect().Group(x => x.Name[0].ToString()).Transform(x => new GroupViewModel(x)).Bind(out _entries).Subscribe();

        _source.Edit(x => x.AddOrUpdate(new Person("Adam", 1)));

        var firstGroup = _entries.First();
        firstGroup.Entries.Count.Should().Be(1);

        _source.Edit(
            x =>
            {
                x.AddOrUpdate(new Person("Alfred", 1)); // add
                x.AddOrUpdate(new Person("Adam", 3)); // update
            });

        firstGroup.Entries.Count.Should().Be(2);

        subscriber.Dispose();
    }

    [Fact]
    public void UpdateNotPossible()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Skip(1).Subscribe(updates => called = true);
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.AddOrUpdate(new Person("Person1", 20));
        subscriber.Dispose();
        called.Should().BeFalse();
    }

    public class GroupEntryViewModel(Person person)
    {
        public Person Person { get; } = person;
    }

    public class GroupViewModel
    {
        private readonly ReadOnlyObservableCollection<GroupEntryViewModel> _entries;

        public GroupViewModel(IGroup<Person, string, string> person) => person?.Cache.Connect().Transform(x => new GroupEntryViewModel(x)).Bind(out _entries).Subscribe();

        public ReadOnlyObservableCollection<GroupEntryViewModel> Entries => _entries;
    }
}
