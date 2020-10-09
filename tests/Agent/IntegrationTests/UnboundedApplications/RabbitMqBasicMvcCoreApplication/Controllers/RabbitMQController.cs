// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqBasicMvcCoreApplication.Controllers
{
    public class RabbitMQController : Controller
    {
        RabbitApi rabbitApi = new RabbitApi();

        [HttpGet]
        public string RabbitMQ_SendReceive(string queueName, string message)
        {
            var receiveMessage = string.Empty;
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            rabbitApi.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            rabbitApi.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = rabbitApi.Channel.BasicGet(queueName, true);

            receiveMessage = Encoding.UTF8.GetString(basicGetResult.Body);

            return string.Format("method=Send,message={0},queueName={1}", receiveMessage, queueName);
        }

        [HttpGet]
        public string RabbitMQ_SendReceive_HeaderExists(string queueName, string message)
        {
            var receiveMessage = string.Empty;
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            rabbitApi.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            rabbitApi.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = rabbitApi.Channel.BasicGet(queueName, true);
            var headerExists = basicGetResult.BasicProperties.Headers.Any(header => header.Key.ToLowerInvariant() == "newrelic");

            receiveMessage = Convert.ToString(headerExists);

            return receiveMessage;
        }

        [HttpGet]
        public string RabbitMQ_SendReceive_HeaderValue(string queueName, string message)
        {
            var receiveMessage = string.Empty;
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            rabbitApi.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            rabbitApi.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = rabbitApi.Channel.BasicGet(queueName, true);
            var headerValue = basicGetResult.BasicProperties.Headers.FirstOrDefault(header => header.Key.ToLowerInvariant() == "newrelic").Value;
            receiveMessage = Encoding.UTF8.GetString((byte[])headerValue);

            return receiveMessage;
        }

        [HttpGet]
        public async Task<string> RabbitMQ_SendReceiveWithEventingConsumer(string queueName, string message)
        {
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            rabbitApi.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            rabbitApi.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            using (var client = new HttpClient())
            {
                var request = HttpContext.Request;
                var url = $"{request.Scheme}://{request.Host.Host}:{request.Host.Port}/RabbitMQ/RabbitMQ_ReceiveWithEventingConsumer?queueName={queueName}";
                return await client.GetStringAsync(url);
            }
        }

        [HttpGet]
        public string RabbitMQ_ReceiveWithEventingConsumer(string queueName)
        {
            using (var manualResetEvent = new ManualResetEventSlim(false))
            {
                var receivedMessage = string.Empty;
                var consumer = new EventingBasicConsumer(rabbitApi.Channel);
                consumer.Received += handler;
                rabbitApi.Channel.BasicConsume(queueName, true, consumer);
                manualResetEvent.Wait();
                return receivedMessage;

                void handler(object ch, BasicDeliverEventArgs basicDeliverEventArgs)
                {
                    receivedMessage = Encoding.UTF8.GetString(basicDeliverEventArgs.Body);
                    manualResetEvent.Set();
                }
            }
        }

        [HttpGet]
        public string RabbitMQ_SendReceiveTopic(string exchangeName, string topicName, string message)
        {
            //Publish
            rabbitApi.Channel.ExchangeDeclare(exchange: exchangeName,
                                    type: "topic");

            var routingKey = topicName;
            var body = Encoding.UTF8.GetBytes(message);
            rabbitApi.Channel.BasicPublish(exchange: exchangeName,
                                    routingKey: routingKey,
                                    basicProperties: null,
                                    body: body);

            //Consume
            var queueName = rabbitApi.Channel.QueueDeclare().QueueName;

            rabbitApi.Channel.QueueBind(queue: queueName,
                                exchange: exchangeName,
                                routingKey: routingKey);

            var basicGetResult = rabbitApi.Channel.BasicGet(queueName, true);

            return $"method=SendReceiveTopic,exchangeName={exchangeName},queueName={queueName},topicName={topicName},message={message}";
        }

        [HttpGet]
        public string RabbitMQ_SendReceiveTempQueue(string message)
        {
            var resultMessage = string.Empty;
            var queueName = rabbitApi.Channel.QueueDeclare().QueueName;

            var body = Encoding.UTF8.GetBytes(message);

            rabbitApi.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var basicGetResult = rabbitApi.Channel.BasicGet(queueName, true);
            resultMessage = Encoding.UTF8.GetString(basicGetResult.Body);

            return $"method=SendReceiveTempQueue,queueName={queueName}message={resultMessage}";
        }

        [HttpGet]
        public string RabbitMQ_QueuePurge(string queueName)
        {
            rabbitApi.Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes("I will be purged");

            rabbitApi.Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            var countMessages = rabbitApi.Channel.QueuePurge(queueName);

            return $"Purged {countMessages} message from queue: {queueName}";
        }
    }
}
