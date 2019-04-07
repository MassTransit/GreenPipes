﻿namespace GreenPipes.BenchmarkConsole
{
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Engines;
    using BenchmarkDotNet.Environments;
    using BenchmarkDotNet.Jobs;


    public class DotNetCoreBenchmarkConfig :
        ManualConfig
    {
        public DotNetCoreBenchmarkConfig()
        {
            Add(MemoryDiagnoser.Default);
            Add(new Job
            {
                Environment = {Runtime = Runtime.Core},
                Run =
                {
                    IterationCount = 2,
                    RunStrategy = RunStrategy.Throughput,
                    WarmupCount = 1,
                    LaunchCount = 1,
                    UnrollFactor = 1,
                    InvocationCount = 2_000_000
                }
            });
        }
    }
}
