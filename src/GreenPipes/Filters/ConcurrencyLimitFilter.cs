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
    using Agents;
    using Contracts;


    /// <summary>
    /// Limits the concurrency of the next section of the pipeline based on the concurrency limit
    /// specified.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class ConcurrencyLimitFilter<TContext> :
        Agent,
        IFilter<TContext>,
        IPipe<CommandContext<SetConcurrencyLimit>>,
        IDisposable
        where TContext : class, PipeContext
    {
        readonly int _concurrencyLimit;
        readonly SemaphoreSlim _limit;

        public ConcurrencyLimitFilter(int concurrencyLimit)
        {
            _concurrencyLimit = concurrencyLimit;

            _limit = new SemaphoreSlim(concurrencyLimit);
        }

        public void Dispose()
        {
            _limit?.Dispose();
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateFilterScope("concurrencyLimit");
            scope.Add("limit", _concurrencyLimit);
            scope.Add("available", _limit.CurrentCount);
        }

        [DebuggerNonUserCode]
        public async Task Send(TContext context, IPipe<TContext> next)
        {
            await _limit.WaitAsync(context.CancellationToken).ConfigureAwait(false);

            try
            {
                await next.Send(context).ConfigureAwait(false);
            }
            finally
            {
                _limit.Release();
            }
        }

        public async Task Send(CommandContext<SetConcurrencyLimit> context)
        {
            var concurrencyLimit = context.Command.ConcurrencyLimit;
            if (concurrencyLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLimit), "The concurrency limit must be >= 1");

            var previousLimit = _concurrencyLimit;
            if (concurrencyLimit > previousLimit)
                _limit.Release(concurrencyLimit - previousLimit);
            else
                for (; previousLimit > concurrencyLimit; previousLimit--)
                    await _limit.WaitAsync().ConfigureAwait(false);
        }

        protected override async Task StopAgent(StopContext context)
        {
            var slot = 0;
            try
            {
                for (; slot < _concurrencyLimit; slot++)
                    await _limit.WaitAsync(context.CancellationToken).ConfigureAwait(false);

                await base.StopAgent(context).ConfigureAwait(false);
            }
            finally
            {
                _limit.Release(slot);
            }
        }
    }
}
