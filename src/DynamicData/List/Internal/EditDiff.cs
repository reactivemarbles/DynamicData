using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class EditDiff<T>
    {
        private readonly ISourceList<T> _source;
        private readonly IEqualityComparer<T> _equalityComparer;

        public EditDiff([NotNull] ISourceList<T> source, IEqualityComparer<T> equalityComparer)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
        }

        public void Edit(IEnumerable<T> items)
        {
            _source.Edit(innerList =>
            {
                var originalItems = innerList.AsArray();
                var newItems = items.AsArray();

                var removes = originalItems.Except(newItems, _equalityComparer);
                var adds = newItems.Except(originalItems, _equalityComparer);

                innerList.Remove(removes);
                innerList.AddRange(adds);
            });
        }
    }
}
