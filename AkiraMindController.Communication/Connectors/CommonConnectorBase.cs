using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AkiraMindController.Communication.Connectors
{
	public abstract class CommonConnectorBase : IConnector
	{
		[Serializable]
		internal protected class Payload
		{
			public string typeName;
			public string payloadJson;
		}

		private Dictionary<string, List<RegisterHandler>> registeredHandlers = new();

		public void RegisterMessageHandler<T>(IConnector.OnReceviceMessageFunc<T> handler)
			=> RegisterMessageHandler(ConvertTypeToTag(typeof(T)), handler);

		public void RegisterMessageHandler<T>(string tag, IConnector.OnReceviceMessageFunc<T> handler)
		{
			if (!registeredHandlers.TryGetValue(tag, out var list))
			{
				list = new();
				registeredHandlers[tag] = list;
			}

			if (list.Any(x => x.Check(handler)))
			{
				//it already has been added.
				return;
			}

			list.Add(new RegisterHandler<T>(handler));
			Log.WriteLine($"Registered message id : {handler?.GetHashCode()} , tag : {tag}");
		}

		public void UnregisterMessageHandler<T>(IConnector.OnReceviceMessageFunc<T> handler)
		=> UnregisterSpecifyMessageAllHandler(ConvertTypeToTag(typeof(T)));

		public void UnregisterSpecifyMessageAllHandler(string tag)
		{
			if (!registeredHandlers.TryGetValue(tag, out var list))
				return;

			list.Clear();
			Log.WriteLine($"Unregistered specify message tag : {tag}");
		}

		private string ConvertTypeToTag(Type type) => $"type:<{type.FullName}>";

		public void UnregisterSpecifyMessageAllHandler<T>()
		{
			var type = typeof(T);
			UnregisterSpecifyMessageAllHandler(ConvertTypeToTag(type));
		}

		public void UnregisterAllMessageHandler()
		{
			registeredHandlers.Clear();
			Log.WriteLine($"Unregistered all message handlers");
		}

		public IEnumerable<RegisterHandler> GetTypeHandlers<T>() => GetTypeHandlers(typeof(T));
		public IEnumerable<RegisterHandler> GetTypeHandlers(Type type) => registeredHandlers.TryGetValue(ConvertTypeToTag(type), out var list) ? list : Enumerable.Empty<RegisterHandler>();
	}
}
