using AkiraMindController.Communication;
using AkiraMindController.Communication.Connectors;
using AkiraMindController.Communication.Connectors.ConnectorImpls.Http;
using AkiraMindController.Communication.Connectors.InternalMessages;
using MU3;
using MU3.Util;
using System;
using System.Collections;
using UnityEngine;
using static AkiraMindController.Communication.Connectors.CommonMessages.Ping;
using Ping = AkiraMindController.Communication.Connectors.CommonMessages.Ping;

namespace AkariMindControllers
{
	public static class Controller
	{
		private static HttpConnectorServer server;

		private static IEnumerator ProcessMessage(Message msg, IResponser responser)
		{
			yield return new WaitForSeconds(1);
			PatchLog.WriteLine($"server get message : {msg.content}");
		}

		public static void Init()
		{
			try
			{
				PatchLog.WriteLine($"Controller.Init() Begin...");
				SimpleInterfaceImplement.Serialize = JsonUtility.ToJson;
				SimpleInterfaceImplement.Deserialize = JsonUtility.FromJson;
				SimpleInterfaceImplement.Log = PatchLog.WriteLine;

				server = new HttpConnectorServer(30000);

				RegisterMessageHandler<Message>(ProcessMessage);
				RegisterMessageHandler<Ping>(ProcessPing);

				server.Start();
				PatchLog.WriteLine($"Controller.Init() Done!");
			}
			catch (Exception e)
			{
				PatchLog.WriteLine($"Controller.Init() Failed : {e.Message}");
			}
		}

		private static void ProcessPing(Ping message, IResponser responser)
		{
			responser.Response(new Pong());
		}

		private static void ExecuteOnUnityThread<T>(OnCoroutineResponsableReceviceMessageFunc<T> handler, T param, IResponser responser)
			=> SingletonMonoBehaviour<SystemUI>.instance.StartCoroutine(handler(param, responser));
		private static void ExecuteOnUnityThread<T>(OnCoroutineReceviceMessageFunc<T> handler, T param)
			=> SingletonMonoBehaviour<SystemUI>.instance.StartCoroutine(handler(param));

		public delegate IEnumerator OnCoroutineResponsableReceviceMessageFunc<T>(T message, IResponser responser);
		public delegate IEnumerator OnCoroutineReceviceMessageFunc<T>(T message);
		public delegate void OnReceviceMessageFunc<T>(T message);

		public static void UnregisterMessageHandler<T>(IConnector.OnReceviceMessageFunc<T> handler) => server.UnregisterMessageHandler<T>(handler);
		public static void UnregisterSpecifyMessageAllHandler<T>() => server.UnregisterSpecifyMessageAllHandler<T>();
		public static void UnregisterSpecifyMessageAllHandler(string tag) => server.UnregisterSpecifyMessageAllHandler(tag);
		public static void UnregisterAllMessageHandler() => server.UnregisterAllMessageHandler();

		public static void RegisterMessageHandler<T>(IConnector.OnReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>(handler);
		public static void RegisterMessageHandler<T>(OnReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>((r, _) => handler(r));
		public static void RegisterMessageHandler<T>(OnCoroutineResponsableReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>((param, responser) => ExecuteOnUnityThread(handler, param, responser));
		public static void RegisterMessageHandler<T>(OnCoroutineReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>((param, _) => ExecuteOnUnityThread(handler, param));


		public static void RegisterMessageHandler<T>(string tag, IConnector.OnReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>(tag, handler);
		public static void RegisterMessageHandler<T>(string tag, OnReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>(tag, (r, _) => handler(r));
		public static void RegisterMessageHandler<T>(string tag, OnCoroutineResponsableReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>(tag, (param, responser) => ExecuteOnUnityThread(handler, param, responser));
		public static void RegisterMessageHandler<T>(string tag, OnCoroutineReceviceMessageFunc<T> handler)
			=> server.RegisterMessageHandler<T>(tag, (param, _) => ExecuteOnUnityThread(handler, param));
	}
}
