using System;
using System.Collections.Generic;

using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class TransformManyRefreshFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithFriends> _results;

    private readonly ISourceList<PersonWithFriends> _source;

    public TransformManyRefreshFixture()
    {
        _source = new SourceList<PersonWithFriends>();

        _results = _source.Connect().AutoRefresh().TransformMany(p => p.Friends.RecursiveSelect(r => r.Friends)).AsAggregator();
    }

    [Fact]
    public void AutoRefresh()
    {
        var friend1 = new PersonWithFriends("Friend1", 40);
        var friend2 = new PersonWithFriends("Friend2", 45);

        var person = new PersonWithFriends("Person", 50);
        _source.Add(person);

        person.Friends = new[] { friend1, friend2 };

        _results.Data.Count.Should().Be(2, "Should be 2 in the cache");
        _results.Data.Items.Should().BeEquivalentTo(new[] { friend1, friend2});
    }

    [Fact]
    public void AutoRefreshOnOtherProperty()
    {
        var friend1 = new PersonWithFriends("Friend1", 40);
        var friend2 = new PersonWithFriends("Friend2", 45);
        var friends = new List<PersonWithFriends> { friend1 };
        var person = new PersonWithFriends("Person", 50, friends);
        _source.Add(person);

        friends.Add(friend2);
        person.Age = 55;

        _results.Data.Count.Should().Be(2, "Should be 2 in the cache");
        _results.Data.Items.Should().BeEquivalentTo(new[] { friend1, friend2});
    }

    [Fact]
    public void AutoRefreshRecursive()
    {
        var friend1 = new PersonWithFriends("Friend1", 30);
        var friend2 = new PersonWithFriends("Friend2", 35);
        var friend3 = new PersonWithFriends("Friend3", 40, new[] { friend1 });
        var friend4 = new PersonWithFriends("Friend4", 45, new[] { friend2 });

        var person = new PersonWithFriends("Person", 50, new[] { friend3 });
        _source.Add(person);

        person.Friends = new[] { friend4 };

        _results.Data.Count.Should().Be(2, "Should be 2 in the cache");
        _results.Data.Items.Should().BeEquivalentTo(new[] { friend4, friend2});
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
