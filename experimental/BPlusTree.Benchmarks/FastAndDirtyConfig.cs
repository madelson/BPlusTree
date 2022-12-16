using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Benchmarks
{
    // from https://github.com/dotnet/BenchmarkDotNet/issues/257
    public class FastAndDirtyConfig : ManualConfig
    {
        public FastAndDirtyConfig()
        {
            Add(DefaultConfig.Instance); // *** add default loggers, reporters etc? ***

            AddJob(Job.Default
                .WithLaunchCount(1)     // benchmark process will be launched only once
                .WithIterationTime(TimeInterval.FromMilliseconds(100)) // 100ms per iteration
                .WithWarmupCount(3)     // 3 warmup iteration
                .WithIterationCount(3)
                .WithMaxRelativeError(0.05)
            ); ;
        }
    }
}
