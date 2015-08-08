using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;
using DynamicData.Linq;

namespace DynamicData.Internal
{
	internal sealed class GroupOn<TObject, TGroupKey>
	{
		private readonly IObservable<IChangeSet<TObject>> _source;
		private readonly Func<TObject, TGroupKey> _groupSelector;
		private readonly ChangeAwareList<IGroup<TObject, TGroupKey>> _groupings = new ChangeAwareList<IGroup<TObject, TGroupKey>>();
		private readonly IDictionary<TGroupKey, Group<TObject,  TGroupKey>> _groupCache = new Dictionary<TGroupKey, Group<TObject, TGroupKey>>();

		public GroupOn([NotNull] IObservable<IChangeSet<TObject>> source, [NotNull] Func<TObject, TGroupKey> groupSelector)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));
			_source = source;
			_groupSelector = groupSelector;
		}

		public IObservable<IChangeSet<IGroup<TObject, TGroupKey>>> Run()
		{
			 return _source.Transform(t => new ItemWithValue<TObject, TGroupKey>(t, _groupSelector(t)))
							.Select(Process)
							.DisposeMany() //dispose removes as the grouping is disposable
							.NotEmpty(); 
		}


		private IChangeSet<IGroup<TObject, TGroupKey>> Process(IChangeSet<ItemWithValue<TObject, TGroupKey>> changes)
		{
			//TODO.This flattened enumerator is inefficient as range operations are lost.
			//maybe can infer within each grouping whether we can regroup i.e. Another enumerator!!!
			//var enumerator = new UnifiedChangeEnumerator<ItemWithValue<TObject, TGroupKey>>(changes);

            changes.Unified()
                .GroupBy(change => change.Current.Value)
				.ForEach(grouping =>
				{
					//lookup group and if created, add to result set
					var currentGroup = grouping.Key;
					var lookup = GetCache(currentGroup);
					var groupCache = lookup.Group;

					if (lookup.WasCreated)
						_groupings.Add(groupCache);

					//start a group edit session, so all changes are batched
					groupCache.Edit(
						list =>
						{
							//iterate through the group's items and process
							grouping.ForEach(change =>
							{
								switch (change.Reason)
								{
									case ListChangeReason.Add:
										list.Add(change.Current.Item);
										break;
									case ListChangeReason.Replace:
									{
										var previousItem = change.Previous.Value.Item;
										var previousGroup = change.Previous.Value.Value;

										//check whether an item changing has resulted in a different group
										if (previousGroup.Equals(currentGroup))
										{
											//find and replace
											var index = list.IndexOf(previousItem);
											list[index] = change.Current.Item;
										}
										else
										{
											//add to new group
											list.Add(change.Current.Item);

											//remove from old group
											_groupCache.Lookup(previousGroup)
												.IfHasValue(g =>
												{
													g.Edit(oldList=> oldList.Remove(previousItem));
													if (g.List.Count != 0) return;
													_groupCache.Remove(g.GroupKey);
													_groupings.Remove(g);
												});
										}
									}
										break;
									case ListChangeReason.Remove:
										list.Remove(change.Current.Item);
										break;
                                    case ListChangeReason.Clear:
                                        list.Clear();
                                        break;

                                }
							});
						});

					if (groupCache.List.Count == 0)
					{
						_groupCache.Remove(groupCache.GroupKey);
						_groupings.Remove(groupCache);
					}
				});
			return _groupings.CaptureChanges();
		}

		private GroupWithAddIndicator GetCache(TGroupKey key)
		{
			var cache = _groupCache.Lookup(key);
			if (cache.HasValue)
				return new GroupWithAddIndicator(cache.Value, false);
		
			var newcache = new Group<TObject, TGroupKey>(key);
			_groupCache[key] = newcache;
			return new GroupWithAddIndicator(newcache, true);
		}

		private class GroupWithAddIndicator
		{
			public Group<TObject, TGroupKey> Group { get;  }
			public bool WasCreated { get;  }

			public GroupWithAddIndicator(Group<TObject, TGroupKey> @group, bool wasCreated)
			{
				Group = @group;
				WasCreated = wasCreated;
			}
		}

	}
}