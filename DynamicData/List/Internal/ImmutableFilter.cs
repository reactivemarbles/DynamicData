using System;

namespace DynamicData.Internal
{
	internal class ImmutableFilter<T>
	{
		private readonly Func<T, bool> _predicate;

		public ImmutableFilter(Func<T, bool> predicate)
		{
			if (predicate == null) throw new ArgumentNullException("predicate");
			_predicate = predicate;
		}

		private readonly ChangeAwareCollection<T> _filtered = new ChangeAwareCollection<T>();


		public IChangeSet<T> Process(IChangeSet<T> changes)
		{
			_filtered.Filter(changes, _predicate);
			return _filtered.CaptureChanges();
		}
	}
}