using System;
using System.ComponentModel;
using System.Linq.Expressions;

using FluentAssertions;

using Xunit;

using DynamicData.Binding;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Binding;

public static partial class WhenPropertyChangedFixture
{
    public partial class UnitTests
    {
        [Fact]
        public void NumericConversionBeforePropertyAccess_InitialValue_IsObserved()
        {
            // Arrange
            // An odd numerator produces an exactly representable, non-integral amount.
            var amount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 4d;
            var model = new ObservablePrice { Amount = amount };
            var expectedScale = ((decimal)amount).Scale;

            // Act
            using var subscription = model.WhenValueChanged(static price => ((decimal)price.Amount).Scale)
                .RecordValues(out var results);

            // Assert
            results.Error.Should().BeNull(because: "the next property belongs to the converted decimal, not the source double");
            results.RecordedValues.Should().Equal(new[] { expectedScale }, because: "the initial value must match the supplied expression");
            results.HasCompleted.Should().BeFalse(because: "further amount changes remain observable");
        }

        /// <summary>Verifies that property changes remain observable through a value-changing numeric conversion.</summary>
        /// <param name="notifyOnInitialValue">Whether subscribing requests an initial value notification.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NumericConversionBeforePropertyAccess_PropertyChanges_AreObserved(bool notifyOnInitialValue)
        {
            // Arrange
            // Odd quarters and eighths have distinct decimal scales without floating-point rounding.
            var initialAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 4d;
            var changedAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 8d;
            var model = new ObservablePrice { Amount = initialAmount };
            var initialScale = ((decimal)initialAmount).Scale;
            var changedScale = ((decimal)changedAmount).Scale;
            var expectedScales = notifyOnInitialValue ? new[] { initialScale, changedScale } : new[] { changedScale };
            using var subscription = model.WhenValueChanged(static price => ((decimal)price.Amount).Scale, notifyOnInitialValue)
                .RecordValues(out var results);

            // Act
            model.Amount = changedAmount;

            // Assert
            results.Error.Should().BeNull(because: "conversion must be applied on both initial and subsequent chain reads");
            results.RecordedValues.Should().Equal(expectedScales, because: "notifications must match the expression and initial-value option");
            results.HasCompleted.Should().BeFalse(because: "amount changes do not complete the observation");
        }

        /// <summary>Verifies that a numeric conversion at the end of a property path preserves initial and changed values.</summary>
        [Fact]
        public void NumericConversionAtLeaf_PropertyChanges_AreObserved()
        {
            // Arrange
            var initialAmount = _randomizer.Double();
            var changedAmount = initialAmount + _randomizer.Double(1, 2);
            var model = new ObservablePrice { Amount = initialAmount };
            var expectedAmounts = new[] { (decimal)initialAmount, (decimal)changedAmount };
            using var subscription = model.WhenValueChanged(static price => (decimal)price.Amount)
                .RecordValues(out var results);

            // Act
            model.Amount = changedAmount;

            // Assert
            results.Error.Should().BeNull(because: "a conversion must also remain supported as the final expression step");
            results.RecordedValues.Should().Equal(expectedAmounts, because: "each observed value must be converted to the requested type");
            results.HasCompleted.Should().BeFalse(because: "further amount changes remain observable");
        }

        /// <summary>Verifies that a reference cast preserves observation of properties on the runtime model.</summary>
        [Fact]
        public void ReferenceCastBeforePropertyAccess_PropertyChanges_AreObserved()
        {
            // Arrange
            var person = Fakers.Person.Clone().WithSeed(_randomizer).Generate();
            INotifyPropertyChanged model = person;
            var initialAge = person.Age;
            var changedAge = initialAge + _randomizer.Int(1, byte.MaxValue);
            using var subscription = model.WhenValueChanged(static source => ((Person)source).Age)
                .RecordValues(out var results);

            // Act
            person.Age = changedAge;

            // Assert
            results.Error.Should().BeNull(because: "supported reference casts must preserve property observation");
            results.RecordedValues.Should().Equal(new[] { initialAge, changedAge }, because: "the runtime model remains the notification source");
            results.HasCompleted.Should().BeFalse(because: "further age changes remain observable");
        }

