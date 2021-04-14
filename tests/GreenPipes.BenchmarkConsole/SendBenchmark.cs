﻿// Copyright 2012-2018 Chris Patterson
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.
namespace GreenPipes.BenchmarkConsole
{
    using System;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Jobs;
    using Contracts;
    using Pipes;
    using Throughput;


    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser, GcServer(true), GcForce]
    public class SendBenchmark
    {
        readonly IPipe<TestContext> _concurrencyPipe;
        readonly TestContext _context;
        readonly IPipe<TestContext> _doublePipe;
        readonly IPipe<TestContext> _emptyPipe;
        readonly IPipe<TestContext> _faultPipe;
        readonly IPipe<TestContext> _retryPipe;
        readonly IPipe<PipeContext> _dispatchPipe;
        readonly IPipe<PipeContext> _doubleDispatchPipe;
        readonly IPipe<PipeContext> _tripleDispatchPipe;

        public SendBenchmark()
        {
            _context = new ThroughputTestContext(Guid.NewGuid(), "Payload");

            _emptyPipe = Pipe.Empty<TestContext>();

            _retryPipe = Pipe.New<TestContext>(x =>
            {
                x.UseRetry(r => r.Immediate(1));

                x.UseFilter(new BenchmarkFilter());
            });

            _concurrencyPipe = Pipe.New<TestContext>(x =>
            {
                x.UseConcurrencyLimit(Environment.ProcessorCount);

                x.UseFilter(new BenchmarkFilter());
            });

            _doublePipe = Pipe.New<TestContext>(x =>
            {
                x.UseConcurrencyLimit(Environment.ProcessorCount);
                x.UseRetry(r => r.Immediate(1));

                x.UseFilter(new BenchmarkFilter());
            });

            _faultPipe = Pipe.New<TestContext>(x =>
            {
                x.UseRetry(r => r.Immediate(1));

                x.UseFilter(new FaultFilter());
            });

            var dispatchPipe = new PipeRouter();
            _dispatchPipe = dispatchPipe;

            dispatchPipe.ConnectPipe(Pipe.Empty<CommandContext<SetConcurrencyLimit>>());

            var doubleDispatchPipe = new PipeRouter();
            _doubleDispatchPipe = doubleDispatchPipe;

            doubleDispatchPipe.ConnectPipe(Pipe.Empty<CommandContext<SetConcurrencyLimit>>());
            doubleDispatchPipe.ConnectPipe(Pipe.Empty<CommandContext<SetRateLimit>>());

            var tripleDispatchPipe = new PipeRouter();
            _tripleDispatchPipe = tripleDispatchPipe;

            tripleDispatchPipe.ConnectPipe(Pipe.Empty<CommandContext<SetConcurrencyLimit>>());
            tripleDispatchPipe.ConnectPipe(Pipe.Empty<CommandContext<SetRateLimit>>());
            tripleDispatchPipe.ConnectPipe(Pipe.Empty<CommandContext<CircuitBreakerOpened>>());
        }

        [Benchmark]
        public async Task EmptyPipe()
        {
            await _emptyPipe.Send(_context);
        }

        [Benchmark]
        public async Task RetryPipe()
        {
            await _retryPipe.Send(_context);
        }

        [Benchmark]
        public async Task ConcurrencyPipe()
        {
            await _concurrencyPipe.Send(_context);
        }

        [Benchmark]
        public async Task DoublePipe()
        {
            await _doublePipe.Send(_context);
        }

        [Benchmark]
        public async Task DispatchPipe()
        {
            await _dispatchPipe.SendCommand<SetConcurrencyLimit>(_command);
        }

        [Benchmark]
        public async Task DoubleDispatchPipe()
        {
            await _doubleDispatchPipe.SendCommand<SetConcurrencyLimit>(_command);
        }

        [Benchmark]
        public async Task TripleDispatchPipe()
        {
            await _tripleDispatchPipe.SendCommand<SetConcurrencyLimit>(_command);
        }

        readonly SetConcurrencyLimit _command = new Command(32);

        public async Task FaultPipe()
        {
            try
            {
                await _faultPipe.Send(_context);
            }
            catch
            {
            }
        }


        class Command :
            SetConcurrencyLimit
        {
            public Command(int concurrencyLimit)
            {
                ConcurrencyLimit = concurrencyLimit;
            }

            public int ConcurrencyLimit { get; }
        }
    }
}
