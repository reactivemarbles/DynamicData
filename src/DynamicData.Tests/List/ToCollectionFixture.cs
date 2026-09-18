namespace DynamicData.Tests.List;

public class ToCollectionFixture
{
    [Test]
    public async Task ToCollectionTest()
    {
        var list = new SourceList<string>();
        //   var collection = Observable.Defer(() =>  list.Connect().ToCollection());
        var collection = list.Connect().ToCollection();
        IReadOnlyCollection<string>? res1 = null;
        IReadOnlyCollection<string>? res2 = null;
        collection.Subscribe(x => res1 = x);
        collection.Subscribe(x => res2 = x);
        list.Add("1");
        list.Add("2");
        await Assert.That(res1?.Count).IsEqualTo(2);
        await Assert.That(res2?.Count).IsEqualTo(2);
    }
}
