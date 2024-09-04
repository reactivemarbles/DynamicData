using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading.Channels;

using Argon;

using Bogus;

using FluentAssertions;

using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DynamicData.Tests.List;

public class ChangeSetFixture
{
    public static IEnumerable<object[]> EnumerateCountsAreCorrectTestCases()
        => Enumerable.Empty<CountsAreCorrectTestCase>()
            .Append(new()
            {
                Name = "Empty Changeset"
            })
            .Append(new()
            {
                Name            = "Single Add",
                AddCount        = 1,
                ChangeCount     = 1,
                TotalItemCount  = 1
            })
            .Append(new()
            {
                Name            = "Multiple Adds",
                AddCount        = 5,
                ChangeCount     = 5,
                TotalItemCount  = 5
            })
            .Append(new()
            {
                Name            = "Range Add",
                AddRangeCount   = 1,
                AddRangeSize    = 5,
                ChangeCount     = 1,
                TotalItemCount  = 5
            })
            .Append(new()
            {
                Name            = "Single Move",
                MoveCount       = 1,
                ChangeCount     = 1,
                TotalItemCount  = 1
            })
            .Append(new()
            {
                Name            = "Multiple Moves",
                MoveCount       = 5,
                ChangeCount     = 5,
                TotalItemCount  = 5
            })
            .Append(new()
            {
                Name            = "Single Refresh",
                RefreshCount    = 1,
                ChangeCount     = 1,
                TotalItemCount  = 1
            })
            .Append(new()
            {
                Name            = "Multiple Refreshes",
                RefreshCount    = 5,
                ChangeCount     = 5,
                TotalItemCount  = 5
            })
            .Append(new()
            {
                Name            = "Single Remove",
                RemoveCount     = 1,
                ChangeCount     = 1,
                TotalItemCount  = 1
            })
            .Append(new()
            {
                Name            = "Multiple Removes",
                RemoveCount     = 5,
                ChangeCount     = 5,
                TotalItemCount  = 5
            })
            .Append(new()
            {
                Name                = "Range Remove",
                RemoveRangeCount    = 1,
                RemoveRangeSize     = 5,
                ChangeCount         = 1,
                TotalItemCount      = 5
            })
            .Append(new()
            {
                Name            = "Single Replace",
                ReplaceCount    = 1,
                ChangeCount     = 1,
                TotalItemCount  = 1
            })
            .Append(new()
            {
                Name            = "Multiple Replaces",
                ReplaceCount    = 5,
                ChangeCount     = 5,
                TotalItemCount  = 5
            })
            .Append(new()
            {
                Name                = "Various Changes",
                AddCount            = 1,
                AddRangeCount       = 2,
                AddRangeSize        = 3,
                MoveCount           = 4,
                RefreshCount        = 5,
                RemoveCount         = 6,
                RemoveRangeCount    = 7,
                RemoveRangeSize     = 8,
                ReplaceCount        = 9,
                ChangeCount         = 34,
                TotalItemCount      = 87
            })
            .Select(testCase => new object[] { testCase });

    [Theory]
    [MemberData(nameof(EnumerateCountsAreCorrectTestCases))]
    public void CountsAreCorrect(CountsAreCorrectTestCase testCase)
    {
        var uut = new ChangeSet<int>(testCase.EnumerateChanges());

        uut.Count.Should().Be(testCase.ChangeCount);
        uut.Adds.Should().Be(testCase.AddCount + (testCase.AddRangeCount * testCase.AddRangeSize));
        uut.Moves.Should().Be(testCase.MoveCount);
        uut.Refreshes.Should().Be(testCase.RefreshCount);
        uut.Removes.Should().Be(testCase.RemoveCount + (testCase.RemoveRangeCount * testCase.RemoveRangeSize));
        uut.Replaced.Should().Be(testCase.ReplaceCount);
        uut.TotalChanges.Should().Be(testCase.TotalItemCount);
    }

