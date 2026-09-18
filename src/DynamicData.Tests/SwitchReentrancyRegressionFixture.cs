namespace DynamicData.Tests;

public class SwitchReentrancyRegressionFixture
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CacheSwitchKeepsReentrantReplacementSubscribed(bool replaceDuringClear)
    {
        using var sources = new ReactiveUI.Primitives.Signals.Signal<IObservable<IChangeSet<int, int>>>();
        using var replacement = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int, int>>();
        using var results = sources.Switch()
            .Do(changes =>
            {
                if (changes.Any(change => change.Reason == (replaceDuringClear ? ChangeReason.Remove : ChangeReason.Add) && change.Current == 1))
                {
                    sources.OnNext(replacement);
                }
            })
            .AsAggregator();

        sources.OnNext(Observable.Return<IChangeSet<int, int>>(
            new ChangeSet<int, int> { new(ChangeReason.Add, 1, 1) }));
        if (replaceDuringClear)
        {
            sources.OnNext(Observable.Return<IChangeSet<int, int>>(
                new ChangeSet<int, int> { new(ChangeReason.Add, 99, 99) }));
        }

        replacement.OnNext(new ChangeSet<int, int> { new(ChangeReason.Add, 2, 2) });

        await Assert.That(replacement.HasObservers).IsTrue();
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 2 });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ListSwitchKeepsReentrantReplacementSubscribed(bool replaceDuringClear)
    {
        using var sources = new ReactiveUI.Primitives.Signals.Signal<IObservable<IChangeSet<int>>>();
        using var replacement = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var results = sources.Switch()
            .Do(changes =>
            {
                if (changes.Any(change => replaceDuringClear
                    ? change.Reason == ListChangeReason.Clear && change.Range.Contains(1)
                    : change.Reason == ListChangeReason.Add && change.Item.Current == 1))
                {
                    sources.OnNext(replacement);
                }
            })
            .AsAggregator();

        sources.OnNext(Observable.Return<IChangeSet<int>>(
            new ChangeSet<int> { new(ListChangeReason.Add, 1, 0) }));
        if (replaceDuringClear)
        {
            sources.OnNext(Observable.Return<IChangeSet<int>>(
                new ChangeSet<int> { new(ListChangeReason.Add, 99, 0) }));
        }

        replacement.OnNext(new ChangeSet<int> { new(ListChangeReason.Add, 2, 0) });

        await Assert.That(replacement.HasObservers).IsTrue();
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 2 });
    }
}
