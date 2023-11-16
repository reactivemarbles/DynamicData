// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    /// <summary>
    /// Options to specify parallelisation of stream operations.  Only applicable for .Net4 and .Net45 builds.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ParallelisationOptions"/> class.
    /// </remarks>
    /// <param name="type">The type of parallel operation.</param>
    /// <param name="threshold">The threshold before making the operation parallel.</param>
    /// <param name="maxDegreeOfParallelisation">The maximum degrees of parallelism.</param>
    public class ParallelisationOptions(ParallelType type = ParallelType.None, int threshold = 0, int maxDegreeOfParallelisation = 0)
    {
        /// <summary>
        /// The default parallelisation options.
        /// </summary>
        public static readonly ParallelisationOptions Default = new(ParallelType.Ordered);

        /// <summary>
        /// Value to be used when no parallelisation should take place.
        /// </summary>
        public static readonly ParallelisationOptions None = new();

        /// <summary>
        /// Gets the maximum degree of parallelisation.
        /// </summary>
        public int MaxDegreeOfParallelisation { get; } = maxDegreeOfParallelisation;

        /// <summary>
        /// Gets the threshold.
        /// </summary>
        public int Threshold { get; } = threshold;

        /// <summary>
        /// Gets the type.
        /// </summary>
        public ParallelType Type { get; } = type;
    }
}

#endif
