namespace DynamicData.Tests.List;

public class SwitchFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<int> _source;

    private readonly ReactiveUI.Primitives.Signals.ISignal<ISourceList<int>> _switchable;

    public SwitchFixture()
    {
        _source = new SourceList<int>();
        _switchable = new ReactiveUI.Primitives.Signals.StateSignal<ISourceList<int>>(_source);
        _results = _switchable.Switch().AsAggregator();
    }

    [Test]
    public async Task ClearsForNewSource()
    {
        var inital = Enumerable.Range(1, 100).ToArray();
        _source.AddRange(inital);

        await Assert.That(_results.Data.Count).IsEqualTo(100);

        var newSource = new SourceList<int>();
        _switchable.OnNext(newSource);

        await Assert.That(_results.Data.Count).IsEqualTo(0);

        newSource.AddRange(inital);
        await Assert.That(_results.Data.Count).IsEqualTo(100);

        var nextUpdates = Enumerable.Range(100, 100).ToArray();
        newSource.AddRange(nextUpdates);
        await Assert.That(_results.Data.Count).IsEqualTo(200);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
        _switchable.Dispose();
    }

    [Test]
    public async Task PoulatesFirstSource()
    {
        var inital = Enumerable.Range(1, 100).ToArray();
        _source.AddRange(inital);

        await Assert.That(_results.Data.Count).IsEqualTo(100);

        await Assert.That(inital).IsEquivalentTo(_source.Items);
    }
}
