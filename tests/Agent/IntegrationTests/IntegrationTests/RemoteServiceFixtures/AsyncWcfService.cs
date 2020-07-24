﻿using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AsyncWcfService : RemoteApplicationFixture
    {
        private const String ApplicationDirectoryName = "AsyncWcfService";
        private const String ExecutableName = "NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.exe";
        private const String TargetFramework = "net451";

        [NotNull]
        public readonly String ExpectedTransactionName = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.BeginServiceMethod";

        public AsyncWcfService() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
            Actions
            (
                exerciseApplication: () =>
                {
                    Query();
                }
            );
        }

        public void Query()
        {
            const string expectedAsyncResult = "foo";
            const string filteredParameter = "bar";
            var timeout = TimeSpan.FromMinutes(1);
            var asyncWcfService = Wcf.GetClient<Applications.AsyncWcfService.IWcfService>(DestinationServerName, Port);
            var actualAsyncResult = Task.Run(() => QueryWcfService(asyncWcfService, expectedAsyncResult, filteredParameter, timeout)).Result;

            Assert.Equal(expectedAsyncResult, actualAsyncResult);
        }

        private async Task<String> QueryWcfService([NotNull] Applications.AsyncWcfService.IWcfService service, [NotNull] String input, [NotNull] String otherInput, TimeSpan timeout)
        {
            var cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(timeout);

            var asyncResult = service.BeginServiceMethod(input, otherInput, _ => { }, null);
            Contract.Assert(asyncResult != null);

            var asyncAwaitHandle = asyncResult.AsyncWaitHandle;
            Contract.Assert(asyncAwaitHandle != null);

            Contract.Assert(Task.Factory != null);
            await Task.Factory.StartNew(() => asyncAwaitHandle.WaitOne(), cancellationSource.Token).ConfigureAwait(false);

            return service.EndServiceMethod(asyncResult);
        }
    }
}