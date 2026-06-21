// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Aggregation;
#else

namespace DynamicData.Aggregation;
#endif

/// <summary>
/// Provides members for the AggregateEnumerator class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
internal sealed class AggregateEnumerator<T>(IChangeSet<T> source) : IAggregateChangeSet<T>
    where T : notnull
{
    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<AggregateItem<T>> GetEnumerator()
    {
        foreach (var change in source)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    yield return new AggregateItem<T>(AggregateType.Add, change.Item.Current);
                    break;

                case ListChangeReason.AddRange:
                    foreach (var item in change.Range)
                    {
                        yield return new AggregateItem<T>(AggregateType.Add, item);
                    }

                    break;

                case ListChangeReason.Replace:
                    yield return new AggregateItem<T>(AggregateType.Remove, change.Item.Previous.Value);
                    yield return new AggregateItem<T>(AggregateType.Add, change.Item.Current);
                    break;

                case ListChangeReason.Remove:
                    yield return new AggregateItem<T>(AggregateType.Remove, change.Item.Current);
                    break;

                case ListChangeReason.RemoveRange:
                case ListChangeReason.Clear:
                    foreach (var item in change.Range)
                    {
                        yield return new AggregateItem<T>(AggregateType.Remove, item);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Provides members for the AggregateEnumerator class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same name, different generics.")]
internal sealed class AggregateEnumerator<TObject, TKey>(IChangeSet<TObject, TKey> source) : IAggregateChangeSet<TObject>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly ChangeSet<TObject, TKey> _source = source.ToConcreteType();

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<AggregateItem<TObject>> GetEnumerator()
    {
        foreach (var change in _source)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    yield return new AggregateItem<TObject>(AggregateType.Add, change.Current);
                    break;

                case ChangeReason.Update:
                    yield return new AggregateItem<TObject>(AggregateType.Remove, change.Previous.Value);
                    yield return new AggregateItem<TObject>(AggregateType.Add, change.Current);
                    break;

                case ChangeReason.Remove:
                    yield return new AggregateItem<TObject>(AggregateType.Remove, change.Current);
                    break;

                default:
                    continue;
            }
        }
    }

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
