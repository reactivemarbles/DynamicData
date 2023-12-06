using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformManyObservableCollectionFixture
{
    [Fact]
    public void FlattenObservableCollection()
    {
        var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        var childIndex = 0;
        var parents = Enumerable.Range(1, 50).Select(
            i =>
            {
                var parent = new Parent(
                    i,
                    new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                childIndex += 2;
                return parent;
            }).ToArray();

        using var source = new SourceCache<Parent, int>(x => x.Id);
        using var aggregator = source.Connect().TransformMany(p => p.Children, c => c.Name).AsAggregator();
        source.AddOrUpdate(parents);

        aggregator.Data.Count.Should().Be(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        aggregator.Data.Count.Should().Be(101);

        ////remove first parent and check children have gone
        source.RemoveKey(1);
        aggregator.Data.Count.Should().Be(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        aggregator.Data.Count.Should().Be(96);
        parents[1].Children.AddRange(childrenInZero);
        aggregator.Data.Count.Should().Be(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        parents[1].Children[0] = new Person("Replacement", 100);
        aggregator.Data.Count.Should().Be(98);

        aggregator.Data.Lookup(replacedChild.Key).HasValue.Should().BeFalse();
        aggregator.Data.Lookup("Replacement").HasValue.Should().BeTrue();
    }

    [Fact]
    public void FlattenReadOnlyObservableCollection()
    {
        var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        var childIndex = 0;
        var parents = Enumerable.Range(1, 50).Select(
            i =>
            {
                var parent = new Parent(
                    i,
                    new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                childIndex += 2;
                return parent;
            }).ToArray();

        using var source = new SourceCache<Parent, int>(x => x.Id);
        using var aggregator = source.Connect().TransformMany(p => p.ChildrenReadonly, c => c.Name).AsAggregator();
        source.AddOrUpdate(parents);

        aggregator.Data.Count.Should().Be(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        aggregator.Data.Count.Should().Be(101);

        ////remove first parent and check children have gone
        source.RemoveKey(1);
        aggregator.Data.Count.Should().Be(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        aggregator.Data.Count.Should().Be(96);
        parents[1].Children.AddRange(childrenInZero);
        aggregator.Data.Count.Should().Be(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        parents[1].Children[0] = new Person("Replacement", 100);
        aggregator.Data.Count.Should().Be(98);

        aggregator.Data.Lookup(replacedChild.Key).HasValue.Should().BeFalse();
        aggregator.Data.Lookup("Replacement").HasValue.Should().BeTrue();
    }

    [Fact]
    public void FlattenObservableCache()
    {
        var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        var childIndex = 0;
        var parents = Enumerable.Range(1, 50).Select(
            i =>
            {
                var parent = new Parent(
                    i,
                    new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                childIndex += 2;
                return parent;
            }).ToArray();

        using var source = new SourceCache<Parent, int>(x => x.Id);
        using var aggregator = source.Connect().TransformMany(p => p.ChildrenCache, c => c.Name).AsAggregator();
        source.AddOrUpdate(parents);

        aggregator.Data.Count.Should().Be(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        aggregator.Data.Count.Should().Be(101);

        ////remove first parent and check children have gone
        source.RemoveKey(1);
        aggregator.Data.Count.Should().Be(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        aggregator.Data.Count.Should().Be(96);
        parents[1].Children.AddRange(childrenInZero);
        aggregator.Data.Count.Should().Be(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        parents[1].Children[0] = new Person("Replacement", 100);
        aggregator.Data.Count.Should().Be(98);

        aggregator.Data.Lookup(replacedChild.Key).HasValue.Should().BeFalse();
        aggregator.Data.Lookup("Replacement").HasValue.Should().BeTrue();
    }

    [Fact]
    public void ObservableCollectionWithoutInitialData()
    {
        using var parents = new SourceCache<Parent, int>(d => d.Id);
        var collection = parents.Connect().TransformMany(d => d.Children, p => p.Name).AsObservableCache();

        var parent = new Parent(1);
        parents.AddOrUpdate(parent);

        collection.Count.Should().Be(0);

        parent.Children.Add(new Person("child1", 1));
        collection.Count.Should().Be(1);

        parent.Children.Add(new Person("child2", 2));
        collection.Count.Should().Be(2);
    }

    [Fact]
    public void ReadOnlyObservableCollectionWithoutInitialData()
    {
        using var parents = new SourceCache<Parent, int>(d => d.Id);
        var collection = parents.Connect().TransformMany(d => d.ChildrenReadonly, p => p.Name).AsObservableCache();

        var parent = new Parent(1);
        parents.AddOrUpdate(parent);

        collection.Count.Should().Be(0);

        parent.Children.Add(new Person("child1", 1));
        collection.Count.Should().Be(1);

        parent.Children.Add(new Person("child2", 2));
        collection.Count.Should().Be(2);
    }

    [Fact]
    public void ObservableCacheWithoutInitialData()
    {
        using var parents = new SourceCache<Parent, int>(d => d.Id);
        var collection = parents.Connect().TransformMany(d => d.ChildrenCache, p => p.Name).AsObservableCache();

        var parent = new Parent(1);
        parents.AddOrUpdate(parent);

        collection.Count.Should().Be(0);

        parent.Children.Add(new Person("child1", 1));
        collection.Count.Should().Be(1);

        parent.Children.Add(new Person("child2", 2));
        collection.Count.Should().Be(2);
    }

    private class Parent
    {
        public Parent(int id, IEnumerable<Person> children)
        {
            Id = id;
            Children = new ObservableCollection<Person>(children);
            ChildrenReadonly = new ReadOnlyObservableCollection<Person>(Children);
            ChildrenCache = Children.ToObservableChangeSet(x => x.Name).AsObservableCache();
        }

        public Parent(int id)
        {
            Id = id;
            Children = new ObservableCollection<Person>();
            ChildrenReadonly = new ReadOnlyObservableCollection<Person>(Children);
            ChildrenCache = Children.ToObservableChangeSet(x => x.Name).AsObservableCache();
        }

        public ObservableCollection<Person> Children { get; }

        public ReadOnlyObservableCollection<Person> ChildrenReadonly { get; }

        public IObservableCache<Person, string> ChildrenCache { get; }

        public int Id { get; }
    }
}
