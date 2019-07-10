// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel
{
    internal interface ISupportsCapcity
    {
        int Capacity { get; set; }
        int Count { get; }
    }
}