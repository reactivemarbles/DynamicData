// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Diagnostics;

/// <summary>
/// Accumulates change statics.
/// </summary>
public class ChangeSummary
{
    /// <summary>
    /// An empty instance of change summary.
    /// </summary>
    public static readonly ChangeSummary Empty = new();

    private readonly int _index;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChangeSummary"/> class.
    /// </summary>
    /// <param name="index">The index of the change.</param>
    /// <param name="latest">The latest statistics.</param>
    /// <param name="overall">The overall statistics.</param>
    public ChangeSummary(int index, ChangeStatistics latest, ChangeStatistics overall)
    {
        Latest = latest;
        Overall = overall;
        _index = index;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChangeSummary"/> class.
    /// </summary>
    private ChangeSummary()
    {
        _index = -1;
        Latest = new ChangeStatistics();
        Overall = new ChangeStatistics();
    }

    /// <summary>
    /// Gets the latest change.
    /// </summary>
    /// <value>
    /// The latest.
    /// </value>
    public ChangeStatistics Latest { get; }

    /// <summary>
    /// Gets the overall change count.
    /// </summary>
    /// <value>
    /// The overall.
    /// </value>
    public ChangeStatistics Overall { get; }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ChangeSummary change && Equals(change);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = _index;
            hashCode = (hashCode * 397) ^ Latest.GetHashCode();
            hashCode = (hashCode * 397) ^ Overall.GetHashCode();
            return hashCode;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"CurrentIndex: {_index}, Latest Count: {Latest.Count}, Overall Count: {Overall.Count}";

    private bool Equals(ChangeSummary other) => _index == other._index && Equals(Latest, other.Latest) && Equals(Overall, other.Overall);
}
