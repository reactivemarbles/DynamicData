using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Internal
{
	internal class QueryWhenChanged<T>
	{
		private readonly IObservable<IChangeSet<T>> _source;
		private readonly List<T> _list = new List<T>();

		public QueryWhenChanged([NotNull] IObservable<IChangeSet<T>> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			_source = source;
		}


		public IObservable<IList<T>> Run()
		{
			return _source.Do(_list.Clone).Select(_=>new ReadOnlyCollection<T>(_list));
		}
	}
}