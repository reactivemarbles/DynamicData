// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Internal;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Internal;

public class BitsetFixture
{
    [Fact]
    public void NewBitset_HasNoActiveBits()
    {
        // Arrange
        var bitset = new Bitset();

        // Act (no action, testing initial state)

        // Assert
        bitset.HasAny().Should().BeFalse();
        bitset.Count.Should().Be(0);
        bitset.FindHighest().Should().Be(-1);
        bitset.FindLowest().Should().Be(-1);
    }

    [Fact]
    public void Set_SingleBit_IsActiveAndQueryable()
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        bitset.Set(0);

        // Assert
        bitset.IsSet(0).Should().BeTrue();
        bitset.HasAny().Should().BeTrue();
        bitset.Count.Should().Be(1);
        bitset.FindHighest().Should().Be(0);
        bitset.FindLowest().Should().Be(0);
    }

    [Fact]
    public void Set_MultipleBits_FindHighestReturnsLargest()
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        bitset.Set(3);
        bitset.Set(17);
        bitset.Set(42);

        // Assert
        bitset.IsSet(3).Should().BeTrue();
        bitset.IsSet(17).Should().BeTrue();
        bitset.IsSet(42).Should().BeTrue();
        bitset.IsSet(4).Should().BeFalse();
        bitset.Count.Should().Be(3);
        bitset.FindHighest().Should().Be(42);
        bitset.FindLowest().Should().Be(3);
    }

    [Fact]
    public void Clear_RemovesBitAndUpdatesQueries()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(5);
        bitset.Set(10);

        // Act
        bitset.Clear(10);

        // Assert
        bitset.IsSet(10).Should().BeFalse();
        bitset.IsSet(5).Should().BeTrue();
        bitset.HasAny().Should().BeTrue();
        bitset.Count.Should().Be(1);
        bitset.FindHighest().Should().Be(5);
    }

    [Fact]
    public void Clear_LastBit_HasAnyReturnsFalse()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(7);

        // Act
        bitset.Clear(7);

        // Assert
        bitset.IsSet(7).Should().BeFalse();
        bitset.HasAny().Should().BeFalse();
        bitset.Count.Should().Be(0);
        bitset.FindHighest().Should().Be(-1);
    }

    [Fact]
    public void Set_IsIdempotent()
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        bitset.Set(3);
        bitset.Set(3);
        bitset.Set(3);

        // Assert
        bitset.IsSet(3).Should().BeTrue();
        bitset.FindHighest().Should().Be(3);
        bitset.Clear(3);
        bitset.HasAny().Should().BeFalse("single Clear should undo any number of Sets");
    }

    [Fact]
    public void Clear_IsIdempotent()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(5);

        // Act
        bitset.Clear(5);
        bitset.Clear(5);
        bitset.Clear(5);

        // Assert
        bitset.IsSet(5).Should().BeFalse();
        bitset.HasAny().Should().BeFalse();
    }

    [Fact]
    public void ClearAll_RemovesEverything()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(0);
        bitset.Set(31);
        bitset.Set(63);

        // Act
        bitset.ClearAll();

        // Assert
        bitset.IsSet(0).Should().BeFalse();
        bitset.IsSet(31).Should().BeFalse();
        bitset.IsSet(63).Should().BeFalse();
        bitset.HasAny().Should().BeFalse();
        bitset.Count.Should().Be(0);
        bitset.FindHighest().Should().Be(-1);
        bitset.FindLowest().Should().Be(-1);
    }

    [Fact]
    public void Set_BeyondInitialCapacity_GrowsAutomatically()
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        bitset.Set(100);

        // Assert
        bitset.IsSet(100).Should().BeTrue();
        bitset.HasAny().Should().BeTrue();
        bitset.Count.Should().Be(1);
        bitset.FindHighest().Should().Be(100);
        bitset.FindLowest().Should().Be(100);
    }

    [Fact]
    public void MultipleWords_FindHighest_ReturnsCorrectIndex()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(10);   // word 0
        bitset.Set(70);   // word 1
        bitset.Set(130);  // word 2

        // Act
        var highest = bitset.FindHighest();
        var lowest = bitset.FindLowest();

        // Assert
        highest.Should().Be(130);
        lowest.Should().Be(10);
    }

    [Fact]
    public void MultipleWords_ClearHighest_UpdatesFindHighest()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(10);
        bitset.Set(70);
        bitset.Set(130);

        // Act
        bitset.Clear(130);

        // Assert
        bitset.FindHighest().Should().Be(70);
        bitset.IsSet(130).Should().BeFalse();
    }

    [Fact]
    public void MultipleWords_ClearLowest_UpdatesFindLowest()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(10);
        bitset.Set(70);
        bitset.Set(130);

        // Act
        bitset.Clear(10);

        // Assert
        bitset.FindLowest().Should().Be(70);
        bitset.IsSet(10).Should().BeFalse();
    }

    [Fact]
    public void MultipleWords_ClearAll_ClearsAllWords()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(0);
        bitset.Set(64);
        bitset.Set(128);
        bitset.Set(192);

        // Act
        bitset.ClearAll();

        // Assert
        bitset.HasAny().Should().BeFalse();
        bitset.Count.Should().Be(0);
        bitset.FindHighest().Should().Be(-1);
    }

    [Fact]
    public void WordBoundary_Bit63And64()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(63);  // last bit of word 0
        bitset.Set(64);  // first bit of word 1

        // Act
        bitset.Clear(64);

        // Assert
        bitset.IsSet(63).Should().BeTrue();
        bitset.IsSet(64).Should().BeFalse();
        bitset.FindHighest().Should().Be(63);
        bitset.FindLowest().Should().Be(63);
    }

    [Fact]
    public void FindHighest_WithOnlyLowBitsSet()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(200); // force multi-word allocation
        bitset.Clear(200);
        bitset.Set(0);
        bitset.Set(1);
        bitset.Set(2);

        // Act
        var highest = bitset.FindHighest();
        var lowest = bitset.FindLowest();

        // Assert
        highest.Should().Be(2);
        lowest.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(63)]
    public void SingleBit_RoundTrips(int index)
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        bitset.Set(index);

        // Assert
        bitset.IsSet(index).Should().BeTrue();
        bitset.HasAny().Should().BeTrue();
        bitset.FindHighest().Should().Be(index);
        bitset.FindLowest().Should().Be(index);

        bitset.Clear(index);
        bitset.IsSet(index).Should().BeFalse();
        bitset.HasAny().Should().BeFalse();
    }

    [Fact]
    public void SetAndClear_ManyBits_CountStaysConsistent()
    {
        // Arrange
        var bitset = new Bitset();
        for (var i = 0; i < 100; i++)
        {
            bitset.Set(i);
        }

        // Act
        for (var i = 0; i < 99; i++)
        {
            bitset.Clear(i);
        }

        // Assert
        bitset.HasAny().Should().BeTrue();
        bitset.Count.Should().Be(1);
        bitset.FindHighest().Should().Be(99);
        bitset.FindLowest().Should().Be(99);
        bitset.IsSet(99).Should().BeTrue();
        bitset.IsSet(0).Should().BeFalse();

        bitset.Clear(99);
        bitset.HasAny().Should().BeFalse();
        bitset.Count.Should().Be(0);
    }

    [Fact]
    public void IsSet_BeyondCapacity_ReturnsFalse()
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        var result = bitset.IsSet(500);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Clear_BeyondCapacity_DoesNotThrow()
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        var act = () => bitset.Clear(500);

        // Assert
        act.Should().NotThrow();
        bitset.HasAny().Should().BeFalse();
    }

    [Fact]
    public void Compact_ShrinksThenSetGrowsBack()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(200);
        bitset.Clear(200);

        // Act
        bitset.Compact();
        bitset.Set(200);

        // Assert
        bitset.IsSet(200).Should().BeTrue();
        bitset.FindHighest().Should().Be(200);
    }

    [Fact]
    public void Compact_RetainsActiveBits()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(5);
        bitset.Set(200);
        bitset.Clear(200);

        // Act
        bitset.Compact();

        // Assert
        bitset.IsSet(5).Should().BeTrue();
        bitset.HasAny().Should().BeTrue();
        bitset.FindHighest().Should().Be(5);
    }

    [Fact]
    public void Compact_AllEmpty_ShrinkToMinimum()
    {
        // Arrange
        var bitset = new Bitset();
        bitset.Set(200);
        bitset.ClearAll();

        // Act
        bitset.Compact();

        // Assert
        bitset.HasAny().Should().BeFalse();
        bitset.FindHighest().Should().Be(-1);
    }

    [Fact]
    public void Count_TracksSetBitsAccurately()
    {
        // Arrange
        var bitset = new Bitset();

        // Act
        bitset.Set(1);
        bitset.Set(10);
        bitset.Set(100);

        // Assert
        bitset.Count.Should().Be(3);

        // Act (idempotent set)
        bitset.Set(10);

        // Assert
        bitset.Count.Should().Be(3, "idempotent Set should not increment");

        // Act (clear one)
        bitset.Clear(10);

        // Assert
        bitset.Count.Should().Be(2);

        // Act (clear all)
        bitset.ClearAll();

        // Assert
        bitset.Count.Should().Be(0);
    }
}
