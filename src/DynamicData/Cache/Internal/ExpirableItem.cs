// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Represents the ExpirableItem record.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="Value">The Value value.</param>
/// <param name="Key">The Key value.</param>
/// <param name="ExpireAt">The ExpireAt value.</param>
/// <param name="Index">The Index value.</param>
internal readonly record struct ExpirableItem<TObject, TKey>(TObject Value, TKey Key, DateTime ExpireAt, long Index = 0)
{
    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            return (Key is null ? 0 : EqualityComparer<TKey>.Default.GetHashCode(Key) * 397) ^ ExpireAt.GetHashCode();
        }
    }

    /// <summary>
    /// Executes the ToString operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"Key: {Key}, Expire At: {ExpireAt}";
}
