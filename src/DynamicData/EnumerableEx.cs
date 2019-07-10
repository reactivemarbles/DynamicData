// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData
{
    /// <summary>
    /// Extensions for dynamic data
    /// </summary>
    [PublicAPI]
    public static class EnumerableEx
    {
        #region Populate changeset from standard enumerable

        /// <summary>
        /// Converts the enumerable to an observable changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <param name="completable">Optionally emmit an OnComplete</param>
        /// <returns>An observable changeset</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> AsObservableChangeSet<TObject, TKey>(this IEnumerable<TObject> source,
            Func<TObject, TKey> keySelector,
            bool completable = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return Observable.Create<IChangeSet<TObject, TKey>>(obs =>
            {
                var changes = source.Select(x => new Change<TObject, TKey>(ChangeReason.Add, keySelector(x), x));
                var changeSet = new ChangeSet<TObject, TKey>(changes);
                obs.OnNext(changeSet);
                if (completable)
                {
                    obs.OnCompleted();
                }

                return Disposable.Empty;
            });
        }

        #endregion

        #region Populate change set from standard enumerable

        /// <summary>
        /// Converts the enumerable to an observable changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="completable">Optionally emmit an OnComplete</param>
        /// <returns>An observable change set</returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject>> AsObservableChangeSet<TObject>(this IEnumerable<TObject> source,
            bool completable = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return Observable.Create<IChangeSet<TObject>>(obs =>
            {
                var changes = source.Select(x => new Change<TObject>(ListChangeReason.Add, x));
                var changeSet = new ChangeSet<TObject>(changes);
                obs.OnNext(changeSet);
                if (completable)
                {
                    obs.OnCompleted();
                }

                return Disposable.Empty;
            });
        }

        #endregion
    }
}
