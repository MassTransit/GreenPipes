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
    using System.Threading.Tasks;
    using Observers;


    /// <summary>
    /// Uses a retry policy to handle exceptions, retrying the operation in according
    /// with the policy
    /// </summary>
    public class RetryFilter<TContext> :
        IFilter<TContext>
        where TContext : class, PipeContext
    {
        readonly RetryObservable _observers;
        readonly IRetryPolicy _retryPolicy;

        public RetryFilter(IRetryPolicy retryPolicy, RetryObservable observers)
        {
            _retryPolicy = retryPolicy;
            _observers = observers;
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateFilterScope("retry");

            _retryPolicy.Probe(scope);
        }

        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        async Task IFilter<TContext>.Send(TContext context, IPipe<TContext> next)
        {
            RetryPolicyContext<TContext> policyContext = _retryPolicy.CreatePolicyContext(context);
            try
            {
                if (_observers.Count > 0)
                    await _observers.PostCreate(policyContext).ConfigureAwait(false);

                await next.Send(policyContext.Context).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (exception.CancellationToken == policyContext.Context.CancellationToken || exception.CancellationToken == context.CancellationToken)
            {
                throw;
            }
            catch (Exception exception)
            {
                policyContext.Context.CancellationToken.ThrowIfCancellationRequested();

                if (policyContext.Context.TryGetPayload(out RetryContext<TContext> payloadRetryContext))
                {
                    if (_retryPolicy.IsHandled(exception))
                    {
                        await policyContext.RetryFaulted(exception).ConfigureAwait(false);

                        if (_observers.Count > 0)
                            await _observers.RetryFault(payloadRetryContext).ConfigureAwait(false);
                    }

                    context.GetOrAddPayload(() => payloadRetryContext);

                    throw;
                }

                if (policyContext.Context.TryGetPayload(out RetryContext genericRetryContext))
                {
                    if (_retryPolicy.IsHandled(exception))
                    {
                        await policyContext.RetryFaulted(exception).ConfigureAwait(false);

                        if (_observers.Count > 0)
                            await _observers.RetryFault(genericRetryContext).ConfigureAwait(false);
                    }

                    context.GetOrAddPayload(() => genericRetryContext);

                    throw;
                }

                if (!policyContext.CanRetry(exception, out RetryContext<TContext> retryContext))
                {
                    if (_retryPolicy.IsHandled(exception))
                    {
                        await retryContext.RetryFaulted(exception).ConfigureAwait(false);

                        if (_observers.Count > 0)
                            await _observers.RetryFault(retryContext).ConfigureAwait(false);

                        context.GetOrAddPayload(() => retryContext);
                    }

                    throw;
                }

                if (_observers.Count > 0)
                    await _observers.PostFault(retryContext).ConfigureAwait(false);

                await Attempt(context, retryContext, next).ConfigureAwait(false);
            }
            finally
            {
                policyContext.Dispose();
            }
        }

        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        async Task Attempt(TContext context, RetryContext<TContext> retryContext, IPipe<TContext> next)
        {
            while (!retryContext.CancellationToken.IsCancellationRequested)
            {
                if (retryContext.Delay.HasValue)
                    await Task.Delay(retryContext.Delay.Value, retryContext.CancellationToken).ConfigureAwait(false);

                await retryContext.PreRetry().ConfigureAwait(false);

                if (_observers.Count > 0)
                    await _observers.PreRetry(retryContext).ConfigureAwait(false);

                try
                {
                    await next.Send(retryContext.Context).ConfigureAwait(false);

                    if (_observers.Count > 0)
                        await _observers.RetryComplete(retryContext).ConfigureAwait(false);

                    return;
                }
                catch (OperationCanceledException exception)
                    when (exception.CancellationToken == retryContext.CancellationToken || exception.CancellationToken == context.CancellationToken)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    if (retryContext.Context.TryGetPayload(out RetryContext<TContext> payloadRetryContext))
                    {
                        if (_retryPolicy.IsHandled(exception))
                        {
                            await retryContext.RetryFaulted(exception).ConfigureAwait(false);

                            if (_observers.Count > 0)
                                await _observers.RetryFault(payloadRetryContext).ConfigureAwait(false);
                        }

                        context.GetOrAddPayload(() => payloadRetryContext);

                        throw;
                    }

                    if (retryContext.Context.TryGetPayload(out RetryContext genericRetryContext))
                    {
                        if (_retryPolicy.IsHandled(exception))
                        {
                            await retryContext.RetryFaulted(exception).ConfigureAwait(false);

                            if (_observers.Count > 0)
                                await _observers.RetryFault(genericRetryContext).ConfigureAwait(false);
                        }

                        context.GetOrAddPayload(() => genericRetryContext);

                        throw;
                    }

                    if (!retryContext.CanRetry(exception, out RetryContext<TContext> nextRetryContext))
                    {
                        if (_retryPolicy.IsHandled(exception))
                        {
                            await nextRetryContext.RetryFaulted(exception).ConfigureAwait(false);

                            if (_observers.Count > 0)
                                await _observers.RetryFault(nextRetryContext).ConfigureAwait(false);

                            context.GetOrAddPayload(() => nextRetryContext);
                        }

                        throw;
                    }

                    if (_observers.Count > 0)
                        await _observers.PostFault(nextRetryContext).ConfigureAwait(false);

                    retryContext = nextRetryContext;
                }
            }

            context.CancellationToken.ThrowIfCancellationRequested();
        }
    }
}
