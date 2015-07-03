using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Controllers;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal class TreeBuilder<TObject, TKey>
        where TObject: class
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TKey> _pivotOn;

        public TreeBuilder([NotNull] IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject,TKey> pivotOn)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pivotOn == null) throw new ArgumentNullException(nameof(pivotOn));

            _source = source;
            _pivotOn = pivotOn;
        }

        public IObservable<IChangeSet<Node<TObject, TKey>, TKey>> Run()
        {
            return Observable.Create<IChangeSet<Node<TObject, TKey>, TKey>>(observer =>
            {
                var locker = new object();
                var filter = new FilterController<Node<TObject, TKey>>();
                filter.Change(node=>node.IsRoot);

                var allData = _source.Synchronize(locker).AsObservableCache();

                var allNodes = allData.Connect()
                    .Synchronize(locker)
                    .Transform((t, v) => new Node<TObject, TKey>(t, v))
                    .AsObservableCache(); ;

                var parentSetter = allNodes.Connect()
                    .Subscribe(changes =>
                    {
                        var grouped = changes.GroupBy(c => _pivotOn(c.Current.Item));

                        grouped.ForEach(group =>
                        {
                            var parentKey = group.Key;
                            var parent = allNodes.Lookup(parentKey);

                            if (!parent.HasValue)
                            {
                                group.ForEach(change =>
                                {
                                    change.Current.Parent = null;
                                    switch (change.Reason)
                                    {
                                        case ChangeReason.Add:
                                            //check for orphaned children (iterate at end) 
                                            break;
                                        case ChangeReason.Update:
                                        {
                                            //copy children to the new node amd set parent
                                            var children = change.Previous.Value.Children.Items;
                                            change.Current.Update(updater => updater.AddOrUpdate(children));
                                            children.ForEach(child => child.Parent = change.Current);
                                        }
                                            break;
                                        case ChangeReason.Remove:
                                        case ChangeReason.Clear:
                                        {
                                            //remove children and null out parent
                                            var children = change.Current.Children.Items;
                                            change.Current.Update(updater => updater.AddOrUpdate(children));
                                            children.ForEach(child => child.Parent = null);
                                        }
                                            break;
                                    }
                                });
                            }
                            else
                            {
                                parent.Value.Update(updater =>
                                {
                                    var p = parent.Value;

                                    group.ForEach(change =>
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
                                                updater.AddOrUpdate(p);
                                            }
                                                break;
                                            case ChangeReason.Update:
                                            {
                                                //copy children to the new node amd set parent
                                                var children = previous.Value.Children.Items;
                                                change.Current.Update(u => u.AddOrUpdate(children));
                                                children.ForEach(child => child.Parent = change.Current);

                                                //check whether the item has a new parent
                                                var previousItem = previous.Value.Item;
                                                var previousKey = previous.Value.Key;
                                                var previousParent = _pivotOn(previousItem);

                                                if (!previousParent.Equals(previousKey))
                                                {
                                                    allNodes.Lookup(previousParent)
                                                        .IfHasValue(n =>
                                                        {
                                                            n.Update(u=>u.Remove(previousKey));
                                                        });
                                                }

                                                //finally update the parent
                                                node.Parent = p;
                                                updater.AddOrUpdate(node);
                                            }

                                                break;
                                            case ChangeReason.Remove:
                                            case ChangeReason.Clear:
                                            {
                                                node.Parent = null;
                                                updater.Remove(key);
                                            }
                                            break;
                                        }
                                    });
                                });
                            }
                        });

                        filter.Reevaluate();
                    });

                var result = allNodes.Connect(filter).SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    result.Dispose();
                    parentSetter.Dispose();
                    allData.Dispose();
                    allNodes.Dispose();
                    filter.Dispose();
                });
            });
        } 
    }
}