using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal sealed class DistinctCalculator<TObject, TKey, TValue>
    {
        private readonly ParallelisationOptions _parallelisationOptions;
        private readonly Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();
        private readonly object _locker = new object();
        private readonly Func<TObject, TValue> _valueSelector;
        private readonly HashSet<TValue> _values = new HashSet<TValue>();

        public DistinctCalculator(Func<TObject, TValue> valueSelector, ParallelisationOptions parallelisationOptions=null)
        {
            _parallelisationOptions = parallelisationOptions ?? ParallelisationOptions.None;
            _valueSelector = valueSelector;
        }


        public IDistinctChangeSet<TValue> Calculate(IChangeSet<TObject, TKey> updates)
        {
            DistinctChangeSet<TValue> changes;
            lock (_locker)
            {
                _cache.Clone(updates);

                //TODO: abstract this class and inherit for proper platform enlightenment
                #if !SILVERLIGHT && !PORTABLE && !PORTABLE40
                        var current = _cache.Items.Parallelise(_parallelisationOptions).Select(i => _valueSelector(i)).Distinct().ToHashSet();
                #else
                        var current = _cache.Items.Select(i => _valueSelector(i)).Distinct().ToHashSet();
                #endif
                HashSet<TValue> previous = _values;

                //maintain
                var additions = current.Except(previous).Select(v => new Change<TValue,TValue>(ChangeReason.Add, v,v));
                var removals = previous.Except(current).Select(v =>new Change<TValue,TValue>(ChangeReason.Remove, v,v));
                changes = new DistinctChangeSet<TValue>(additions.Union(removals));

                changes.ForEach(change =>
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                _values.Add(change.Current);
                                break;
                            case ChangeReason.Remove:
                                _values.Remove(change.Current);
                                break;
                        }
                    });
            }

            return changes;
        }
    }
}