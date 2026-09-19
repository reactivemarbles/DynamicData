namespace DynamicData.Tests.List;

public class SourceListFixture
{
    [Test]
    public async Task InitialChangeIsRange()
    {
        var source = new SourceList<string>();
        source.Add("A");
        var changeSets = new List<IChangeSet<string>>();

        source.Connect().Subscribe(changeSets.Add).Dispose();

        await Assert.That(changeSets[0].First().Type).IsEqualTo(ChangeType.Range);
        await Assert.That(changeSets[0].First().Range.Index).IsEqualTo(0);
    }
}
