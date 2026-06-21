// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

internal readonly record struct ExpirableItem<TObject, TKey>(TObject Value, TKey Key, DateTime ExpireAt, long Index = 0)
{
    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Key is null ? 0 : EqualityComparer<TKey>.Default.GetHashCode(Key) * 397) ^ ExpireAt.GetHashCode();
        }
    }

    public override string ToString() => $"Key: {Key}, Expire At: {ExpireAt}";
}
