using AkiraMindController.Communication;
using AkiraMindController.Communication.AkariCommand;
using AkiraMindController.Communication.Connectors.CommonMessages;
using AkiraMindController.Communication.Connectors.ConnectorImpls.Http;
using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using OngekiFumenEditor.Utils.Ogkr;
using OngekiFumenEditorPlugins.AkariMindController.Modules.OngekiGamePlayControllerViewer.Base;
using OngekiFumenEditorPlugins.AkariMindController.Modules.OngekiGamePlayControllerViewer.Views;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using static AkiraMindController.Communication.Connectors.CommonMessages.Ping;

namespace OngekiFumenEditorPlugins.AkariMindController.Modules.OngekiGamePlayControllerViewer.ViewModels
{
	[Export(typeof(IOngekiGamePlayControllerViewer))]
	[MapToView(ViewType = typeof(OngekiGamePlayControllerViewerView))]
	public class OngekiGamePlayControllerViewerViewModel : Tool, IOngekiGamePlayControllerViewer
	{
		public override PaneLocation PreferredLocation => PaneLocation.Bottom;

		public bool IsConnected => ConnectStatus == ConnectStatus.Connected;
		private ConnectStatus connectStatus = ConnectStatus.NotConnect;
		public ConnectStatus ConnectStatus
		{
			get => connectStatus;
			set
			{
				if (connectStatus != value && value == ConnectStatus.Disconnected)
					AppendOutputLine("Disconnected.");
				Set(ref connectStatus, value);
				NotifyOfPropertyChange(() => IsConnected);
			}
		}

		private string ogkrSavePath;
		public string OgkrSavePath
		{
			get => ogkrSavePath;
			set => Set(ref ogkrSavePath, value);
		}

		private bool isPlayGuideSEAfterPlay;
		public bool IsPlayGuideSEAfterPlay
		{
			get => isPlayGuideSEAfterPlay;
			set => Set(ref isPlayGuideSEAfterPlay, value);
		}

		private bool isAutoPlay;
		public bool IsAutoPlay
		{
			get => isAutoPlay;
			set
			{
				Set(ref isAutoPlay, value);
				MakeSureOptionsApplied();
			}
		}

		private bool isPauseIfMissBellOrDamaged;
		public bool IsPauseIfMissBellOrDamaged
		{
			get => isPauseIfMissBellOrDamaged;
			set
			{
				Set(ref isPauseIfMissBellOrDamaged, value);
				MakeSureOptionsApplied();
			}
		}


		private string curAutoFaderTargetDataStr;
		public string CurAutoFaderTargetDataStr
		{
			get => curAutoFaderTargetDataStr;
			set
			{
				Set(ref curAutoFaderTargetDataStr, value);
			}
		}

		private string preAutoFaderTargetDataStr;
		public string PreAutoFaderTargetDataStr
		{
			get => preAutoFaderTargetDataStr;
			set
			{
				Set(ref preAutoFaderTargetDataStr, value);
			}
		}

		private float seekTimeMsec;
		public float SeekTimeMsec
		{
			get => seekTimeMsec;
			set => Set(ref seekTimeMsec, value);
		}

		private float calcCurFrame;
		public float CalcCurFrame
		{
			get => calcCurFrame;
			set => Set(ref calcCurFrame, value);
		}

		private bool isPlayAfterSeek;
		public bool IsPlayAfterSeek
		{
			get => isPlayAfterSeek;
			set => Set(ref isPlayAfterSeek, value);
		}

		private string cueId;
		public string CueId
		{
			get => cueId;
			set => Set(ref cueId, value);
		}

		private bool isReloadAfterSeek;
		public bool IsReloadAfterSeek
		{
			get => isReloadAfterSeek;
			set => Set(ref isReloadAfterSeek, value);
		}

		private int port = 30000;
		private HttpConnectorClient client;
		private AbortableThread thread;

		public int Port
		{
			get => port;
			set => Set(ref port, value);
		}

		protected override void OnViewLoaded(object view)
		{
			base.OnViewLoaded(view);

			DisplayName = "AkariMindController";

			var opt = new JsonSerializerOptions() { IncludeFields = true };
			SimpleInterfaceImplement.Deserialize = (json, type) => JsonSerializer.Deserialize(json, type, opt);
			SimpleInterfaceImplement.Serialize = obj => JsonSerializer.Serialize(obj, opt);
			SimpleInterfaceImplement.Log = x => Log.LogDebug(x);

			thread = new AbortableThread(OnThreadAutomaticCheckConnect);
			thread.IsBackground = true;
			thread.Start();
		}

