namespace DynamicData.Tests.List;

public class RemoveManyFixture
{
    private readonly List<int> _list;

    public RemoveManyFixture() => _list = new List<int>();

    [Test]
    public async Task DoesNotRemoveDuplicates()
    {
        _list.AddRange(new[] { 1, 1, 1, 5, 6, 7 });
        _list.RemoveMany(new[] { 1, 1, 7 });
        await Assert.That(_list).IsEquivalentTo(new[] { 1, 5, 6 });
    }

    [Test]
    public async Task RemoveLargeBatch()
    {
        var toAdd = Enumerable.Range(1, 10000).ToArray();
        _list.AddRange(toAdd);

        var toRemove = _list.Take(_list.Count / 2).OrderBy(x => Guid.NewGuid()).ToArray();
        _list.RemoveMany(toRemove);
        await Assert.That(_list).IsEquivalentTo(toAdd.Except(toRemove));
    }

    [Test]
    public async Task RemoveManyWillRemoveARange()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.RemoveMany(Enumerable.Range(2, 8));
        await Assert.That(_list).IsEquivalentTo(new[] { 1, 10 });
    }
}
