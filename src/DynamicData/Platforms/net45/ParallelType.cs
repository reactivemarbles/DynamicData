// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    /// <summary>
    /// The type of parallelisation.
    /// </summary>
    public enum ParallelType
    {
        /// <summary>
        /// No parallelisation will take place.
        /// </summary>
        None,

        /// <summary>
        /// Parallelisation will take place without preserving the enumerable order.
        /// </summary>
        Parallelise,

        /// <summary>
        /// Parallelisation will take place whilst preserving the enumerable order.
        /// </summary>
        Ordered
    }
}

#endif