		private async void OnThreadAutomaticCheckConnect(CancellationToken obj)
		{
			while (!obj.IsCancellationRequested)
			{
				await Task.Delay(1000);
				if (client is null)
					continue;
				await UpdateCheckConnecting();
			}
		}

		private void AppendOutputLine(string v)
		{
			Log.LogInfo($"[akari]{v}");
		}

		public async void Connect()
		{
			client = new HttpConnectorClient(Port);
			if (!await UpdateCheckConnecting())
			{
				client = default;
				ConnectStatus = ConnectStatus.Disconnected;
			}
			else
			{
				RefreshUI();
			}
		}

		public void OpenOgkrSavePathDialog()
		{
			OgkrSavePath = FileDialogHelper.SaveFile("请指定.ogkr保存的路径用于音击程序读取.", new[] { (".ogkr", "标准音击谱面文件") }) ?? OgkrSavePath;
		}

		public async void GetOgkrSavePathFromGamePlay()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			if ((await GetNotesManagerData())?.OgkrFilePath is not string ogkrPath || !File.Exists(ogkrPath))
				return;

			OgkrSavePath = Path.GetFullPath(ogkrPath);
			MessageBox.Show("获取成功");
		}

		private Task SendMessageAsync<T>() where T : new() => SendMessageAsync(new T());
		private Task SendMessageAsync(object obj) => Task.Run(() => client.SendMessage(obj));
		private Task<X> SendMessageAsync<T, X>() where T : new() where X : new() => SendMessageAsync<T, X>(new T());
		private Task<X> SendMessageAsync<T, X>(T obj) where T : new() where X : new() => Task.Run(() => client.SendMessageWithResponse<T, X>(obj));

