// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Diagnostics;
#else

namespace DynamicData.Diagnostics;
#endif

/// <summary>
///     Object used to capture accumulated changes.
/// </summary>
/// <param name="Index">     Gets the index. </param>
/// <param name="Adds">     Gets the adds. </param>
/// <param name="Updates">     Gets the updates. </param>
/// <param name="Removes">     Gets the removes. </param>
/// <param name="Refreshes">     Gets the refreshes. </param>
/// <param name="Moves">     Gets the moves. </param>
/// <param name="Count">     Gets the count. </param>
public record ChangeStatistics(int Index, int Adds, int Updates, int Removes, int Refreshes, int Moves, int Count)
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChangeStatistics"/> class.
    /// </summary>
    public ChangeStatistics()
        : this(-1, default, default, default, default, default, default) => Index = -1;

    /// <summary>
    ///     Gets the last updated.
    /// </summary>
    /// <value>
    ///     The last updated.
    /// </value>
    public DateTime LastUpdated { get; } = DateTime.Now;

    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Adds;
            hashCode = (hashCode * 397) ^ Updates;
            hashCode = (hashCode * 397) ^ Removes;
            hashCode = (hashCode * 397) ^ Refreshes;
            hashCode = (hashCode * 397) ^ Moves;
            hashCode = (hashCode * 397) ^ Count;
            hashCode = (hashCode * 397) ^ Index;
            hashCode = (hashCode * 397) ^ LastUpdated.GetHashCode();
            return hashCode;
        }
    }

    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"CurrentIndex: {Index}, Adds: {Adds}, Updates: {Updates}, Removes: {Removes}, Refreshes: {Refreshes}, Count: {Count}, Timestamp: {LastUpdated}";
}
