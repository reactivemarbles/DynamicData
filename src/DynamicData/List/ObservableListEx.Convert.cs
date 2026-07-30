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
    /// <para>Convert the object using the specified conversion function.</para>
    /// <para>This is a lighter equivalent of Transform and is designed to be used with non-disposable objects.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to convert.</param>
    /// <param name="conversionFactory">The <c>Func&lt;T, TResult&gt;</c> conversion factory.</param>
    /// <returns>An observable which emits the change set.</returns>
    [Obsolete("Prefer Cast as it is does the same thing but is semantically correct")]
    public static IObservable<IChangeSet<TDestination>> Convert<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TDestination> conversionFactory)
        where TObject : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(conversionFactory);

        return source.Select(changes => changes.Transform(conversionFactory));
    }
}
