﻿using AkiraMindController.Communication.Connectors.CommonMessages;
using AkiraMindController.Communication.Utils;
using System;
using System.IO;
using System.Net;

namespace AkiraMindController.Communication.Connectors.ConnectorImpls.Http
{
    public class HttpConnectorClient : ISendable
    {
        public HttpConnectorClient(int port = 28570)
        {
            Port = port;
        }

        public int Port { get; }

        public void SendMessage<T>() where T : new() => SendMessage(new T());

        public void SendMessage(object obj) => SendMessage(obj, null);

        public void SendMessage(object obj, Action<Stream> respStreamProcFunc)
        {
            var isPing = obj is not Ping;

            Log.SetEnableLog(isPing);
            {
                var queryPayload = MessageContentPacker.SerializeToPayloadString(obj);
                var url = $"http://127.0.0.1:{Port}/{(isPing ? "ping":string.Empty)}?payload={queryPayload}";
                var request = (HttpWebRequest)WebRequest.Create(url);

                Log.WriteLine($"[client] posted request : {url}");
                try
                {
                    using var resp = (HttpWebResponse)request.GetResponse();
                    Log.WriteLine($"[client] request response : {resp.StatusCode}");

                    if (respStreamProcFunc is not null)
                    {
                        using var stream = resp.GetResponseStream();
                        respStreamProcFunc(stream);
                        Log.WriteLine($"[client] response stream has been processed");
                    }
                }
                catch (Exception e)
                {
                    Log.WriteLine($"[client] resp-waiting throw exception : {e.Message}");
                }
            }
            Log.SetEnableLog(true);
        }

        public X SendMessageWithResponse<T, X>(T obj)
            where T : new()
            where X : new()
        {
            X result = default;
            SendMessage(obj, stream =>
            {
                using var reader = new StreamReader(stream);
                var str = reader.ReadToEnd();
                var obj = MessageContentPacker.DeserializeFromPayloadString(str);
                result = obj?.GetType() == typeof(X) ? (X)obj : default;
            });
            return result;
        }

        public X SendMessageWithResponse<T, X>()
            where T : new()
            where X : new() => SendMessageWithResponse<T, X>(new T());
    }
}
