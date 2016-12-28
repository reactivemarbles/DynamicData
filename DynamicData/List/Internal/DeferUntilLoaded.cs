using System;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class DeferUntilLoaded<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;

        public DeferUntilLoaded([NotNull] IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return _source.MonitorStatus()
                          .Where(status => status == ConnectionStatus.Loaded)
                          .Take(1)
                          .Select(_ => new ChangeSet<T>())
                          .Concat(_source)
                          .NotEmpty();
        }
    }
}
