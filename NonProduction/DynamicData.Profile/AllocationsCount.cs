// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Profile
{
    public class AllocationsCount
    {

        public long InitialSize { get; }
        public long FinalSize { get; }
        public long Size => FinalSize - InitialSize;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public AllocationsCount(long initialSize, long finalSize)
        {
            InitialSize = initialSize;
            FinalSize = finalSize;
        }

        public override string ToString()
        {
            return $"Allocation Bytes: {Size:N0}";
        }
    }
}