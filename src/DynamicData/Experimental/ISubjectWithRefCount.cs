// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;

namespace DynamicData.Experimental
{
    internal interface ISubjectWithRefCount<T> : ISubject<T>
    {
        /// <summary>number of subscribers.</summary>
        /// <value>
        /// The ref count.
        /// </value>
        int RefCount { get; }
    }
}