    public record struct CountsAreCorrectTestCase
        : IXunitSerializable
    {
        public required string Name { get; set; }

        public int ChangeCount { get; set; }
        
        public int AddCount { get; set; }

        public int AddRangeCount { get; set; }
        
        public int AddRangeSize { get; set; }

        public int MoveCount { get; set; }

        public int RefreshCount { get; set; }

        public int RemoveCount { get; set; }
        
        public int RemoveRangeCount { get; set; }
        
        public int RemoveRangeSize { get; set; }

        public int ReplaceCount { get; set; }

        public int TotalItemCount { get; set; }

        public IEnumerable<Change<int>> EnumerateChanges()
        {
            var randomizer = new Randomizer(Name.GetHashCode());

            for (var i = 0; i < AddCount; ++i)
                yield return new Change<int>(
                    reason:     ListChangeReason.Add,
                    current:    randomizer.Int(),
                    index:      randomizer.Int(min: 0));

            for (var i = 0; i < AddRangeCount; ++i)
                yield return new Change<int>(
                    reason:     ListChangeReason.AddRange,
                    items:      Enumerable.Repeat(0, AddRangeSize)
                        .Select(_ => randomizer.Int())
                        .ToArray(),
                    index:      randomizer.Int(min: 0));

            for (var i = 0; i < MoveCount; ++i)
                yield return new Change<int>(
                    current:        randomizer.Int(),
                    currentIndex:   randomizer.Int(min: 0),
                    previousIndex:  randomizer.Int(min: 0));

            for (var i = 0; i < RefreshCount; ++i)
                yield return new Change<int>(
                    reason:     ListChangeReason.Refresh,
                    current:    randomizer.Int(),
                    index:      randomizer.Int(min: 0));

            for (var i = 0; i < RemoveCount; ++i)
                yield return new Change<int>(
                    reason:     ListChangeReason.Remove,
                    current:    randomizer.Int(),
                    index:      randomizer.Int(min: 0));

            for (var i = 0; i < RemoveRangeCount; ++i)
                yield return new Change<int>(
                    reason:     ListChangeReason.RemoveRange,
                    items:      Enumerable.Repeat(0, RemoveRangeSize)
                        .Select(_ => randomizer.Int())
                        .ToArray(),
                    index:      randomizer.Int(min: 0));

            for (var i = 0; i < ReplaceCount; ++i)
                yield return new Change<int>(
                    reason:         ListChangeReason.Replace,
                    current:        randomizer.Int(),
                    previous:       randomizer.Int(),
                    currentIndex:   randomizer.Int(min: 0),
                    previousIndex:  randomizer.Int(min: 0));
        }

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            Name                = info.GetValue<string>(nameof(Name));
            ChangeCount         = info.GetValue<int>(nameof(ChangeCount));
            AddCount            = info.GetValue<int>(nameof(AddCount));
            AddRangeCount       = info.GetValue<int>(nameof(AddRangeCount));
            AddRangeSize        = info.GetValue<int>(nameof(AddRangeSize));
            MoveCount           = info.GetValue<int>(nameof(MoveCount));
            RefreshCount        = info.GetValue<int>(nameof(RefreshCount));
            RemoveCount         = info.GetValue<int>(nameof(RemoveCount));
            RemoveRangeCount    = info.GetValue<int>(nameof(RemoveRangeCount));
            RemoveRangeSize     = info.GetValue<int>(nameof(RemoveRangeSize));
            ReplaceCount        = info.GetValue<int>(nameof(ReplaceCount));
            TotalItemCount      = info.GetValue<int>(nameof(TotalItemCount));
        }
        
        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Name),             Name);
            info.AddValue(nameof(ChangeCount),      ChangeCount);
            info.AddValue(nameof(AddCount),         AddCount);
            info.AddValue(nameof(AddRangeCount),    AddRangeCount);
            info.AddValue(nameof(AddRangeSize),     AddRangeSize);
            info.AddValue(nameof(MoveCount),        MoveCount);
            info.AddValue(nameof(RefreshCount),     RefreshCount);
            info.AddValue(nameof(RemoveCount),      RemoveCount);
            info.AddValue(nameof(RemoveRangeCount), RemoveRangeCount);
            info.AddValue(nameof(RemoveRangeSize),  RemoveRangeSize);
            info.AddValue(nameof(ReplaceCount),     ReplaceCount);
            info.AddValue(nameof(TotalItemCount),   TotalItemCount);
        }
        
        public override string ToString()
            => Name;
    }
}
