// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace DynamicData.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
            => BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args, DefaultConfig.Instance
                    .WithArtifactsPath(Path.Combine(
                        GetProjectRootDirectory(),
                        Path.GetFileName(DefaultConfig.Instance.ArtifactsPath))));

        private static string GetProjectRootDirectory([CallerFilePath] string? callerFilePath = null)
            => Path.GetDirectoryName(callerFilePath)!;
    }
}