        /// <summary>Verifies that an interface cast preserves observation after replacing an intermediate object.</summary>
        [Fact]
        public void InterfaceCastBeforePropertyAccess_ReplacementChildChanges_AreObserved()
        {
            // Arrange
            var initialAge = _randomizer.Int(1, byte.MaxValue);
            var replacementAge = initialAge + _randomizer.Int(1, byte.MaxValue);
            var changedAge = replacementAge + _randomizer.Int(1, byte.MaxValue);
            var parent = new ParentModel { Child = new ChildModel { Age = initialAge } };
            var replacement = new ChildModel { Age = replacementAge };
            using var subscription = parent.WhenValueChanged(static source => ((IHasAge)source.Child!).Age)
                .RecordValues(out var results);
            parent.Child = replacement;

            // Act
            replacement.Age = changedAge;

            // Assert
            results.Error.Should().BeNull(because: "supported interface casts must preserve nested property observation");
            results.RecordedValues.Should().Equal(new[] { initialAge, replacementAge, changedAge },
                because: "the interface property must follow the replacement child and its subsequent changes");
            results.HasCompleted.Should().BeFalse(because: "further child changes remain observable");
        }

        /// <summary>Verifies that converted properties retain independent values and notification sources.</summary>
        /// <param name="changeAmount">Whether to change the first observed property rather than the second.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NumericConversionsOnDifferentProperties_Changes_AreIndependent(bool changeAmount)
        {
            // Arrange
            var initialAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 4d;
            var initialOtherAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 8d;
            var changedAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 16d;
            var model = new ObservablePrice { Amount = initialAmount, OtherAmount = initialOtherAmount };
            var initialScale = ((decimal)initialAmount).Scale;
            var initialOtherScale = ((decimal)initialOtherAmount).Scale;
            var changedScale = ((decimal)changedAmount).Scale;
            var expectedScales = changeAmount ? new[] { initialScale, changedScale } : new[] { initialScale };
            var expectedOtherScales = changeAmount ? new[] { initialOtherScale } : new[] { initialOtherScale, changedScale };
            using var amountSubscription = model.WhenValueChanged(static price => ((decimal)price.Amount).Scale)
                .RecordValues(out var amounts);
            using var otherSubscription = model.WhenValueChanged(static price => ((decimal)price.OtherAmount).Scale)
                .RecordValues(out var otherAmounts);

            // Act
            if (changeAmount)
            {
                model.Amount = changedAmount;
            }
            else
            {
                model.OtherAmount = changedAmount;
            }

            // Assert
            amounts.Error.Should().BeNull(because: "the first converted property remains observable");
            otherAmounts.Error.Should().BeNull(because: "the second converted property remains observable");
            amounts.RecordedValues.Should().Equal(expectedScales, because: "the first path observes only its own property");
            otherAmounts.RecordedValues.Should().Equal(expectedOtherScales, because: "the second path observes only its own property");
            amounts.HasCompleted.Should().BeFalse(because: "the first observation remains active");
            otherAmounts.HasCompleted.Should().BeFalse(because: "the second observation remains active");
        }

        /// <summary>Verifies that different conversions of one property retain their own evaluation paths.</summary>
        [Fact]
        public void DifferentConversionPathsOnSameProperty_Changes_UseEachConversion()
        {
            // Arrange
            var initialAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 4d;
            var changedAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 8d;
            var model = new ObservablePrice { Amount = initialAmount };
            var expectedScales = new[] { ((decimal)initialAmount).Scale, ((decimal)changedAmount).Scale };
            var expectedTruncatedScales = new[] { ((decimal)(long)initialAmount).Scale, ((decimal)(long)changedAmount).Scale };
            using var exactSubscription = model.WhenValueChanged(static price => ((decimal)price.Amount).Scale)
                .RecordValues(out var exact);
            using var truncatedSubscription = model.WhenValueChanged(static price => ((decimal)(long)price.Amount).Scale)
                .RecordValues(out var truncated);

            // Act
            model.Amount = changedAmount;

            // Assert
            exact.Error.Should().BeNull(because: "the direct conversion remains observable");
            truncated.Error.Should().BeNull(because: "the conversion through an integer remains observable");
            exact.RecordedValues.Should().Equal(expectedScales, because: "the direct path preserves fractional digits");
            truncated.RecordedValues.Should().Equal(expectedTruncatedScales, because: "the integer conversion discards fractional digits");
            exact.HasCompleted.Should().BeFalse(because: "the direct observation remains active");
            truncated.HasCompleted.Should().BeFalse(because: "the integer-converted observation remains active");
        }

