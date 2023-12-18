// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class TreeBuilder<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey> pivotOn, IObservable<Func<Node<TObject, TKey>, bool>>? predicateChanged)
    where TObject : class
    where TKey : notnull
{
    private readonly Func<TObject, TKey> _pivotOn = pivotOn ?? throw new ArgumentNullException(nameof(pivotOn));

    private readonly IObservable<Func<Node<TObject, TKey>, bool>> _predicateChanged = predicateChanged ?? Observable.Return(DefaultPredicate);

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    private static Func<Node<TObject, TKey>, bool> DefaultPredicate => node => node.IsRoot;

    public IObservable<IChangeSet<Node<TObject, TKey>, TKey>> Run() => Observable.Create<IChangeSet<Node<TObject, TKey>, TKey>>(
            observer =>
            {
                var locker = new object();
                var reFilterObservable = new BehaviorSubject<Unit>(Unit.Default);

                var allData = _source.Synchronize(locker).AsObservableCache();

                // for each object we need a node which provides
                // a structure to set the parent and children
                var allNodes = allData.Connect().Synchronize(locker).Transform((t, v) => new Node<TObject, TKey>(t, v)).AsObservableCache();

                var groupedByPivot = allNodes.Connect().Synchronize(locker).Group(x => _pivotOn(x.Item)).AsObservableCache();

                void UpdateChildren(Node<TObject, TKey> parentNode)
                {
                    var lookup = groupedByPivot.Lookup(parentNode.Key);
                    if (lookup.HasValue && lookup.Value is not null)
                    {
                        var children = lookup.Value.Cache.Items;
                        parentNode.Update(u => u.AddOrUpdate(children));
                        children.ForEach(x => x.Parent = parentNode);
                    }
                }

                // as nodes change, maintain parent and children
                var parentSetter = allNodes.Connect().Do(
                    changes =>
                    {
                        foreach (var group in changes.GroupBy(c => _pivotOn(c.Current.Item)))
                        {
                            var parentKey = group.Key;
                            var parent = allNodes.Lookup(parentKey);

                            if (!parent.HasValue)
                            {
                                // deal with items which have no parent
                                foreach (var change in group)
                                {
                                    if (change.Reason != ChangeReason.Refresh)
                                    {
                                        change.Current.Parent = null;
                                    }

                                    switch (change.Reason)
                                    {
                                        case ChangeReason.Add:
                                            UpdateChildren(change.Current);
                                            break;

                                        case ChangeReason.Update:
                                            {
                                                // copy children to the new node amd set parent
                                                var children = change.Previous.Value.Children.Items;
                                                change.Current.Update(updater => updater.AddOrUpdate(children));
                                                children.ForEach(child => child.Parent = change.Current);

                                                // remove from old parent if different
                                                var previous = change.Previous.Value;
                                                var previousParent = _pivotOn(previous.Item);

                                                if (previousParent is not null && !previousParent.Equals(previous.Key))
                                                {
                                                    allNodes.Lookup(previousParent).IfHasValue(n => n.Update(u => u.Remove(change.Key)));
                                                }

                                                break;
                                            }

                                        case ChangeReason.Remove:
                                            {
                                                // remove children and null out parent
                                                var children = change.Current.Children.Items;
                                                change.Current.Update(updater => updater.Remove(children));
                                                children.ForEach(child => child.Parent = null);

                                                break;
                                            }

                                        case ChangeReason.Refresh:
                                            {
                                                var previousParent = change.Current.Parent;
                                                if (!previousParent.Equals(parent))
                                                {
                                                    previousParent.IfHasValue(n => n.Update(u => u.Remove(change.Key)));
                                                    change.Current.Parent = null;
                                                }

                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                // deal with items have a parent
                                parent.Value.Update(
                                    updater =>
                                    {
                                        var p = parent.Value;

                                        foreach (var change in group)
                                        {
                                            var previous = change.Previous;
                                            var node = change.Current;
                                            var key = node.Key;

                                            switch (change.Reason)
                                            {
                                                case ChangeReason.Add:
                                                    {
                                                        // update the parent node
                                                        node.Parent = p;
                                                        updater.AddOrUpdate(node);
                                                        UpdateChildren(node);

                                                        break;
                                                    }

                                                case ChangeReason.Update:
                                                    {
                                                        // copy children to the new node amd set parent
                                                        var children = previous.Value.Children.Items;
                                                        change.Current.Update(u => u.AddOrUpdate(children));
                                                        children.ForEach(child => child.Parent = change.Current);

                                                        // check whether the item has a new parent
                                                        var previousItem = previous.Value.Item;
                                                        var previousKey = previous.Value.Key;
                                                        var previousParent = _pivotOn(previousItem);

                                                        if (previousParent is not null && !previousParent.Equals(previousKey))
                                                        {
                                                            allNodes.Lookup(previousParent).IfHasValue(n => n.Update(u => u.Remove(key)));
                                                        }

                                                        // finally update the parent
                                                        node.Parent = p;
                                                        updater.AddOrUpdate(node);

                                                        break;
                                                    }

                                                case ChangeReason.Remove:
                                                    {
                                                        node.Parent = null;
                                                        updater.Remove(key);

                                                        var children = node.Children.Items;
                                                        change.Current.Update(u => u.Remove(children));
                                                        children.ForEach(child => child.Parent = null);

                                                        break;
                                                    }

                                                case ChangeReason.Refresh:
                                                    {
                                                        var previousParent = change.Current.Parent;
                                                        if (!previousParent.Equals(parent))
                                                        {
                                                            previousParent.IfHasValue(n => n.Update(u => u.Remove(change.Key)));
                                                            change.Current.Parent = p;
                                                            updater.AddOrUpdate(change.Current);
                                                        }

                                                        break;
                                                    }
                                            }
                                        }
                                    });
                            }
                        }

                        reFilterObservable.OnNext(Unit.Default);
                    }).DisposeMany().Subscribe();

                var filter = _predicateChanged.Synchronize(locker).CombineLatest(reFilterObservable, (predicate, _) => predicate);
                var result = allNodes.Connect().Filter(filter).SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        result.Dispose();
                        parentSetter.Dispose();
                        allData.Dispose();
                        allNodes.Dispose();
                        groupedByPivot.Dispose();
                        reFilterObservable.OnCompleted();
                    });
            });
}
