using System;
using System.Collections.Generic;
using System.Linq;

using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformSafeParallelFixture : IDisposable
{
    private readonly IList<Error<Person, string>> _errors;

    private readonly ChangeSetAggregator<PersonWithGender, string> _results;

    private readonly ISourceCache<Person, string> _source;

    private readonly Func<Person, PersonWithGender> _transformFactory = p =>
    {
        if (p.Age % 3 == 0)
        {
            throw new Exception($"Cannot transform {p}");
        }

        var gender = p.Age % 2 == 0 ? "M" : "F";
        return new PersonWithGender(p, gender);
    };

    public TransformSafeParallelFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _errors = new List<Error<Person, string>>();

        var safeTransform = _source.Connect().TransformSafe(_transformFactory, error => _errors.Add(error));
        _results = new ChangeSetAggregator<PersonWithGender, string>(safeTransform);
    }

    [Fact]
    public void AddWithError()
    {
        var person = new Person("Person", 3);
        _source.AddOrUpdate(person);

        _errors.Count.Should().Be(1, "Should be 1 error reported");
        _results.Messages.Count.Should().Be(1, "Should be 1 messages");
    }

    [Fact]
    public void AddWithNoError()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(_transformFactory(person), "Should be same person");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void UpdateBatch()
    {
        const string key = "Adult1";
        var update1 = new Person(key, 1);
        var update2 = new Person(key, 2);
        var update3 = new Person(key, 3);

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(update1);
                updater.AddOrUpdate(update2);
                updater.AddOrUpdate(update3);
            });

        _errors.Count.Should().Be(1, "Should be 1 error reported");
        _results.Messages.Count.Should().Be(1, "Should be 1 messages");

        _results.Data.Count.Should().Be(1, "Should 1 item in the cache");
        _results.Data.Items[0].Should().Be(_transformFactory(update2), "Change 2 shoud be the only item cached");
    }

    [Fact]
    public void UpdateBatchAndClear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddOrUpdate(people);
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");

        _errors.Count.Should().Be(33, "Should be 33 errors");
        _results.Messages[0].Adds.Should().Be(67, "Should be 67 add");
        _results.Messages[1].Removes.Should().Be(67, "Should be 67 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void UpdateSucessively()
    {
        const string key = "Adult1";
        var update1 = new Person(key, 1);
        var update2 = new Person(key, 2);
        var update3 = new Person(key, 3);

        _source.AddOrUpdate(update1);
        _source.AddOrUpdate(update2);
        _source.AddOrUpdate(update3);

        _errors.Count.Should().Be(1, "Should be 1 error reported");
        _results.Messages.Count.Should().Be(3, "Should be 3 messages");

        _results.Data.Count.Should().Be(1, "Should 1 item in the cache");
        _results.Data.Items[0].Should().Be(_transformFactory(update2), "Change 2 shoud be the only item cached");
    }
}
