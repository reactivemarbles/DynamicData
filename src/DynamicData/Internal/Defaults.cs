// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace DynamicData.Internal;

internal static class Defaults
{
    public static IScheduler Scheduler { get; } = TaskPoolScheduler.Default;
}
