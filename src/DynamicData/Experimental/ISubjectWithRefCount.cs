// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;

namespace DynamicData.Experimental;

/// <summary>
/// A subject which also contains its current reference count.
/// </summary>
/// <typeparam name="T">The type of item.</typeparam>
internal interface ISubjectWithRefCount<T> : ISubject<T>
{
    /// <summary>Gets number of subscribers.</summary>
    /// <value>
    /// The ref count.
    /// </value>
    int RefCount { get; }
}
