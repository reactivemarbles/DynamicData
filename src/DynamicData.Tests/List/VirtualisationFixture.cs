using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class VirtualisationFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ReactiveUI.Primitives.Signals.ISignal<VirtualRequest> _requestSubject = new ReactiveUI.Primitives.Signals.StateSignal<VirtualRequest>(new VirtualRequest(0, 25));

    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public VirtualisationFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().Virtualise(_requestSubject).AsAggregator();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
        _requestSubject.Dispose();
    }

    [Test]
    public async Task InsertAfterPageProducesNothing()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var expected = people.Take(25).ToArray();

        _source.InsertRange(_generator.Take(100), 50);
        await Assert.That(_results.Data.Items).IsEquivalentTo(expected);
    }

    [Test]
    public async Task InsertInPageReflectsChange()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var newPerson = new Person("A", 1);
        _source.Insert(10, newPerson);

        var message = _results.Messages[1].ElementAt(0);
        var removedPerson = people.ElementAt(24);

        await Assert.That(_results.Data.Items.ElementAt(10)).IsEqualTo(newPerson);
        await Assert.That(message.Item.Current).IsEqualTo(removedPerson);
        await Assert.That(message.Reason).IsEqualTo(ListChangeReason.Remove);
    }

    [Test]
    public async Task MoveToNextPage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        _requestSubject.OnNext(new VirtualRequest(25, 25));

        var expected = people.Skip(25).Take(25).ToArray();
        await Assert.That(_results.Data.Items).IsEquivalentTo(expected);
    }

    [Test]
    public async Task MoveWithinSamePage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        var personToMove = people[0];
        _source.Move(0, 10);

        var actualPersonAtIndex10 = _results.Data.Items.ElementAt(10);
        await Assert.That(actualPersonAtIndex10).IsEqualTo(personToMove);
    }

    [Test]
    public async Task MoveWithinSamePage2()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        var personToMove = people[10];
        _source.Move(10, 0);

        var actualPersonAtIndex0 = _results.Data.Items.ElementAt(0);
        await Assert.That(actualPersonAtIndex0).IsEqualTo(personToMove);
    }

    [Test]
    public async Task RemoveBeforeShiftsPage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);
        _requestSubject.OnNext(new VirtualRequest(25, 25));
        _source.RemoveAt(0);
        var expected = people.Skip(26).Take(25).ToArray();

        await Assert.That(_results.Data.Items).IsEquivalentTo(expected);

        var removedMessage = _results.Messages[2].ElementAt(0);
        var removedPerson = people.ElementAt(25);
        await Assert.That(removedMessage.Item.Current).IsEqualTo(removedPerson);
        await Assert.That(removedMessage.Reason).IsEqualTo(ListChangeReason.Remove);

        var addedMessage = _results.Messages[2].ElementAt(1);
        var addedPerson = people.ElementAt(50);
        await Assert.That(addedMessage.Item.Current).IsEqualTo(addedPerson);
        await Assert.That(addedMessage.Reason).IsEqualTo(ListChangeReason.Add);
    }

    [Test]
    public async Task VirtualiseInitial()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var expected = people.Take(25).ToArray();

        await Assert.That(_results.Data.Items).IsEquivalentTo(expected);
    }

    [Test]
    public async Task DoesNotThrowWithDuplicates()
    {
        // see https://github.com/reactivemarbles/DynamicData/issues/540

        var result = new List<string>();

        var source = new SourceList<string>();
        source.AddRange(Enumerable.Repeat("item", 10));
        source.Connect()
            .Virtualise(new ReactiveUI.Primitives.Signals.StateSignal<IVirtualRequest>(new VirtualRequest(0, 3)))
            .Clone(result)
            .Subscribe();

        await Assert.That(result.Count).IsEqualTo(1);
    }
}
