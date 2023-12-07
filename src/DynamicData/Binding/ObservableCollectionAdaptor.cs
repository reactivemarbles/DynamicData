// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

namespace DynamicData.Binding;

/// <summary>
/// Adaptor to reflect a change set into an observable list.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ObservableCollectionAdaptor{T}"/> class.
/// </remarks>
/// <param name="collection">The collection.</param>
/// <param name="refreshThreshold">The number of changes before a Reset event is used.</param>
/// <param name="allowReplace"> Use replace instead of remove / add for updates. </param>
/// <param name="resetOnFirstTimeLoad"> Should a reset be fired for a first time load.This option is due to historic reasons where a reset would be fired for the first time load regardless of the number of changes.</param>
/// <exception cref="System.ArgumentNullException">collection.</exception>
public class ObservableCollectionAdaptor<T>(IObservableCollection<T> collection, int refreshThreshold,
#pragma warning disable CS9113 // Parameter is unread.
    bool allowReplace = true,
#pragma warning restore CS9113 // Parameter is unread.
    bool resetOnFirstTimeLoad = true) : IChangeSetAdaptor<T>

    where T : notnull
{
    private readonly IObservableCollection<T> _collection = collection ?? throw new ArgumentNullException(nameof(collection));
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionAdaptor{TObject}"/> class.
    /// </summary>
    /// <param name="collection">The collection.</param>
    public ObservableCollectionAdaptor(IObservableCollection<T> collection)
        : this(collection, DynamicDataOptions.Binding)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionAdaptor{TObject}"/> class.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="options"> The binding options.</param>
    public ObservableCollectionAdaptor(IObservableCollection<T> collection, BindingOptions options)
        : this(collection, options.ResetThreshold, options.UseReplaceForUpdates, options.ResetOnFirstTimeLoad)
    {
    }

    /// <summary>
    /// Maintains the specified collection from the changes.
    /// </summary>
    /// <param name="changes">The changes.</param>
    public void Adapt(IChangeSet<T> changes)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        if (changes.TotalChanges - changes.Refreshes > refreshThreshold || (!_loaded && resetOnFirstTimeLoad))
        {
            using (_collection.SuspendNotifications())
            {
                _collection.Clone(changes);
                _loaded = true;
            }
        }
        else
        {
            // TODO: pass in allowReplace to handle replace vs remove / add
            _collection.Clone(changes);
        }
    }
}

/// <summary>
/// Represents an adaptor which is used to update observable collection from
/// a change set stream.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ObservableCollectionAdaptor{TObject, TKey}"/> class.
/// </remarks>
/// <param name="refreshThreshold">The threshold before a reset notification is triggered.</param>
/// <param name="useReplaceForUpdates"> Use replace instead of remove / add for updates. </param>
/// <param name="resetOnFirstTimeLoad"> Should a reset be fired for a first time load.This option is due to historic reasons where a reset would be fired for the first time load regardless of the number of changes.</param>
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, only generic difference.")]
public class ObservableCollectionAdaptor<TObject, TKey>(int refreshThreshold = 25, bool useReplaceForUpdates = true, bool resetOnFirstTimeLoad = true) : IObservableCollectionAdaptor<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Cache<TObject, TKey> _cache = new();
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionAdaptor{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="options"> The binding options.</param>
    public ObservableCollectionAdaptor(BindingOptions options)
        : this(options.ResetThreshold, options.UseReplaceForUpdates, options.ResetOnFirstTimeLoad)
    {
    }

    /// <summary>
    /// Maintains the specified collection from the changes.
    /// </summary>
    /// <param name="changes">The changes.</param>
    /// <param name="collection">The collection.</param>
    public void Adapt(IChangeSet<TObject, TKey> changes, IObservableCollection<TObject> collection)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));
        collection.ThrowArgumentNullExceptionIfNull(nameof(collection));

        _cache.Clone(changes);

        if (changes.Count - changes.Refreshes > refreshThreshold || (!_loaded && resetOnFirstTimeLoad))
        {
            _loaded = true;
            using (collection.SuspendNotifications())
            {
                collection.Load(_cache.Items);
            }
        }
        else
        {
            using (collection.SuspendCount())
            {
                DoUpdate(changes, collection);
            }
        }
    }

    private void DoUpdate(IChangeSet<TObject, TKey> changes, IObservableCollection<TObject> list)
    {
        foreach (var change in changes.ToConcreteType())
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    list.Add(change.Current);
                    break;

                case ChangeReason.Remove:
                    list.Remove(change.Current);
                    break;

                case ChangeReason.Update:

                    // Remove / Add is default as some platforms do not support list[index] = XXX notifications.
                    if (useReplaceForUpdates)
                    {
                        list.Replace(change.Previous.Value, change.Current);
                    }
                    else
                    {
                        list.Remove(change.Previous.Value);
                        list.Add(change.Current);
                    }

                    break;
            }
        }
    }
}
