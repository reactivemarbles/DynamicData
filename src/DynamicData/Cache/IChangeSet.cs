// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// <para>A collection of changes with some arbitrary additional context.</para>
/// <para>Changes are always published in the order.</para>
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TContext">The additional context.</typeparam>
public interface IChangeSet<TObject, TKey, out TContext> : IChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Additional context.
    /// </summary>
    TContext Context { get; }
}

/// <summary>
/// <para>A collection of changes.</para>
/// <para>Changes are always published in the order.</para>
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IChangeSet<TObject, TKey> : IChangeSet, IEnumerable<Change<TObject, TKey>>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets the number of updates.
    /// </summary>
    int Updates { get; }
}
