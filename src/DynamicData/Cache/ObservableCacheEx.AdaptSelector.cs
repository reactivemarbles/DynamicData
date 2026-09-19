// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    // TODO: Apply the Adapter to more places

    /// <summary>
    /// Executes the AdaptSelector operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <typeparam name="TResult">The type of the TResult value.</typeparam>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    private static Func<TObject, TKey, TResult> AdaptSelector<TObject, TKey, TResult>(Func<TObject, TResult> other)
        where TObject : notnull
        where TKey : notnull
        where TResult : notnull => (obj, _) => other(obj);
}
