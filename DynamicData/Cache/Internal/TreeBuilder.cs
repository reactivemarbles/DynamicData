using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Controllers;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class TreeBuilder<TObject, TKey>
        where TObject : class
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TKey> _pivotOn;

        public TreeBuilder([NotNull] IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, TKey> pivotOn)
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
                filter.Change(node => node.IsRoot);

                var allData = _source.Synchronize(locker).AsObservableCache();

                //for each object we need a node which provides
                //a structure to set the parent and children
                var allNodes = allData.Connect()
                                      .Synchronize(locker)
                                      .Transform((t, v) => new Node<TObject, TKey>(t, v))
                                      .DisposeMany()
                                      .AsObservableCache();

                //as nodes change, maintain parent and children
                var parentSetter = allNodes.Connect()
                                           .Subscribe(changes =>
                                           {
                                               var grouped = changes.GroupBy(c => _pivotOn(c.Current.Item));

                                               foreach (var group in grouped)
                                               {
                                                   var parentKey = group.Key;
                                                   var parent = allNodes.Lookup(parentKey);

                                                   if (!parent.HasValue)
                                                   {
                                                       //deal with items which have no parent
                                                       foreach (var change in group)
                                                       {
                                                           change.Current.Parent = null;
                                                           switch (change.Reason)
                                                           {
                                                               case ChangeReason.Add:
                                                                   break;
                                                               case ChangeReason.Update:
                                                                   {
                                                                       //copy children to the new node amd set parent
                                                                       var children = change.Previous.Value.Children.Items;
                                                                       change.Current.Update(updater => updater.AddOrUpdate(children));
                                                                       children.ForEach(child => child.Parent = change.Current);

                                                                       //remove from old parent if different
                                                                       var previous = change.Previous.Value;
                                                                       var previousParent = _pivotOn(previous.Item);

                                                                       if (!previousParent.Equals(previous.Key))
                                                                       {
                                                                           allNodes.Lookup(previousParent)
                                                                                   .IfHasValue(n => { n.Update(u => u.Remove(change.Key)); });
                                                                       }

                                                                       break;
                                                                   }
                                                               case ChangeReason.Remove:
                                                                   {
                                                                       //remove children and null out parent
                                                                       var children = change.Current.Children.Items;
                                                                       change.Current.Update(updater => updater.Remove(children));
                                                                       children.ForEach(child => child.Parent = null);

                                                                       break;
                                                                   }
                                                           }
                                                       }
                                                   }
                                                   else
                                                   {
                                                       //deal with items have a parent
                                                       parent.Value.Update(updater =>
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

                                                                           break;
                                                                       }
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
                                                                                       .IfHasValue(n => { n.Update(u => u.Remove(key)); });
                                                                           }

                                                                           //finally update the parent
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
                                                               }
                                                           }
                                                       });
                                                   }
                                               }

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
