using System;

using DynamicData.Tests.Domain;

using Xunit;

namespace DynamicData.Tests.List;

public class OnItemFixture
{
    [Fact]
    public void OnItemAddCalled()
    {
        var called = false;
        var source = new SourceList<Person>();

        source.Connect().OnItemAdded(_ => called = true).Subscribe();

        var person = new Person("A", 1);

        source.Add(person);
        Assert.True(called);
    }

    [Fact]
    public void OnItemRefreshedCalled()
    {
        var called = false;
        var source = new SourceList<Person>();

        var person = new Person("A", 1);
        source.Add(person);

        source.Connect().AutoRefresh(x=>x.Age).OnItemRefreshed(_ => called = true).Subscribe();

        person.Age += 1;

        Assert.True(called);
    }

    [Fact]
    public void OnItemRemovedCalled()
    {
        var called = false;
        var source = new SourceList<Person>();

        source.Connect().OnItemRemoved(_ => called = true).Subscribe();

        var person = new Person("A", 1);
        source.Add(person);
        source.Remove(person);
        Assert.True(called);
    }
}
