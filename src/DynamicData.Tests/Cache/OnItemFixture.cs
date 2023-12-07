using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public class OnItemFixture
{
    [Fact]
    public void OnItemAddCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemAdded(_ => called = true).Subscribe();

        var person = new Person("A", 1);

        source.AddOrUpdate(person);
        Assert.True(called);
    }

    [Fact]
    public void OnItemRefreshedCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        var person = new Person("A", 1);
        source.AddOrUpdate(person);

        source.Connect().AutoRefresh(x=>x.Age).OnItemRefreshed(_ => called = true).Subscribe();

        person.Age += 1;

        Assert.True(called);
    }

    [Fact]
    public void OnItemRemovedCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemRemoved(_ => called = true).Subscribe();

        var person = new Person("A", 1);
        source.AddOrUpdate(person);
        source.Remove(person);
        Assert.True(called);
    }

    [Fact]
    [Description("Test for https://github.com/reactivemarbles/DynamicData/issues/613")]
    public void OnItemRemovedNotCalledForUpdate()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemRemoved(_ => called = true).Subscribe();

        source.AddOrUpdate(new Person("A", 1));
        source.AddOrUpdate(new Person("A", 2));

        called.Should().Be(false);
    }

    [Fact]
    public void OnItemUpdatedCalled()
    {
        var called = false;
        var source = new SourceCache<Person, int>(x => x.Age);

        source.Connect().OnItemUpdated((x, y) => called = true).Subscribe();

        var person = new Person("A", 1);
        source.AddOrUpdate(person);
        var update = new Person("B", 1);
        source.AddOrUpdate(update);
        Assert.True(called);
    }

    [Fact]
    [Description("Test for https://github.com/reactivemarbles/DynamicData/issues/268")]
    public void ListAndCacheShouldHaveEquivalentBehaviour()
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

        Assert.Equal(listOutput, cacheOutput, new ProxyEqualityComparer());

        list.Dispose();
        cache.Dispose();

        Assert.Equal(listOutput, cacheOutput, new ProxyEqualityComparer());
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
        public bool Equals(Proxy x, Proxy y) => x?.Item.Id == y?.Item.Id && x.Active == y.Active;

        public int GetHashCode(Proxy obj) => HashCode.Combine(obj?.Active, obj.Item);
    }
}
