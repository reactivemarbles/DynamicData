// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

namespace DynamicData.Cache.Internal;

/// <summary>Initializes a new instance of the <see cref="RemoveKeyEnumerator{TObject, TKey}"/> class.Converts a <see cref="Change{TObject, TKey}"/> to <see cref="ChangeSet{TObject}"/>.</summary>
/// <param name="source">The change set with a key.</param>
/// <param name="list">
/// An optional list, if provided it allows the refresh from a key based cache to find the index for the resulting list based refresh.
/// If not provided a refresh will dropdown to a replace which may ultimately result in a remove+add change downstream.
/// </param>
internal sealed class RemoveKeyEnumerator<TObject, TKey>(IChangeSet<TObject, TKey> source, IExtendedList<TObject>? list = null) : IEnumerable<Change<TObject>>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IChangeSet<TObject, TKey> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    /// A <see cref="IEnumerator{T}" /> that can be used to iterate through the collection.
    /// </returns>
    public IEnumerator<Change<TObject>> GetEnumerator()
    {
        foreach (var change in _source.ToConcreteType())
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    yield return new Change<TObject>(ListChangeReason.Add, change.Current, change.CurrentIndex);
                    break;

                case ChangeReason.Refresh:
                    // Refresh needs an index, which we don't have in a Change<T, K> model since it's key based.
                    // See: DynamicData > Binding > ObservableCollectionAdaptor.cs Line 129-130

                    // Note: A refresh is not index based within the context of a sorted change.
                    // Thus, currentIndex will not be available here where as other changes like add and remove do have indexes if coming from a sorted change set.

                    // In order to properly handle a refresh and map to an index on a list, we need to use the source list (within the edit method so that it's thread safe)
                    var index = list?.IndexOf(change.Current);
                    if (index >= 0)
                    {
                        yield return new Change<TObject>(ListChangeReason.Refresh, change.Current, index.Value);
                    }
                    else
                    {
                        // Fallback to a replace if a list is not available
                        yield return new Change<TObject>(ListChangeReason.Replace, change.Current, change.Current);
                    }

                    break;

                case ChangeReason.Moved:
                    // Move is always sorted
                    yield return new Change<TObject>(change.Current, change.CurrentIndex, change.PreviousIndex);
                    break;

                case ChangeReason.Update:
                    yield return new Change<TObject>(ListChangeReason.Remove, change.Previous.Value, change.PreviousIndex);
                    yield return new Change<TObject>(ListChangeReason.Add, change.Current, change.CurrentIndex);
                    break;

                case ChangeReason.Remove:
                    yield return new Change<TObject>(ListChangeReason.Remove, change.Current, change.CurrentIndex);
                    break;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
