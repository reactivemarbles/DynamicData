#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformSafeFixture : IDisposable
{
    private readonly Func<Person, PersonWithGender> _transformFactory = p =>
    {
        if (p.Age % 3 == 0)
        {
            throw new Exception($"Cannot transform {p}");
        }

        var gender = p.Age % 2 == 0 ? "M" : "F";
        return new PersonWithGender(p, gender);
    };

    private readonly IList<Error<Person, string>> _errors;

    private readonly ChangeSetAggregator<PersonWithGender, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public TransformSafeFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
        _errors = new List<Error<Person, string>>();

        var safeTransform = _source.Connect().TransformSafe(_transformFactory, error => _errors.Add(error));
        _results = new ChangeSetAggregator<PersonWithGender, string>(safeTransform);
    }

    [Test]
    public async Task AddWithError()
    {
        var person = new Person("Person", 3);
        _source.AddOrUpdate(person);

        await Assert.That(_errors.Count).IsEqualTo(1).Because("Should be 1 error reported");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 message");
    }

    [Test]
    public async Task AddWithNoError()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(_transformFactory(person)).Because("Should be same person");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task UpdateBatch()
    {
        const string key = "Adult1";
        var update1 = new Person(key, 1);
        var update2 = new Person(key, 2);
        var update3 = new Person(key, 3);

        _source.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(update1);
                innerCache.AddOrUpdate(update2);
                innerCache.AddOrUpdate(update3);
            });

        await Assert.That(_errors.Count).IsEqualTo(1).Because("Should be 1 error reported");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 messages");

        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(_transformFactory(update2)).Because("Change 2 shoud be the only item cached");
    }

    [Test]
    public async Task UpdateBatchAndClear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddOrUpdate(people);
        _source.Clear();

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");

        await Assert.That(_errors.Count).IsEqualTo(33).Because("Should be 33 errors");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(67).Because("Should be 67 add");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(67).Because("Should be 67 removes");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task UpdateSucessively()
    {
        const string key = "Adult1";
        var update1 = new Person(key, 1);
        var update2 = new Person(key, 2);
        var update3 = new Person(key, 3);

        _source.AddOrUpdate(update1);
        _source.AddOrUpdate(update2);
        _source.AddOrUpdate(update3);

        await Assert.That(_errors.Count).IsEqualTo(1).Because("Should be 1 error reported");
        await Assert.That(_results.Messages.Count).IsEqualTo(3).Because("Should be 3 messages");

        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(_transformFactory(update2)).Because("Change 2 shoud be the only item cached");
    }
}
