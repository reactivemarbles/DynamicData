// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the FilterStatic class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="predicate">The predicate value.</param>
internal sealed class FilterStatic<T>(IObservable<IChangeSet<T>> source, Func<T, bool> predicate)
    where T : notnull
{
    /// <summary>
    /// The _predicate field.
    /// </summary>
    private readonly Func<T, bool> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Run() => Observable.Defer(() => _source.Scan(
                                                            new ChangeAwareList<T>(),
                                                            (state, changes) =>
                                                            {
                                                                Process(state, changes);
                                                                return state;
                                                            }).Select(filtered => filtered.CaptureChanges()).NotEmpty());

    /// <summary>
    /// Executes the Process operation.
    /// </summary>
    /// <param name="filtered">The filtered value.</param>
    /// <param name="changes">The changes value.</param>
    private void Process(ChangeAwareList<T> filtered, IChangeSet<T> changes)
    {
        foreach (var item in changes)
        {
            switch (item.Reason)
            {
                case ListChangeReason.Add:
                    {
                        var change = item.Item;
                        if (_predicate(change.Current))
                        {
                            filtered.Add(change.Current);
                        }

                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var matches = item.Range.Where(t => _predicate(t)).ToList();
                        filtered.AddRange(matches);
                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var change = item.Item;
                        var match = _predicate(change.Current);
                        if (match)
                        {
                            filtered.ReplaceOrAdd(change.Previous.Value, change.Current);
                        }
                        else
                        {
                            filtered.Remove(change.Previous.Value);
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        filtered.Remove(item.Item.Current);
                        break;
                    }

                case ListChangeReason.RemoveRange:
                    {
                        filtered.RemoveMany(item.Range);
                        break;
                    }

                case ListChangeReason.Clear:
                    {
                        filtered.ClearOrRemoveMany(item);
                        break;
                    }
            }
        }
    }
}
