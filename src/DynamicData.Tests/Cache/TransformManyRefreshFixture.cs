using System;
using System.Collections.Generic;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformManyRefreshFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithFriends, string> _results;

    private readonly ISourceCache<PersonWithFriends, string> _source;

    public TransformManyRefreshFixture()
    {
        _source = new SourceCache<PersonWithFriends, string>(p => p.Key);

        _results = _source.Connect().AutoRefresh().TransformMany(p => p.Friends, p => p.Name).AsAggregator();
    }

    [Fact]
    public void AutoRefresh()
    {
        var person = new PersonWithFriends("Person", 50);
        _source.AddOrUpdate(person);

        person.Friends = new[]
        {
            new PersonWithFriends("Friend1", 40),
            new PersonWithFriends("Friend2", 45)
        };

        _results.Data.Count.Should().Be(2, "Should be 2 in the cache");
        _results.Data.Lookup("Friend1").HasValue.Should().BeTrue();
        _results.Data.Lookup("Friend2").HasValue.Should().BeTrue();
    }

    [Fact]
    public void AutoRefreshOnOtherProperty()
    {
        var friends = new List<PersonWithFriends> { new("Friend1", 40) };
        var person = new PersonWithFriends("Person", 50, friends);
        _source.AddOrUpdate(person);

        friends.Add(new PersonWithFriends("Friend2", 45));
        person.Age = 55;

        _results.Data.Count.Should().Be(2, "Should be 2 in the cache");
        _results.Data.Lookup("Friend1").HasValue.Should().BeTrue();
        _results.Data.Lookup("Friend2").HasValue.Should().BeTrue();
    }

    [Fact]
    public void DirectRefresh()
    {
        var friends = new List<PersonWithFriends> { new("Friend1", 40) };
        var person = new PersonWithFriends("Person", 50, friends);
        _source.AddOrUpdate(person);

        friends.Add(new PersonWithFriends("Friend2", 45));
        _source.Refresh(person);

        _results.Data.Count.Should().Be(2, "Should be 2 in the cache");
        _results.Data.Lookup("Friend1").HasValue.Should().BeTrue();
        _results.Data.Lookup("Friend2").HasValue.Should().BeTrue();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
