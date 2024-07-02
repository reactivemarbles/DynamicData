using System.Collections.ObjectModel;
using System.Reactive;
using DynamicData.Binding;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Issues
{
    public class EmptyToChangeSetIssue
    {
        [Fact]
        public void EmptyCollectionToChangeSetBehaviour()
        {
            var collection = new ObservableCollection<Unit>();

            var results = collection.ToObservableChangeSet().AsAggregator();
            results.Messages.Count.Should()
                .BeGreaterThan(0, "An empty collection should still have an update, even if empty.");
        }
    }
}
