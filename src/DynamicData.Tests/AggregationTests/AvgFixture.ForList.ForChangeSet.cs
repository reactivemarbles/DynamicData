namespace DynamicData.Tests.AggregationTests;

public partial class AvgFixture
{
    public partial class ForList
    {
        // ForAggregateChangeSet contains the shared behavioral coverage.
        public class ForChangeSet
        {
            [Fact]
            public void ChangeSetOverload_ForwardsToAggregateOverload()
            {
                using var source = CreatePopulatedSource();

                using var subscription = source.Connect()
                    .Avg(value => value, emptyValue: -1)
                    .RecordValues(out var results);

                source.Clear();

                results.RecordedValues.Should().Equal(20.0, -1.0);
            }

            private static TestSourceList<int> CreatePopulatedSource()
            {
                var source = new TestSourceList<int>();
                source.AddRange(new[] { 10, 20, 30 });
                return source;
            }
        }
    }
}
