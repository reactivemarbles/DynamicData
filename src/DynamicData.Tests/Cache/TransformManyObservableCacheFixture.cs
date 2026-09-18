#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformManyObservableCollectionFixture
{
    [Test]
    public async Task FlattenObservableCollection()
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

        await Assert.That(aggregator.Data.Count).IsEqualTo(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        await Assert.That(aggregator.Data.Count).IsEqualTo(101);

        ////remove first parent and check children have gone
        source.RemoveKey(1);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        await Assert.That(aggregator.Data.Count).IsEqualTo(96);
        parents[1].Children.AddRange(childrenInZero);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        parents[1].Children[0] = new Person("Replacement", 100);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        await Assert.That(aggregator.Data.Lookup(replacedChild.Key).HasValue).IsFalse();
        await Assert.That(aggregator.Data.Lookup("Replacement").HasValue).IsTrue();
    }

    [Test]
    public async Task FlattenReadOnlyObservableCollection()
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

        await Assert.That(aggregator.Data.Count).IsEqualTo(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        await Assert.That(aggregator.Data.Count).IsEqualTo(101);

        ////remove first parent and check children have gone
        source.RemoveKey(1);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        await Assert.That(aggregator.Data.Count).IsEqualTo(96);
        parents[1].Children.AddRange(childrenInZero);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        parents[1].Children[0] = new Person("Replacement", 100);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        await Assert.That(aggregator.Data.Lookup(replacedChild.Key).HasValue).IsFalse();
        await Assert.That(aggregator.Data.Lookup("Replacement").HasValue).IsTrue();
    }

    [Test]
    public async Task FlattenObservableCache()
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

        await Assert.That(aggregator.Data.Count).IsEqualTo(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        await Assert.That(aggregator.Data.Count).IsEqualTo(101);

        ////remove first parent and check children have gone
        source.RemoveKey(1);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        await Assert.That(aggregator.Data.Count).IsEqualTo(96);
        parents[1].Children.AddRange(childrenInZero);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        parents[1].Children[0] = new Person("Replacement", 100);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        await Assert.That(aggregator.Data.Lookup(replacedChild.Key).HasValue).IsFalse();
        await Assert.That(aggregator.Data.Lookup("Replacement").HasValue).IsTrue();
    }

    [Test]
    public async Task ObservableCollectionWithoutInitialData()
    {
        using var parents = new SourceCache<Parent, int>(d => d.Id);
        var collection = parents.Connect().TransformMany(d => d.Children, p => p.Name).AsObservableCache();

        var parent = new Parent(1);
        parents.AddOrUpdate(parent);

        await Assert.That(collection.Count).IsEqualTo(0);

        parent.Children.Add(new Person("child1", 1));
        await Assert.That(collection.Count).IsEqualTo(1);

        parent.Children.Add(new Person("child2", 2));
        await Assert.That(collection.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReadOnlyObservableCollectionWithoutInitialData()
    {
        using var parents = new SourceCache<Parent, int>(d => d.Id);
        var collection = parents.Connect().TransformMany(d => d.ChildrenReadonly, p => p.Name).AsObservableCache();

        var parent = new Parent(1);
        parents.AddOrUpdate(parent);

        await Assert.That(collection.Count).IsEqualTo(0);

        parent.Children.Add(new Person("child1", 1));
        await Assert.That(collection.Count).IsEqualTo(1);

        parent.Children.Add(new Person("child2", 2));
        await Assert.That(collection.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ObservableCacheWithoutInitialData()
    {
        using var parents = new SourceCache<Parent, int>(d => d.Id);
        var collection = parents.Connect().TransformMany(d => d.ChildrenCache, p => p.Name).AsObservableCache();

        var parent = new Parent(1);
        parents.AddOrUpdate(parent);

        await Assert.That(collection.Count).IsEqualTo(0);

        parent.Children.Add(new Person("child1", 1));
        await Assert.That(collection.Count).IsEqualTo(1);

        parent.Children.Add(new Person("child2", 2));
        await Assert.That(collection.Count).IsEqualTo(2);
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
