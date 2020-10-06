// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if P_LINQ
// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    /// <summary>
    /// Options to specify parallelisation of stream operations.  Only applicable for .Net4 and .Net45 builds.
    /// </summary>
    [SuppressMessage("ReSharper", "CommentTypo")]
    public class ParallelisationOptions
    {
        /// <summary>
        /// The default parallelisation options
        /// </summary>
        public static readonly ParallelisationOptions Default = new ParallelisationOptions(ParallelType.Ordered, 0);

        /// <summary>
        /// Value to be used when no parallelisation should take place
        /// </summary>
        public static readonly ParallelisationOptions None = new ParallelisationOptions(ParallelType.None, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public ParallelisationOptions(ParallelType type = ParallelType.None, int threshold = 0, int maxDegreeOfParallisation = 0)
        {
            Type = type;
            Threshold = threshold;
            MaxDegreeOfParallisation = maxDegreeOfParallisation;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        public ParallelType Type { get; }

        /// <summary>
        /// Gets the threshold.
        /// </summary>
        public int Threshold { get; }

        /// <summary>
        /// Gets the maximum degree of parallisation.
        /// </summary>
        public int MaxDegreeOfParallisation { get; }
    }
}
#endif
