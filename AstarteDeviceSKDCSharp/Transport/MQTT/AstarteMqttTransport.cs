﻿/*
 * This file is part of Astarte.
 *
 * Copyright 2023 SECO Mind Srl
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 */

using AstarteDeviceSDK.Protocol;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Publishing;
using MQTTnet.Exceptions;
using System.Text;

namespace AstarteDeviceSDKCSharp.Transport.MQTT
{
    public abstract class AstarteMqttTransport : AstarteTransport
    {
        protected IMqttClient? _client;
        private readonly IMqttConnectionInfo _connectionInfo;
        protected AstarteMqttTransport(AstarteProtocolType type,
        IMqttConnectionInfo connectionInfo) : base(type)
        {
            _connectionInfo = connectionInfo;
        }

        private async Task<IMqttClient> InitClientAsync()
        {
            if (_client != null)
            {
                try
                {
                    await _client.DisconnectAsync();
                }
                catch (MqttCommunicationException ex)
                {
                    throw new AstarteTransportException(ex.Message, ex);
                }
            }

            MqttFactory mqttFactory = new();
            return mqttFactory.CreateMqttClient();
        }

        private async Task CompleteAstarteConnection()
        {
            if (!_introspectionSent)
            {
                await SendIntrospection();
                await SendEmptyCacheAsync();
                _introspectionSent = true;
            }
        }

        public override async Task Connect()
        {

            if (_client != null)
            {
                if (_client.IsConnected)
                {
                    return;
                }
            }
            else
            {
                _client = await InitClientAsync();
            }

            var result = await _client.ConnectAsync(_connectionInfo.GetMqttConnectOptions(),
                    CancellationToken.None);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                await CompleteAstarteConnection();
            }
            else
            {
                throw new AstarteTransportException
                ($"Error connecting to MQTT. Code: {result.ResultCode}");
            }

        }

        public override void Disconnect()
        {
            if (_client != null)
            {
                if (_client.IsConnected)
                {
                    _client.DisconnectAsync();
                }
            }

        }

        public override bool IsConnected()
        {
            if (_client == null)
            {
                return false;
            }
            return _client.IsConnected;
        }

        public IMqttConnectionInfo GetConnectionInfo()
        {
            return _connectionInfo;
        }

        private async Task SendEmptyCacheAsync()
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
                                  .WithTopic(_connectionInfo.GetClientId() + "/control/emptyCache")
                                  .WithPayload(Encoding.ASCII.GetBytes("1"))
                                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                                  .WithRetainFlag(false)
                                  .Build();

            MqttClientPublishResult result = await _client.PublishAsync(applicationMessage);
            if (result.ReasonCode != MqttClientPublishReasonCode.Success)
            {
                throw new AstarteTransportException($"Error publishing on MQTT. Code: {result.ReasonCode}");
            }
        }

    }
}
