using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Diagnostics;
using DynamicData.Kernel;
using DynamicData.Tests.Operators;

namespace DynamicData.Tests
{
    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public class SortExpression<T>
    {
        private readonly SortDirection _direction;
        private readonly Func<T, IComparable> _expression;

        public SortExpression(Func<T, IComparable> expression, SortDirection direction = SortDirection.Ascending)
        {
            _expression = expression;
            _direction = direction;
        }

        public SortDirection Direction
        {
            get { return _direction; }
        }

        public Func<T, IComparable> Expression
        {
            get { return _expression; }
        }
    }

    public class SortExpressionComparer<T> : List<SortExpression<T>>, IComparer<T>
    {

        public int Compare(T x, T y)
        {
            foreach (var item in this)
            {
                var yValue = item.Expression(y);

                int result = item.Expression(x).CompareTo(yValue);
                if (result == 0)
                {
                    continue;
                }

                return (item.Direction == SortDirection.Ascending) ? result : -result;
            }
            return 0;
        }
    }

    public class TestSortedChangeSetResult<TObject, TKey> : IDisposable
    {
        private readonly IList<ISortedChangeSet<TObject, TKey>> _messages = new List<ISortedChangeSet<TObject, TKey>>();
        private ChangeSummary _summary;
        private Exception _error;
        private readonly IDisposable _disposer;

        private IList<TObject> _resultList = new List<TObject>();

        private readonly IObservableCache<TObject, TKey> _data;

        public TestSortedChangeSetResult(IObservable<ISortedChangeSet<TObject, TKey>> source)
        {
            var published = source.Publish();

            var error = published.Subscribe(updates => { }, ex => _error = ex);
            var results = published.Subscribe(updates => _messages.Add(updates));
            _data = published.AsObservableCache();
            var summariser = published.CollectUpdateStats().Subscribe(summary => _summary = summary);


            var connected = published.Connect();
            _disposer = Disposable.Create(() =>
                                              {
                                                  connected.Dispose();
                                                  summariser.Dispose();
                                                  results.Dispose();
                                                  error.Dispose();
                                              });
        }



        public IObservableCache<TObject, TKey> Data
        {
            get { return _data; }
        }

        public IList<ISortedChangeSet<TObject, TKey>> Messages
        {
            get { return _messages; }
        }

        public ChangeSummary Summary
        {
            get { return _summary; }
        }

        public Exception Error
        {
            get { return _error; }
        }

        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}