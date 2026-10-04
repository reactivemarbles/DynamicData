using System.Linq;
using System.Reactive.Linq;

using FluentAssertions;

using Xunit;

using DynamicData.Binding;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Binding;

public static partial class WhenPropertyChangedFixture
{
    public partial class UnitTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Subscription_ExplicitDisposal_ReleasesHandlers(bool deepChain)
        {
            // Arrange
            var leaf = new ObservablePrice { Amount = ObservedAmount };
            var child = new ObservablePrice { Child = leaf };
            var root = new ObservablePrice { Child = child };
            var models = deepChain ? new[] { root, child, leaf } : new[] { leaf };
            var source = deepChain
                ? root.WhenValueChanged(static price => price.Child!.Child!.Amount)
                : leaf.WhenValueChanged(static price => price.Amount);
            using var subscription = source.RecordValues(out var results);
            var attachedHandlerCounts = models.Select(model => model.HandlerCount).ToArray();

            // Act
            subscription.Dispose();

            // Assert
            attachedHandlerCounts.Should().OnlyContain(count => count == 1, because: "each visited object must stay subscribed after initialization");
            models.Select(model => model.HandlerCount).Should().OnlyContain(count => count == 0,
                because: "disposing the returned subscription must release every retained handler");
            results.RecordedValues.Should().Equal(new[] { ObservedAmount }, because: "initialization must publish the observed value");
            results.Error.Should().BeNull(because: "explicit disposal is not an observation failure");
            results.HasCompleted.Should().BeFalse(because: "unsubscribing does not publish a completion notification");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Subscription_SynchronousCompletion_ReleasesHandlers(bool deepChain)
        {
            // Arrange
            var leaf = new ObservablePrice { Amount = ObservedAmount };
            var child = new ObservablePrice { Child = leaf };
            var root = new ObservablePrice { Child = child };
            var models = deepChain ? new[] { root, child, leaf } : new[] { leaf };
            var source = deepChain
                ? root.WhenValueChanged(static price => price.Child!.Child!.Amount)
                : leaf.WhenValueChanged(static price => price.Amount);

            // Act
            using var subscription = source.Take(1)
                .RecordValues(out var results);

            // Assert
            results.RecordedValues.Should().Equal(new[] { ObservedAmount }, because: "the requested initial value must be delivered");
            results.Error.Should().BeNull(because: "taking an initial value is normal completion");
            results.HasCompleted.Should().BeTrue(because: "Take completes after receiving its requested value");
            models.Should().OnlyContain(model => model.WasSubscribed, because: "handlers must attach before initial delivery");
            models.Select(model => model.HandlerCount).Should().OnlyContain(count => count == 0,
                because: "synchronous completion must release handlers before Subscribe returns");
        }
    }
}
