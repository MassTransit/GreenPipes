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
namespace GreenPipes.Partitioning
{
    using System.Threading.Tasks;
    using Agents;


    public interface IPartitioner :
        ISupervisor,
        IProbeSite
    {
        IPartitioner<T> GetPartitioner<T>(PartitionKeyProvider<T> keyProvider)
            where T : class, PipeContext;
    }


    public interface IPartitioner<TContext> :
        ISupervisor,
        IProbeSite
        where TContext : class, PipeContext
    {
        /// <summary>
        /// Sends the context through the partitioner
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="next">The next pipe</param>
        /// <returns></returns>
        Task Send(TContext context, IPipe<TContext> next);
    }
}
