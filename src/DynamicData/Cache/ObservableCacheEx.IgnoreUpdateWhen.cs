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
    /// <summary>
    /// Ignores the update when the condition is met.
    /// The first parameter in the ignore function is the current value and the second parameter is the previous value.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to selectively suppress updates in.</param>
    /// <param name="ignoreFunction">The <see cref="Func{TObject, TObject, bool}"/> ignore function (current,previous)=>{ return true to ignore }.</param>
    /// <returns>An observable which emits change sets and ignores updates equal to the lambda.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> IgnoreUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject, bool> ignoreFunction)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Select(
            updates =>
            {
                var result = updates.Where(
                    u =>
                    {
                        if (u.Reason != ChangeReason.Update)
                        {
                            return true;
                        }

                        return !ignoreFunction(u.Current, u.Previous.Value);
                    });
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();
    }
}
