using System;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Internal
{
	internal class Transformer<TSource, TDestination>
	{
		private readonly IObservable<IChangeSet<TSource>> _source;
		private readonly Func<TSource, TDestination> _factory;
		private readonly ChangeAwareList<TDestination> _transformed = new ChangeAwareList<TDestination>();

		public Transformer([NotNull] IObservable<IChangeSet<TSource>> source, [NotNull] Func<TSource, TDestination> factory)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (factory == null) throw new ArgumentNullException("factory");
			_source = source;
			_factory = factory;
		}


		public IObservable<IChangeSet<TDestination>> Run()
		{
			return _source.Select(Process);
		}

		private IChangeSet<TDestination> Process(IChangeSet<TSource> changes)
		{
			_transformed.Transform(changes, _factory);
			return _transformed.CaptureChanges();
		}
	}
}