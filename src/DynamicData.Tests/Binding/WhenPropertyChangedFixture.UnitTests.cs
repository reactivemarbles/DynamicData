using System;
using System.Linq;

using Bogus;

using FluentAssertions;

using Xunit;

using DynamicData.Binding;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Binding;
 
public static partial class WhenPropertyChangedFixture
{
    public partial class UnitTests
    {
        private readonly Randomizer _randomizer
            = new(0x35C1_709B);

        // https://github.com/reactivemarbles/DynamicData/issues/1149
        [Fact]
        public void ExpressionContainsImplicitInterfaceCast()
        {
            var child = new ChildModel()
            {
                Age = 10
            };
            
            using var subscription = ObserveAge(child)
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.RecordedValues.Should().ContainSingle("the initial value of the observed expression should have been published");
            results.RecordedValues[0].Should().Be(child.Age, "the initial value of the observed expression should have been published");

            ++child.Age;

            results.Error.Should().BeNull("no errors should have occurred");
            results.RecordedValues.Skip(1).Should().ContainSingle("the value of the observed expression changed once");
            results.RecordedValues[1].Should().Be(child.Age, "the correct value should have been published");

            static IObservable<int> ObserveAge<T>(T source)
                where T : IHasAge
                => source.WhenValueChanged(source => source.Age);
        }
    }
}