        /// <summary>Verifies that conversions to different result types do not share incompatible property factories.</summary>
        [Fact]
        public void LeafConversionsWithDifferentResultTypes_Changes_UseEachResultType()
        {
            // Arrange
            var initialAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 4d;
            var changedAmount = initialAmount + _randomizer.Int(1, ushort.MaxValue);
            var model = new ObservablePrice { Amount = initialAmount };
            var expectedDecimals = new[] { (decimal)initialAmount, (decimal)changedAmount };
            var expectedIntegers = new[] { (long)initialAmount, (long)changedAmount };
            using var decimalSubscription = model.WhenValueChanged(static price => (decimal)price.Amount)
                .RecordValues(out var decimals);
            using var integerSubscription = model.WhenValueChanged(static price => (long)price.Amount)
                .RecordValues(out var integers);

            // Act
            model.Amount = changedAmount;

            // Assert
            decimals.Error.Should().BeNull(because: "the decimal result must have a compatible factory");
            integers.Error.Should().BeNull(because: "the integer result must have a compatible factory");
            decimals.RecordedValues.Should().Equal(expectedDecimals, because: "the decimal path preserves fractional values");
            integers.RecordedValues.Should().Equal(expectedIntegers, because: "the integer path truncates fractional values");
            decimals.HasCompleted.Should().BeFalse(because: "the decimal observation remains active");
            integers.HasCompleted.Should().BeFalse(because: "the integer observation remains active");
        }

        /// <summary>Verifies that conversion methods with matching source and result types retain distinct behavior.</summary>
        [Fact]
        public void ConversionMethodsWithSameSignature_Changes_UseEachMethod()
        {
            // Arrange
            var initialAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 4d;
            var changedAmount = (_randomizer.Int(1, ushort.MaxValue) | 1) / 8d;
            var model = new ObservablePrice { Amount = initialAmount };
            Func<double, decimal> exactConversion = ConvertAmount;
            Func<double, decimal> truncatedConversion = TruncateAmount;
            var parameter = Expression.Parameter(typeof(ObservablePrice), nameof(model));
            var amount = Expression.Property(parameter, nameof(ObservablePrice.Amount));
            var exactBody = Expression.Property(Expression.Convert(amount, typeof(decimal), exactConversion.Method), nameof(decimal.Scale));
            var truncatedBody = Expression.Property(
                Expression.Convert(amount, typeof(decimal), truncatedConversion.Method), nameof(decimal.Scale));
            var exactPath = Expression.Lambda<Func<ObservablePrice, byte>>(exactBody, parameter);
            var truncatedPath = Expression.Lambda<Func<ObservablePrice, byte>>(truncatedBody, parameter);
            var expectedScales = new[] { exactConversion(initialAmount).Scale, exactConversion(changedAmount).Scale };
            var expectedTruncatedScales = new[] { truncatedConversion(initialAmount).Scale, truncatedConversion(changedAmount).Scale };
            using var exactSubscription = model.WhenValueChanged(exactPath)
                .RecordValues(out var exact);
            using var truncatedSubscription = model.WhenValueChanged(truncatedPath)
                .RecordValues(out var truncated);

            // Act
            model.Amount = changedAmount;

            // Assert
            exact.Error.Should().BeNull(because: "the fractional conversion method remains observable");
            truncated.Error.Should().BeNull(because: "the truncating conversion method remains observable");
            exact.RecordedValues.Should().Equal(expectedScales, because: "the fractional conversion method belongs to this path");
            truncated.RecordedValues.Should().Equal(expectedTruncatedScales, because: "the truncating conversion method belongs to this path");
            exact.HasCompleted.Should().BeFalse(because: "the fractional observation remains active");
            truncated.HasCompleted.Should().BeFalse(because: "the truncating observation remains active");
        }

