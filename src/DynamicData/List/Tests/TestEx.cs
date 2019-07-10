// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests
{
    /// <summary>
    /// Test extensions
    /// </summary>
    public static class ListTextEx
    {
        /// <summary>
        /// Aggregates all events and statistics for a changeset to help assertions when testing
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns></returns>
        public static ChangeSetAggregator<T> AsAggregator<T>(this IObservable<IChangeSet<T>> source)
        {
            return new ChangeSetAggregator<T>(source);
        }
    }
}
