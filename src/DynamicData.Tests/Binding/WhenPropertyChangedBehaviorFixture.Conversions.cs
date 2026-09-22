// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;

using DynamicData.Binding;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public sealed partial class WhenPropertyChangedBehaviorFixture
{
    /// <summary>Verifies that a numeric conversion is evaluated before reading a property of the converted value.</summary>
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
}
