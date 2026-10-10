namespace DynamicData.Tests.Binding;
 
public static partial class WhenPropertyChangedFixture
{
    public partial class IntegrationTests
    {
        [Fact]
        public async Task Shallow_ConcurrentMutationDuringInitialEmit_NotDropped()
        {
            var item = new Item()
            {
                Id = 1,
                Value = 10
            };

            var whenSubscribing = new ManualResetEventSlim();
            var whenValueChanged = new ManualResetEventSlim();

            var source = item.WhenPropertyChanged(
                propertyAccessor:       static item => item.Value,
                notifyOnInitialValue:   true);

            var observedValues = new List<int>();
            var observer = Observer.Create<PropertyValue<Item, int>>(propertyValue =>
            {
                observedValues.Add(propertyValue.Value);

                whenSubscribing.Set();
                whenValueChanged.Wait();
            });

            await Task.WhenAll(
                Task.Run(() =>
                {
                    using var subscription = source.Subscribe(observer);
                }),
                Task.Run(() =>
                {
                    whenSubscribing.Wait();

                    item.Value = 20;

                    whenValueChanged.Set();
                }));

            observedValues.Should().BeEquivalentTo(
                expectation:    new [] { 10, 20 },
                config:         options => options.WithStrictOrdering(),
                because:        "All change events occurring after publication of the initial value should be captured and forwarded.");
        }
    }
}
