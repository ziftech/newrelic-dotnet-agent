/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IStreamingModel : IDisposable
    {
        string DisplayName { get; }
    }

    public interface IStreamingBatchModel<TRequest> : IDisposable where TRequest:IStreamingModel
    {
        int Count { get; }

        void Dispose(bool disposeBatchItems);
    }

}
