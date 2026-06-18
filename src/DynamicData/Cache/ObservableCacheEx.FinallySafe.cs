// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Obsolete: do not use. This can cause unhandled exception issues. Use the standard Rx <c>Finally</c> operator instead.
    /// </summary>
    /// <typeparam name="T">The type contained within the observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> to attach a finally action to.</param>
    /// <param name="finallyAction">The <see cref="Action"/> to invoke when the subscription terminates.</param>
    /// <returns>An observable which has always a finally action applied.</returns>
    [Obsolete("This can cause unhandled exception issues so do not use")]
    public static IObservable<T> FinallySafe<T>(this IObservable<T> source, Action finallyAction)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        finallyAction.ThrowArgumentNullExceptionIfNull(nameof(finallyAction));

        return new FinallySafe<T>(source, finallyAction).Run();
    }
}
