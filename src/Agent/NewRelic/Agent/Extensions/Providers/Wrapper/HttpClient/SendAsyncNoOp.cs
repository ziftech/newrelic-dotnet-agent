// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.HttpClient
{
    public class SendAsyncNoOp : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string AssemblyName = "System.Net.Http";
        private const string HttpClientTypeName = "System.Net.Http.HttpClient";
        private const string SendAsyncMethodName = "SendAsync";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var version = method.Type.Assembly.GetName().Version;

            if (version.Major > 4 && method.MatchesAny(assemblyName: AssemblyName, typeName: HttpClientTypeName, methodName: SendAsyncMethodName))
            {
                return new CanWrapResponse(true);
            }
            else
            {
                return new CanWrapResponse(false);
            }
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            return Delegates.NoOp;
        }
    }
}
