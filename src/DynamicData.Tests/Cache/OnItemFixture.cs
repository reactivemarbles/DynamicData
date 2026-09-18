#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class OnItemFixture
{
    [Test]
    public async Task OnItemAddCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemAdded(_ => called = true).Subscribe();

        var person = new Person("A", 1);

        source.AddOrUpdate(person);
        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task OnItemRefreshedCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        var person = new Person("A", 1);
        source.AddOrUpdate(person);

        source.Connect().AutoRefresh(x => x.Age).OnItemRefreshed(_ => called = true).Subscribe();

        person.Age += 1;

        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task OnItemRemovedCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemRemoved(_ => called = true).Subscribe();

        var person = new Person("A", 1);
        source.AddOrUpdate(person);
        source.Remove(person);
        await Assert.That(called).IsTrue();
    }

    [Test]
    [Description("Test for https://github.com/reactivemarbles/DynamicData/issues/613")]
    public async Task OnItemRemovedNotCalledForUpdate()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemRemoved(_ => called = true).Subscribe();

        source.AddOrUpdate(new Person("A", 1));
        source.AddOrUpdate(new Person("A", 2));

        await Assert.That(called).IsFalse();
    }

    [Test]
    public async Task OnItemUpdatedCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemUpdated((x, y) => called = true).Subscribe();

        var person = new Person("A", 1);
        source.AddOrUpdate(person);
        var update = new Person("B", 1);
        source.AddOrUpdate(update);
        await Assert.That(called).IsTrue();
    }

    [Test]
    [Description("Test for https://github.com/reactivemarbles/DynamicData/issues/268")]
    public async Task ListAndCacheShouldHaveEquivalentBehaviour()
    {
        var source = new ObservableCollection<Item>
        {
            new() { Id = 1 },
            new() { Id = 2 }
        };

        var list = source.ToObservableChangeSet()
            .Transform(item => new Proxy { Item = item })
            .OnItemAdded(proxy => proxy.Active = true)
            .OnItemRemoved(proxy => proxy.Active = false)
            .Bind(out var listOutput)
            .Subscribe();

        var cache = source.ToObservableChangeSet(item => item.Id)
            .Transform(item => new Proxy { Item = item })
            .OnItemAdded(proxy => proxy.Active = true)
            .OnItemRemoved(proxy => proxy.Active = false)
            .Bind(out var cacheOutput)
            .Subscribe();

        await Assert.That(cacheOutput).IsEquivalentTo(listOutput, ProxyEqualityComparer.Instance);

        list.Dispose();
        cache.Dispose();

        await Assert.That(cacheOutput).IsEquivalentTo(listOutput, ProxyEqualityComparer.Instance);
    }

    public class Item
    {
        public int Id { get; set; }
    }

    public class Proxy
    {
        public Item Item { get; set; }

        public bool? Active { get; set; }

    }

    public class ProxyEqualityComparer : IEqualityComparer<Proxy>
    {
        public static readonly ProxyEqualityComparer Instance = new();

        public bool Equals(Proxy x, Proxy y) => x?.Item.Id == y?.Item.Id && x.Active == y.Active;

        public int GetHashCode(Proxy obj) => HashCode.Combine(obj?.Active, obj.Item);
    }
}
