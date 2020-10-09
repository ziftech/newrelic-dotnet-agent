// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;

namespace RabbitMqBasicMvcCoreApplication
{
    public class RabbitApi
    {
        private readonly ConnectionFactory ChannelFactory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
        public IModel Channel;

        public RabbitApi()
        {
            var connection = ChannelFactory.CreateConnection();
            Channel = connection.CreateModel();
        }
    }
}