        /// <summary>Verifies that a user-defined conversion operator is applied before reading a property of its result.</summary>
        [Fact]
        public void UserDefinedConversionBeforePropertyAccess_PropertyChanges_AreObserved()
        {
            // Arrange
            // A fractional amount proves the operator ran, because it keeps only whole units.
            var initialAmount = _randomizer.Int(1, ushort.MaxValue) + 0.5d;
            var changedAmount = initialAmount + _randomizer.Int(1, ushort.MaxValue);
            var model = new ObservablePrice { Amount = initialAmount };
            var expectedUnits = new[] { ((Money)initialAmount).WholeUnits, ((Money)changedAmount).WholeUnits };
            using var subscription = model.WhenValueChanged(static price => ((Money)price.Amount).WholeUnits)
                .RecordValues(out var results);

            // Act
            model.Amount = changedAmount;

            // Assert
            results.Error.Should().BeNull(because: "a user-defined conversion operator must run before the next property is read");
            results.RecordedValues.Should().Equal(expectedUnits, because: "each observed value must be read from the converted result");
            results.HasCompleted.Should().BeFalse(because: "further amount changes remain observable");
        }

        /// <summary>Verifies that a user-defined conversion keeps its own evaluation path alongside a built-in conversion.</summary>
        [Fact]
        public void UserDefinedAndBuiltInConversionsOnSameProperty_Changes_UseEachConversion()
        {
            // Arrange
            var initialAmount = _randomizer.Int(1, ushort.MaxValue) + 0.5d;
            var changedAmount = initialAmount + _randomizer.Int(1, ushort.MaxValue);
            var model = new ObservablePrice { Amount = initialAmount };
            var expectedUnits = new[] { ((Money)initialAmount).WholeUnits, ((Money)changedAmount).WholeUnits };
            var expectedScales = new[] { ((decimal)initialAmount).Scale, ((decimal)changedAmount).Scale };
            using var moneySubscription = model.WhenValueChanged(static price => ((Money)price.Amount).WholeUnits)
                .RecordValues(out var units);
            using var decimalSubscription = model.WhenValueChanged(static price => ((decimal)price.Amount).Scale)
                .RecordValues(out var scales);

            // Act
            model.Amount = changedAmount;

            // Assert
            units.Error.Should().BeNull(because: "the user-defined conversion must have a compatible factory");
            scales.Error.Should().BeNull(because: "the built-in conversion must have a compatible factory");
            units.RecordedValues.Should().Equal(expectedUnits, because: "the user-defined path reports whole units");
            scales.RecordedValues.Should().Equal(expectedScales, because: "the built-in path reports the decimal scale");
            units.HasCompleted.Should().BeFalse(because: "the user-defined observation remains active");
            scales.HasCompleted.Should().BeFalse(because: "the built-in observation remains active");
        }

        /// <summary>Verifies that equivalent converted paths share a factory regardless of lambda parameter names.</summary>
        [Fact]
        public void EquivalentConversionPaths_DifferentParameterNames_ReuseFactory()
        {
            // Arrange
            Expression<Func<ObservablePrice, byte>> first = static price => ((decimal)price.Amount).Scale;
            Expression<Func<ObservablePrice, byte>> second = static otherPrice => ((decimal)otherPrice.Amount).Scale;
            var firstFactory = ObservablePropertyFactoryCache.Instance.GetFactory(first);

            // Act
            var secondFactory = ObservablePropertyFactoryCache.Instance.GetFactory(second);

            // Assert
            secondFactory.Should().BeSameAs(firstFactory, because: "parameter names do not change a property's evaluation path");
        }
    }
}
