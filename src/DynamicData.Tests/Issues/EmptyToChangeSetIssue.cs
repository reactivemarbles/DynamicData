#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

namespace DynamicData.Tests.Issues
{
    public class EmptyToChangeSetIssue
    {
        [Test]
        public async Task EmptyCollectionToChangeSetBehaviour()
        {
            var collection = new ObservableCollection<Unit>();

            var results = collection.ToObservableChangeSet().AsAggregator();
            await Assert.That(results.Messages.Count).IsGreaterThan(0).Because("An empty collection should still have an update, even if empty.");
        }
    }
}
