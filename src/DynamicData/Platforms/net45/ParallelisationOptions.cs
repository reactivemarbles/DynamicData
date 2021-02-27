// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    /// <summary>
    /// Options to specify parallelisation of stream operations.  Only applicable for .Net4 and .Net45 builds.
    /// </summary>
    public class ParallelisationOptions
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
        /// Initializes a new instance of the <see cref="ParallelisationOptions"/> class.
        /// </summary>
        /// <param name="type">The type of parallel operation.</param>
        /// <param name="threshold">The threshold before making the operation parallel.</param>
        /// <param name="maxDegreeOfParallelisation">The maximum degrees of parallelism.</param>
        public ParallelisationOptions(ParallelType type = ParallelType.None, int threshold = 0, int maxDegreeOfParallelisation = 0)
        {
            Type = type;
            Threshold = threshold;
            MaxDegreeOfParallelisation = maxDegreeOfParallelisation;
        }

        /// <summary>
        /// Gets the maximum degree of parallelisation.
        /// </summary>
        public int MaxDegreeOfParallelisation { get; }

        /// <summary>
        /// Gets the threshold.
        /// </summary>
        public int Threshold { get; }

        /// <summary>
        /// Gets the type.
        /// </summary>
        public ParallelType Type { get; }
    }
}

#endif