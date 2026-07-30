// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the DeferUntilLoaded class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class DeferUntilLoaded<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _result field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _result;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferUntilLoaded{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    public DeferUntilLoaded(IObservableCache<TObject, TKey> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        _result = source.CountChanged.Where(count => count != 0).Take(1).Select(_ => new ChangeSet<TObject, TKey>()).Concat(source.Connect()).NotEmpty();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferUntilLoaded{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    public DeferUntilLoaded(IObservable<IChangeSet<TObject, TKey>> source) => _result = source.MonitorStatus().Where(status => status == ConnectionStatus.Loaded).Take(1).Select(_ => new ChangeSet<TObject, TKey>()).Concat(source).NotEmpty();

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => _result;
}
