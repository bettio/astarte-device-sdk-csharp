﻿using MQTTnet;
using MQTTnet.Client;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace AstarteDeviceSDKCSharp
{
    public class Crypto
    {

        public string GenerateCsr(string realm, string deviceId, string cryptoStoreDir)
        {

            ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string fileName = "device.key";

            if (!File.Exists(Path.Combine(cryptoStoreDir, fileName)))
            {
                string privateKey = new(PemEncoding.Write("PRIVATE KEY",ecdsa.ExportECPrivateKey()).ToArray());
                File.WriteAllText(Path.Combine(cryptoStoreDir, fileName),privateKey);
            }
            else
            {
                string publicKey = File.ReadAllText(
                        Path.Combine(cryptoStoreDir, fileName)).ToString()
                        .Replace("-----BEGIN PRIVATE KEY-----", "")
                        .Replace("-----END PRIVATE KEY-----", "").Replace("\n", "");
                ecdsa.ImportECPrivateKey(Convert.FromBase64String(publicKey), out _);
            }

            var cert = new CertificateRequest($"O=Devices,CN={realm}/{deviceId}", ecdsa, HashAlgorithmName.SHA256);

            byte[] pkcs10 = cert.CreateSigningRequest();
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("-----BEGIN CERTIFICATE REQUEST-----");

            string base64 = Convert.ToBase64String(pkcs10);

            int offset = 0;
            const int LineLength = 64;

            while (offset < base64.Length)
            {
                int lineEnd = Math.Min(offset + LineLength, base64.Length);
                builder.AppendLine(base64.Substring(offset, lineEnd - offset));
                offset = lineEnd;
            }

            builder.AppendLine("-----END CERTIFICATE REQUEST-----");
            return builder.ToString();
        }


        public void ImportDeviceCertificate(string certificate, string cryptoStoreDir)
        {

            var cert = new X509Certificate(Convert.FromBase64String(certificate));
            //File.WriteAllText(Path.Combine(cryptoStoreDir, "device.crt"), 
            //           PemEncoding.Write("PRIVATE KEY",cert.GetRawCertData().ToArray());

        }


        public async Task SetupMqttAsync(string realm, string deviceId, string cryptoStoreDir)
        {

            var mqttFactory = new MqttFactory();

            using (var mqttClient = mqttFactory.CreateMqttClient())
            {

                var tlsOptions = new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    Certificates = new List<X509Certificate>
                    {
                        new X509Certificate(cryptoStoreDir + "device.crt")
                    },
                    IgnoreCertificateChainErrors = true,
                    IgnoreCertificateRevocationErrors = true,
                    SslProtocol = System.Security.Authentication.SslProtocols.Tls,

                };

                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer("mqtts://localhost:8883/")
                    .WithTls(tlsOptions)
                    .WithCleanSession()
                    .Build();

                await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic($"{realm}/{deviceId}/#")
                    .WithPayload("19.5")
                    .Build();

                await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

                await mqttClient.DisconnectAsync();

                Console.WriteLine("MQTT application message is published.");
            }
        }

    }
}


