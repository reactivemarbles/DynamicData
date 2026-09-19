using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;
//TODO: To optimise this, we need to introduce replace range, or specify a buffer

public class FilterOnObservableFixture
{
    [Test]
    public async Task ChangeAValueSoItIsStillInTheFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new PersonObs("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        people[50].SetAge(100);
        await Assert.That(stub.Results.Data.Count).IsEqualTo(82);
        // initial add range, refreshes to filter out < 18 and then no refresh for the no-op filter change
        // stub.Results.Messages.Count should be 102.
    }

    [Test]
    public async Task ChangeAValueToMatchFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new PersonObs("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        people[20].SetAge(10);

        // should have 100-18-1 left
        await Assert.That(stub.Results.Data.Count).IsEqualTo(81);

        // initial addrange, refreshes to filter out < 18 and then refresh for the filter change
        // stub.Results.Messages.Count should be 1 + 18 + 1.
    }

    [Test]
    public async Task ChangeAValueToNoLongerMatchFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new PersonObs("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        // should have 100-18 left
        await Assert.That(stub.Results.Data.Count).IsEqualTo(82);

        // stub.Results.Messages.Count should be 1 + 18.

        people[10].SetAge(20);

        // should have 82+1 left
        await Assert.That(stub.Results.Data.Count).IsEqualTo(83);

        // initial addrange, refreshes to filter out < 18 and then one refresh for the filter change
        // stub.Results.Messages.Count should be 1 + 18 + 1.
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(i => new PersonObs("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);
        stub.Source.Clear();

        await Assert.That(stub.Results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InitialValues()
    {
        var people = Enumerable.Range(1, 100).Select(i => new PersonObs("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        // should have 100-18 left
        await Assert.That(stub.Results.Data.Count).IsEqualTo(82);

        // initial addrange, refreshes to filter out < 18
        // stub.Results.Messages.Count should be 1 + 18.

        await Assert.That(stub.Results.Data.Items).IsEquivalentTo(people.Skip(18));
    }

    [Test]
    public async Task RemoveRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new PersonObs("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);
        stub.Source.RemoveRange(89, 10);

        await Assert.That(stub.Results.Data.Count).IsEqualTo(72);
        // initial addrange, refreshes to filter out < 18 and then removerange
        // stub.Results.Messages.Count should be 1 + 18 + 1.
    }

    private class FilterPropertyStub : IDisposable
    {
        public FilterPropertyStub() => Results = new ChangeSetAggregator<PersonObs>(Source.Connect().FilterOnObservable(p => p.Age.Select(v => v > 18)));

        public ChangeSetAggregator<PersonObs> Results { get; }

        public ISourceList<PersonObs> Source { get; } = new SourceList<PersonObs>();

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
