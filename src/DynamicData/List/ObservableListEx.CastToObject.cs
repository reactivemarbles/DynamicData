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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Casts each item in the changeset to <c>object</c>. Typically used before <see cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/> to work around type inference limitations.
    /// </summary>
    /// <typeparam name="T">The source item type (must be a reference type).</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to cast to object.</param>
    /// <returns>A list changeset stream of <c>object</c> items.</returns>
    /// <seealso cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/>
    public static IObservable<IChangeSet<object>> CastToObject<T>(this IObservable<IChangeSet<T>> source)
        where T : class
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        return source.Select(changes => changes.Transform(t => (object)t));
    }
}
