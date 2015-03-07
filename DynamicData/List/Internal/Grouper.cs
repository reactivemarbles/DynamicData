using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal sealed class Grouper<TObject, TGroupKey>
	{

		private readonly ChangeAwareCollection<IGroup<TObject, TGroupKey>> _groupings = new ChangeAwareCollection<IGroup<TObject, TGroupKey>>();
		private readonly IDictionary<TGroupKey, Group<TObject,  TGroupKey>> _groupCache = new Dictionary<TGroupKey, Group<TObject, TGroupKey>>();

		public IChangeSet<IGroup<TObject, TGroupKey>> Process(IChangeSet<ItemWithValue<TObject, TGroupKey>> changes)
		{
			changes
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
									case ChangeReason.Add:
										list.Add(change.Current.Item);
										break;
									case ChangeReason.Update:
									{
										var previousItem = change.Previous.Value.Item;
										var previousGroup = change.Previous.Value.Value;


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
									case ChangeReason.Remove:
										list.Remove(change.Current.Item);
										break;
									case ChangeReason.Evaluate:
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

		private GroupAndAddIndicator GetCache(TGroupKey key)
		{
			var cache = _groupCache.Lookup(key);
			if (cache.HasValue)
				return new GroupAndAddIndicator(cache.Value, false);
		
			var newcache = new Group<TObject, TGroupKey>(key);
			_groupCache[key] = newcache;
			return new GroupAndAddIndicator(newcache, true);
		}

		private class GroupAndAddIndicator
		{
			public Group<TObject, TGroupKey> Group { get;  }
			public bool WasCreated { get;  }

			public GroupAndAddIndicator(Group<TObject, TGroupKey> @group, bool wasCreated)
			{
				Group = @group;
				WasCreated = wasCreated;
			}
		}

	}
}