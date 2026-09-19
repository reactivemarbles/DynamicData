using DynamicData.Tests.Domain;

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

    [Test]
    public async Task AutoRefresh()
    {
        var person = new PersonWithFriends("Person", 50);
        _source.AddOrUpdate(person);

        person.Friends = new[]
        {
            new PersonWithFriends("Friend1", 40),
            new PersonWithFriends("Friend2", 45)
        };

        await Assert.That(_results.Data.Count).IsEqualTo(2).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Lookup("Friend1").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Friend2").HasValue).IsTrue();
    }

    [Test]
    public async Task AutoRefreshOnOtherProperty()
    {
        var friends = new List<PersonWithFriends> { new("Friend1", 40) };
        var person = new PersonWithFriends("Person", 50, friends);
        _source.AddOrUpdate(person);

        friends.Add(new PersonWithFriends("Friend2", 45));
        person.Age = 55;

        await Assert.That(_results.Data.Count).IsEqualTo(2).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Lookup("Friend1").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Friend2").HasValue).IsTrue();
    }

    [Test]
    public async Task DirectRefresh()
    {
        var friends = new List<PersonWithFriends> { new("Friend1", 40) };
        var person = new PersonWithFriends("Person", 50, friends);
        _source.AddOrUpdate(person);

        friends.Add(new PersonWithFriends("Friend2", 45));
        _source.Refresh(person);

        await Assert.That(_results.Data.Count).IsEqualTo(2).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Lookup("Friend1").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Friend2").HasValue).IsTrue();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
