namespace DynamicData.Tests.List;

public class MergeManyChangeSetsFixture
{
    [Test]
    public async Task MergeManyShouldWork()
    {
        var a = new SourceList<int>();
        var b = new SourceList<int>();
        var c = new SourceList<int>();

        var parent = new SourceList<SourceList<int>>();
        parent.Add(a);
        parent.Add(b);
        parent.Add(c);

        var d = parent.Connect().MergeMany(e => e.Connect().RemoveIndex()).AsObservableList();

        await Assert.That(d.Count).IsEqualTo(0);

        a.Add(1);

        await Assert.That(d.Count).IsEqualTo(1);
        a.Add(2);
        await Assert.That(d.Count).IsEqualTo(2);

        b.Add(3);
        await Assert.That(d.Count).IsEqualTo(3);
        b.Add(5);
        await Assert.That(d.Count).IsEqualTo(4);
        await Assert.That(new[] { 1, 2, 3, 5 }).IsEquivalentTo(d.Items);

        b.Clear();

        // Fails below
        await Assert.That(d.Count).IsEqualTo(2);
        await Assert.That(new[] { 1, 2 }).IsEquivalentTo(d.Items);

        a.ReplaceAt(0, 100);
        await Assert.That(new[] { 2, 100 }).IsEquivalentTo(d.Items);

        var f = new SourceList<int>();
        f.AddRange(Enumerable.Range(10, 5));
        parent.ReplaceAt(2, f);

        await Assert.That(new[] { 2, 100, 10, 11, 12, 13, 14 }).IsEquivalentTo(d.Items);
    }
}
