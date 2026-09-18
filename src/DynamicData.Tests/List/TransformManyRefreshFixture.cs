using DynamicData.Tests.Domain;

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

    [Test]
    public async Task AutoRefresh()
    {
        var friend1 = new PersonWithFriends("Friend1", 40);
        var friend2 = new PersonWithFriends("Friend2", 45);

        var person = new PersonWithFriends("Person", 50);
        _source.Add(person);

        person.Friends = new[] { friend1, friend2 };

        await Assert.That(_results.Data.Count).IsEqualTo(2).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { friend1, friend2 });
    }

    [Test]
    public async Task AutoRefreshOnOtherProperty()
    {
        var friend1 = new PersonWithFriends("Friend1", 40);
        var friend2 = new PersonWithFriends("Friend2", 45);
        var friends = new List<PersonWithFriends> { friend1 };
        var person = new PersonWithFriends("Person", 50, friends);
        _source.Add(person);

        friends.Add(friend2);
        person.Age = 55;

        await Assert.That(_results.Data.Count).IsEqualTo(2).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { friend1, friend2 });
    }

    [Test]
    public async Task AutoRefreshRecursive()
    {
        var friend1 = new PersonWithFriends("Friend1", 30);
        var friend2 = new PersonWithFriends("Friend2", 35);
        var friend3 = new PersonWithFriends("Friend3", 40, new[] { friend1 });
        var friend4 = new PersonWithFriends("Friend4", 45, new[] { friend2 });

        var person = new PersonWithFriends("Person", 50, new[] { friend3 });
        _source.Add(person);

        person.Friends = new[] { friend4 };

        await Assert.That(_results.Data.Count).IsEqualTo(2).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { friend4, friend2 });
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
