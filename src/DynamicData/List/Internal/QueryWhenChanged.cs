// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the QueryWhenChanged class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
internal sealed class QueryWhenChanged<T>(IObservable<IChangeSet<T>> source)
    where T : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IReadOnlyCollection<T>> Run() => Observable.Create<IReadOnlyCollection<T>>(observer =>
                                                             {
                                                                 var list = new List<T>();

                                                                 return _source.Subscribe(changes =>
                                                                 {
                                                                     list.Clone(changes);
                                                                     observer.OnNext(new ReadOnlyCollectionLight<T>(list));
                                                                 });
                                                             });
}
