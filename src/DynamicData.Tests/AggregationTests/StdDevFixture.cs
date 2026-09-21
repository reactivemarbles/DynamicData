// Copyright (c) 2011-2026 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Linq;

using Bogus;

using DynamicData.Aggregation;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;
using Xunit.Abstractions;

namespace DynamicData.Tests.AggregationTests;

/// <summary>Verifies sample standard deviation for the supported numeric overloads.</summary>
public sealed class StdDevFixture
{
    private const int Seed = 0x2409_1071;

    private readonly Randomizer _randomizer = new(Seed);

    public StdDevFixture(ITestOutputHelper output)
        => output.WriteLine($"{nameof(StdDevFixture)} seed: {Seed:X8}");

    /// <summary>Verifies that cache standard deviation applies the sample divisor to the variance.</summary>
    [Theory]
    [InlineData(nameof(Int32))]
    [InlineData(nameof(Int64))]
    [InlineData(nameof(Single))]
    [InlineData(nameof(Double))]
    [InlineData(nameof(Decimal))]
    public void Cache_ThreeEquallySpacedValues_ReportsTheirSpacing(string numericType)
    {
        // Arrange
        var center = _randomizer.Int(-100, 100);
        var spacing = _randomizer.Int(1, 30);
        var values = new[] { center - spacing, center, center + spacing };
        var fallback = _randomizer.Int(1, 100);
        using var source = new TestSourceCache<int, int>(static value => value);
        var standardDeviation = numericType switch
        {
            nameof(Int32) => source.Connect().StdDev(static value => value, fallback),
            nameof(Int64) => source.Connect().StdDev(static value => (long)value, fallback),
            nameof(Single) => source.Connect().StdDev(static value => (float)value, fallback),
            nameof(Double) => source.Connect().StdDev(static value => (double)value, fallback),
            nameof(Decimal) => source.Connect().StdDev(static value => (decimal)value, fallback).Select(static value => (double)value),
            _ => throw new ArgumentOutOfRangeException(nameof(numericType))
        };
        using var subscription = standardDeviation
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act
        source.AddOrUpdate(values);

        // Assert
        results.Error.Should().BeNull(because: "all values are within the supported numeric ranges");
        results.RecordedValues.Should().ContainSingle(because: "one source edit produces one aggregate")
            .Which.Should().Be(spacing, because: "the sample variance of three equally spaced values is the square of their spacing");
    }

    /// <summary>Verifies that list standard deviation applies the sample divisor to the variance.</summary>
    [Theory]
    [InlineData(nameof(Int32))]
    [InlineData(nameof(Int64))]
    [InlineData(nameof(Single))]
    [InlineData(nameof(Double))]
    [InlineData(nameof(Decimal))]
    public void List_ThreeEquallySpacedValues_ReportsTheirSpacing(string numericType)
    {
        // Arrange
        var center = _randomizer.Int(-100, 100);
        var spacing = _randomizer.Int(1, 30);
        var values = new[] { center - spacing, center, center + spacing };
        var fallback = _randomizer.Int(1, 100);
        using var source = new TestSourceList<int>();
        var standardDeviation = numericType switch
        {
            nameof(Int32) => source.Connect().StdDev(static value => value, fallback),
            nameof(Int64) => source.Connect().StdDev(static value => (long)value, fallback),
            nameof(Single) => source.Connect().StdDev(static value => (float)value, fallback),
            nameof(Double) => source.Connect().StdDev(static value => (double)value, fallback),
            nameof(Decimal) => source.Connect().StdDev(static value => (decimal)value, fallback).Select(static value => (double)value),
            _ => throw new ArgumentOutOfRangeException(nameof(numericType))
        };
        using var subscription = standardDeviation
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act
        source.AddRange(values);

        // Assert
        results.Error.Should().BeNull(because: "all values are within the supported numeric ranges");
        results.RecordedValues.Should().ContainSingle(because: "one source edit produces one aggregate")
            .Which.Should().Be(spacing, because: "the sample variance of three equally spaced values is the square of their spacing");
    }

    /// <summary>Verifies that integer overloads retain the fractional part of the mean when calculating variance.</summary>
    [Theory]
    [InlineData(nameof(Int32))]
    [InlineData(nameof(Int64))]
    public void IntegerValues_FractionalMean_PreservesFractionalVariance(string numericType)
    {
        // Arrange
        var start = _randomizer.Int(-100, 100);
        var spacing = _randomizer.Int(2, 30);
        var adjustment = _randomizer.Int(1, 2);
        var values = new[] { start, start + spacing, start + spacing + spacing + adjustment };
        var mean = values.Average();
        var expected = Math.Sqrt(values.Sum(value => Math.Pow(value - mean, 2)) / (values.Length - 1));
        var fallback = _randomizer.Int(1, 100);
        using var source = new TestSourceCache<int, int>(static value => value);
        var standardDeviation = numericType switch
        {
            nameof(Int32) => source.Connect().StdDev(static value => value, fallback),
            nameof(Int64) => source.Connect().StdDev(static value => (long)value, fallback),
            _ => throw new ArgumentOutOfRangeException(nameof(numericType))
        };
        using var subscription = standardDeviation
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act
        source.AddOrUpdate(values);

        // Assert
        results.Error.Should().BeNull(because: "integer inputs with a fractional mean are valid");
        results.RecordedValues.Should().ContainSingle(because: "one source edit produces one aggregate")
            .Which.Should().BeApproximately(expected, 1e-10, because: "integer inputs do not imply an integer-valued mean or variance");
    }

    /// <summary>Verifies that clearing the input returns the configured fallback rather than evaluating an undefined variance.</summary>
    [Theory]
    [InlineData(nameof(Int32))]
    [InlineData(nameof(Int64))]
    [InlineData(nameof(Single))]
    [InlineData(nameof(Double))]
    [InlineData(nameof(Decimal))]
    public void Cache_AllItemsRemoved_ReportsFallback(string numericType)
    {
        // Arrange
        var center = _randomizer.Int(-100, 100);
        var spacing = _randomizer.Int(1, 30);
        var values = new[] { center - spacing, center, center + spacing };
        var fallback = _randomizer.Int(1, 100);
        using var source = new TestSourceCache<int, int>(static value => value);
        source.AddOrUpdate(values);
        var standardDeviation = numericType switch
        {
            nameof(Int32) => source.Connect().StdDev(static value => value, fallback),
            nameof(Int64) => source.Connect().StdDev(static value => (long)value, fallback),
            nameof(Single) => source.Connect().StdDev(static value => (float)value, fallback),
            nameof(Double) => source.Connect().StdDev(static value => (double)value, fallback),
            nameof(Decimal) => source.Connect().StdDev(static value => (decimal)value, fallback).Select(static value => (double)value),
            _ => throw new ArgumentOutOfRangeException(nameof(numericType))
        };
        using var subscription = standardDeviation
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act
        source.Clear();

        // Assert
        results.Error.Should().BeNull(because: "an empty collection has a defined fallback");
        results.RecordedValues[^1].Should().Be(fallback, because: "sample variance is not calculated for fewer than two items");
    }
}