		public async Task Play()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			await SendMessageAsync(new ResumeGamePlay() { playGuideSEBeforePlay = IsPlayGuideSEAfterPlay });
		}

		public async Task Pause()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;
			await SendMessageAsync(new PauseGamePlay());
		}

		public async Task Restart()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			await SendMessageAsync(new RestartGamePlay());
		}

		public async Task SeekTo(TimeSpan time)
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			Log.LogError($"akari seek to {time} , playAfterSeek : {IsPlayAfterSeek}");
			if (IsReloadAfterSeek)
				await Reload();
			await SendMessageAsync(new SeekToGamePlay()
			{
				audioTimeMsec = (int)time.TotalMilliseconds,
				playAfterSeek = IsPlayAfterSeek
			});
		}

		public Task SeekTo() => SeekTo(TimeSpan.FromMilliseconds(SeekTimeMsec));

		public async void RefreshUI()
		{
			var data = await GetNotesManagerData();

			isAutoPlay = (data)?.IsAutoPlay ?? false;
			isPauseIfMissBellOrDamaged = (data)?.IsPauseIfMissBellOrDamaged ?? false;

			NotifyOfPropertyChange(() => IsAutoPlay);
			NotifyOfPropertyChange(() => IsPauseIfMissBellOrDamaged);
		}

		public async Task Reload()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;
			if (!File.Exists(OgkrSavePath))
				return;
			if ((await GetNotesManagerData()) is not NotesManagerData data)
				return;
			await SendMessageAsync(new PauseGamePlay());
			var currentPlaytime = data.CurrentTime;
			await GenerateOgkr(OgkrSavePath);
			await SendMessageAsync(new SeekToGamePlay()
			{
				audioTimeMsec = (int)currentPlaytime.TotalMilliseconds,
				playAfterSeek = IsPlayAfterSeek
			});
		}

		private async Task MakeSureOptionsApplied()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;
			await SendMessageAsync(new AutoPlay() { isEnable = IsAutoPlay });
			await SendMessageAsync(new SetNoteManagerValue() { name = "isPauseIfMissBellOrDamaged", value = IsPauseIfMissBellOrDamaged.ToString() });
		}

		private async Task GenerateOgkr(string ogkrSavePath)
		{
			if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
				return;

			var result = await StandardizeFormat.Process(editor.Fumen);

			if (result?.SerializedFumen is OngekiFumen serializedFumen)
			{
				var data = await IoC.Get<IFumenParserManager>().GetSerializer(ogkrSavePath).SerializeAsync(serializedFumen);
				await File.WriteAllBytesAsync(ogkrSavePath, data);
				await SendMessageAsync(new ReloadFumen { checkOgkrFilePath = ogkrSavePath });

				Log.LogInfo($"AkariMindController generate fumen to {ogkrSavePath}");
			}
			else
			{
				var errorMsg = result.Message;
				Log.LogError($"AkariMindController can't generate fumen : {errorMsg}");
				MessageBox.Show($"无法生成谱面并更新到游戏中:{errorMsg}");
			}
		}

		public async Task<bool> UpdateCheckConnecting()
		{
			ConnectStatus = (await SendMessageAsync<Ping, Pong>().WithTimeout(1000)) is Pong ?
				ConnectStatus.Connected : ConnectStatus.Disconnected;
			return ConnectStatus == ConnectStatus.Connected;
		}

		public Task<bool> CheckVailed()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return Task.FromResult(false);
			//todo
			return Task.FromResult(true);
		}

		public async void PlayGuideSE()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			await SendMessageAsync<PlayGuideSE>();
		}

		public async void ForceEnd()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			await SendMessageAsync<ForceEndGamePlay>();
		}

		public async void DumpUnfinishInfo()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			await SendMessageAsync<DumpUnfinishInfo>();
		}

		public async Task<NotesManagerData?> GetNotesManagerData()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return null;

			if ((await SendMessageAsync<GetNoteManagerValue, GetNoteManagerValue.ReturnValue>()) is not GetNoteManagerValue.ReturnValue retVal)
				return null;

			var msecPerFrame = 1000 / 60.0;

			return new NotesManagerData()
			{
				CurrentTime = TimeSpan.FromMilliseconds(retVal.currentFrame * msecPerFrame),
				InvisibleTime = TimeSpan.FromMilliseconds(retVal.invisibleFrame * msecPerFrame),
				VisibleTime = TimeSpan.FromMilliseconds(retVal.visibleFrame * msecPerFrame),
				NoteEndTime = TimeSpan.FromMilliseconds(retVal.noteEndFrame * msecPerFrame),
				NoteStartTime = TimeSpan.FromMilliseconds(retVal.noteStartFrame * msecPerFrame),
				PlayEndTime = TimeSpan.FromMilliseconds(retVal.playEndFrame * msecPerFrame),
				PlayStartTime = TimeSpan.FromMilliseconds(retVal.playStartFrame * msecPerFrame),
				PlayProgress = retVal.playProgress,
				OgkrFilePath = retVal.ogkrFilePath,
				IsPlayEnd = retVal.isPlayEnd,
				IsPlaying = retVal.isPlaying,
				IsAutoPlay = retVal.autoPlay,
				IsPauseIfMissBellOrDamaged = retVal.isPauseIfMissBellOrDamaged
			};
		}

		public async void GetAutoFaderData()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			if ((await SendMessageAsync<GetNoteManagerAutoPlayData, GetNoteManagerAutoPlayData.ReturnValue>()) is GetNoteManagerAutoPlayData.ReturnValue data)
			{
				CurAutoFaderTargetDataStr = data.curFaderTargetStr;
				PreAutoFaderTargetDataStr = data.prevFaderTargetStr;
			}
		}

		public async void ApplyAutoFaderData()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			await SendMessageAsync(new SetNoteManagerValue() { name = "curFaderTarget", value = CurAutoFaderTargetDataStr?.Replace("\n", "") });
			await SendMessageAsync(new SetNoteManagerValue() { name = "prevFaderTarget", value = PreAutoFaderTargetDataStr?.Replace("\n", "") });
		}

		public async void PlayCustomSound()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			await SendMessageAsync(new PlayCustomCommonSound { cueIdList = CueId.Split("+").Select(int.Parse).ToArray() });
		}

		public async void DumpAutoFaderTarget()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			if ((await SendMessageAsync<DumpNoteManagerAutoPlayData, DumpNoteManagerAutoPlayData.ReturnValue>()) is DumpNoteManagerAutoPlayData.ReturnValue data)
			{
				var filePath = data.dumpFilePath;
				if (File.Exists(filePath))
				{
					if (MessageBox.Show("转储成功,是否打开文件夹?", "DumpAutoFaderTarget", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					{
						var dir = Path.GetDirectoryName(filePath);
						ProcessUtils.OpenPath(dir);
					}
				}
				else
				{
					MessageBox.Show("转储失败,请自行查看游戏日志", "DumpAutoFaderTarget");
				}
			}
		}

		public async void ManualCallCalcAutoPlayFader()
		{
			if (ConnectStatus != ConnectStatus.Connected)
				return;

			if (await SendMessageAsync<CalculateNextAutoPlayData, CalculateNextAutoPlayData.ReturnValue>(new CalculateNextAutoPlayData() { frame = CalcCurFrame }) is CalculateNextAutoPlayData.ReturnValue data)
			{
				CurAutoFaderTargetDataStr = data.curFaderTargetStr;
			}
		}
	}
}
