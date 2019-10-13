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
namespace GreenPipes.Filters
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;
    using Internals.Extensions;


    /// <summary>
    /// Limits the number of calls through the filter to a specified count per time interval
    /// specified.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class RateLimitFilter<TContext> :
        IFilter<TContext>,
        IPipe<CommandContext<SetRateLimit>>,
        IDisposable
        where TContext : class, PipeContext
    {
        readonly TimeSpan _interval;
        readonly SemaphoreSlim _limit;
        readonly Timer _timer;
        int _count;
        int _rateLimit;

        public RateLimitFilter(int rateLimit, TimeSpan interval)
        {
            _rateLimit = rateLimit;
            _interval = interval;
            _limit = new SemaphoreSlim(rateLimit);
            _timer = new Timer(Reset, null, interval, interval);
        }

        public void Dispose()
        {
            _limit?.Dispose();
            _timer?.Dispose();
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateFilterScope("rateLimit");
            scope.Add("limit", _rateLimit);
            scope.Add("available", _limit.CurrentCount);
            scope.Add("interval", _interval);
        }

        [DebuggerNonUserCode]
        public Task Send(TContext context, IPipe<TContext> next)
        {
            var waitAsync = _limit.WaitAsync(context.CancellationToken);
            if (waitAsync.IsCompletedSuccessfully())
            {
                Interlocked.Increment(ref _count);

                return next.Send(context);
            }

            async Task SendAsync()
            {
                await waitAsync.ConfigureAwait(false);

                Interlocked.Increment(ref _count);

                await next.Send(context).ConfigureAwait(false);
            }

            return SendAsync();
        }

        public async Task Send(CommandContext<SetRateLimit> context)
        {
            var rateLimit = context.Command.RateLimit;
            if (rateLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(rateLimit), "The rate limit must be >= 1");

            var previousLimit = _rateLimit;
            if (rateLimit > previousLimit)
                _limit.Release(rateLimit - previousLimit);
            else
                for (; previousLimit > rateLimit; previousLimit--)
                    await _limit.WaitAsync().ConfigureAwait(false);

            _rateLimit = rateLimit;
        }

        void Reset(object state)
        {
            var processed = Interlocked.Exchange(ref _count, 0);
            if (processed > 0)
                _limit.Release(processed);
        }
    }
}