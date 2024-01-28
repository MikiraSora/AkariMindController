using AkariMindControllers.AkariMind.MU3.Reader;
using AkariMindControllers.Base;
using AkariMindControllers.Utils;
using AkiraMindController.Communication.Bases;
using AkiraMindController.Communication.Bases.Collection;
using AkiraMindController.Communication.Utils;
using JetBrains.Annotations;
using MonoMod;
using MU3.Battle;
using MU3.Game;
using MU3.Notes;
using MU3.Reader;
using MU3.Sound;
using MU3.Tutorial;
using MU3.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AkariMindControllers.AkariMind.MU3.Notes
{
	[MonoModPatch("global::MU3.Notes.NotesManager")]
	internal partial class NotesManagerEx : NotesManager
	{
		#region Raw Props/Fields

		private NotesList _pNotesList = new NotesList();
		private static readonly int[] _seAnswerSound;
		private NoteControlList _noteControlList;
		private float _curFrame;
		private NotesNodeCache _notesCache;
		private bool _isForceFinish;
		private float _frameNoteStart;
		private float _frameNoteEnd;
		private float _framePlayStart;
		private bool _pause;
		private AutoplayLaneCollection apfList;

		#endregion

		public float getStartPlayFrame() => _framePlayStart;
		public float getStartNoteFrame() => _frameNoteStart;

		public void refreshNotesVisible()
		{
			for (int i = 0; i < _noteControlList.Count; i++)
			{
				NoteControl noteControl = _noteControlList[i];
				if (!noteControl.isPlay && !noteControl.isEnd && _curFrame >= noteControl.frameCreate)
				{
					LinkedListNode<NotesBase> note = noteControl.createNotesBase(_notesCache);
					addNotesBase(note);
				}
			}
		}

		public void playGuideSE(NotesManagerSE guideSEType = NotesManagerSE.GuideSE_Count)
		{
			PatchLog.WriteLine($"call playGuideSE() guideSEType = {guideSEType}");

			var soundID = (int)guideSEType;
			var vol = 1f;
			if (soundID == 1 || soundID == 2)
				vol = GameOption.volGuide;
			if (vol > 0.01f)
				Singleton<SoundManager>.instance.playVolume(_seAnswerSound[soundID], isLoop: false, vol);
		}

		static bool isEnableAutoPlay = false;
		bool _isPauseIfMissBellOrDamaged = false;

		public extern bool orig_isAutoPlay();
		private extern void orig_updateFader();

		private FixedSizeCycleCollection<AutoFaderTarget?> recordAudioTargets;

		public bool isAutoPlay()
		{
			if (orig_isAutoPlay())
				return true;

			return isEnableAutoPlay;
		}

		public bool isPauseIfMissBellOrDamaged()
		{
			return _isPauseIfMissBellOrDamaged;
		}

		public extern void orig_reset();
		public void reset()
		{
			orig_reset();
			invaildCurrentAutoFaderTargets();
			recordAudioTargets = recordAudioTargets ?? new FixedSizeCycleCollection<AutoFaderTarget?>(500);
			recordAudioTargets.Clear();
		}

		private extern void orig_createFieldState(float frame, ref FieldState fieldState);
		private void createFieldState(float frame, ref FieldState fieldState) => orig_createFieldState(frame, ref fieldState);

		public extern void orig_damagePlayer(Damage type, int damageXM);

		void dumpAutoFaderData()
		{
			void print(AutoFaderTarget r)
			{
				PatchLog.WriteLine($"targetPlaceRange : {r.targetPlaceRange}");
				PatchLog.WriteLine($"finalTargetFrame : {r.finalTargetFrame}");

				PatchLog.WriteLine($"moveableRange : {r.moveableRange}");
				PatchLog.WriteLine($"damageRanges : {(r.damageRanges?.Length > 0 ? string.Join(" ", r.damageRanges.Select(x => x.ToString()).ToArray()) : string.Empty)}");
				PatchLog.WriteLine($"bellRanges : {(r.damageRanges?.Length > 0 ? string.Join(" ", r.bellRanges.Select(x => x.ToString()).ToArray()) : string.Empty)}");
				PatchLog.WriteLine($"targetRanges : {(r.damageRanges?.Length > 0 ? string.Join(" ", r.targetRanges.Select(x => x.ToString()).ToArray()) : string.Empty)}");
			}

			PatchLog.WriteLine($"");
			PatchLog.WriteLine($"playerJudge : {getPlayerNotePlace()}");
			PatchLog.WriteLine($"playerDraw : {getPlayerNotePlaceDraw()}");
			PatchLog.WriteLine($"autoFaderPlace : {autoFaderPre}");
			PatchLog.WriteLine($"currentFieldRange : {new ValueRange(fieldState.area.posCenterL, fieldState.area.posCenterR)}");
			PatchLog.WriteLine($"------------curFaderTarget-----------");
			print(curFaderTarget);
			PatchLog.WriteLine($"------------prevFaderTarget-----------");
			print(prevFaderTarget);
		}

		public void damagePlayer(Damage type, int damageXM)
		{
			var debug = isAutoPlay() && !_pause;

			if (debug)
			{
				PatchLog.WriteLine($"------------damagePlayer() dumper-----------");
				PatchLog.WriteLine($"call damagePlayer({type},{damageXM}) at _curFrame:{getCurrentFrame()} ({getCurrentMsec()}ms)");
				dumpAutoFaderData();
				PatchLog.WriteLine($"--------------------------------------------");
			}

			orig_damagePlayer(type, damageXM);

			if (debug && isPauseIfMissBellOrDamaged())
				pauseGame();
		}

		public extern void orig_setResultEffectAndScore_Bell(bool isHit, int recoverXM, Vector3 posText, Vector3 posBomb);
		public virtual void setResultEffectAndScore_Bell(bool isHit, int recoverXM, Vector3 posText, Vector3 posBomb)
		{
			orig_setResultEffectAndScore_Bell(isHit, recoverXM, posText, posBomb);

			if (!isHit)
			{
				if (isAutoPlay() && !_pause)
				{
					PatchLog.WriteLine($"------------setResultEffectAndScore_Bell() dumper-----------");
					PatchLog.WriteLine($"call setResultEffectAndScore_Bell({isHit},{recoverXM},{posText},{posBomb}) at _curFrame:{getCurrentFrame()} ({getCurrentMsec()}ms)");
					dumpAutoFaderData();
					PatchLog.WriteLine($"--------------------------------------------");

					if (isPauseIfMissBellOrDamaged())
						pauseGame();
				}
			}
		}

		public struct NoteFrameInfo
		{
			public float frame;
			public NotesBase note;
		}

		public AutoFaderTarget curFaderTarget = default;
		public AutoFaderTarget prevFaderTarget = default;

		private const float MOVABLE_LEFT = -28;
		private const float MOVABLE_RIGHT = 28;

		/// <summary>
		/// 计算在frame内能收到伤害的范围
		/// </summary>
		/// <param name="frame"></param>
		/// <returns></returns>
		private ValueRange[] calcDamageRanges(float curFrame, float nextFrame, float? faderPlace = default)
		{
			var currentFaderPlace = faderPlace ?? SingletonMonoBehaviour<GameEngine>.instance.gameDeviceManager.getFader();

			var shellNotes = _pNotesList
				.OfType<ShellNoteEx>()
				.Where(shell =>
				{
					var frame = shell.ShellNoteCore.param.frameHit;
					return curFrame <= frame && frame <= nextFrame;
				})
				.ToArray();

			var beamNotes = _pNotesList
				.OfType<BeamNoteEx>()
				.Where(beam =>
				{
					var frame = beam.ShellNoteCore.param.frameFore <= curFrame && curFrame <= beam.ShellNoteCore.param.frameRear ?
											curFrame :
											beam.ShellNoteCore.param.frameFore;
					return curFrame <= frame && frame <= nextFrame;
				})
				.ToArray();

			var shellDamageRanges = shellNotes.Select(x =>
			{
				var width = x.ShellNoteCore.ShellWidth;
				var place = x.ShellNoteCore.param.placeHit;

				var left = place - width * 0.5f;
				var right = place + width * 0.5f;

				return new ValueRange(left, right);
			});

			var beamDamageRanges = beamNotes.Select(x =>
			{
				var param = x.ShellNoteCore.param;

				var curMsec = getCurrentMsec();
				var nextMsec = nextFrame * 16.666666f;
				var isIn = param.shape.isIn(nextMsec);
				var isIn2 = param.shape.isIn(curMsec);
				var interpolatedPlace = param.shape.getPlace(nextMsec);
				var interpolatedPlace2 = param.shape.getPlace(curMsec);
				var place = isIn ? interpolatedPlace :
				(
					nextMsec < param.frameFore * 16.666666f ? param.placeFore : param.placeRear
				);
				var width = param.widthJudge;

				var left = place - width * 0.5f;
				var right = place + width * 0.5f;

				var state = (int)x.ShellNoteCore.state;

				if (state >= (int)BeamNoteCore.State.ShootPre
				&& state <= (int)BeamNoteCore.State.Shoot
				&& curFrame < param.frameRear)
				{
					//激光只给走一边,不准跨线
					if (currentFaderPlace < place)
						right = MOVABLE_RIGHT;
					else
						left = MOVABLE_LEFT;
				}

				return new ValueRange(left, right);
			});

			var totalDamageRanges = ValueRange.Union(beamDamageRanges.Concat(shellDamageRanges)).ToArray();

			return totalDamageRanges;
		}

		private float CalcNextFrameOfObjectList(LaneObj laneObj, float frame)
		{
			for (int i = 0; i < laneObj.Count; i++)
			{
				/*
                 *              cur.frame
                 * |-------------|
                 *       ^
                 *       |
                 *       frame
                 */

				var cur = laneObj[i];
				if (frame <= cur.frame)
					return cur.frame;
			}

			return 0;
		}

		public float calcNextAutoPlayFaderFrame(float curFrame)
		{
			var nextFrame = curFrame;

			var beamNotes = _pNotesList
				.OfType<BeamNoteEx>()
				.Where(beam => beam.ShellNoteCore.param.frameFore <= curFrame && curFrame <= beam.ShellNoteCore.param.frameRear)
				.ToArray();

			nextFrame = curFrame + 500 / 16.666667f;

			var filterNote = _pNotesList.Select(x => x switch
			{
				ShellNoteEx shell => new NoteFrameInfo { frame = shell.ShellNoteCore.param.frameHit, note = shell },
				BellNoteEx bell => new NoteFrameInfo { frame = bell.BellNoteCore.getAvaliableFrameHit, note = bell },
				BeamNoteEx beam => new NoteFrameInfo
				{
					frame = CalcNextFrameOfObjectList(beam.ShellNoteCore.param.shape, curFrame),
					note = beam.ShellNoteCore.param.frameFore <= curFrame && curFrame <= beam.ShellNoteCore.param.frameRear ?
					beam : default
				},
				_ => default
			}).Where(x => x.note is not null && curFrame <= x.frame && x.frame <= nextFrame).OrderBy(x => x.frame).ToArray();

			//获取未来500ms内，最早出现的Bell或者子弹物件
			if (filterNote.Length > 0)
				nextFrame = filterNote.FirstOrDefault().frame;
			else if (beamNotes.Length > 0)
			{
				//如果没有需要注意的物件且还有激光，那就按帧计算，专心躲激光
				nextFrame = curFrame;
			}

			var finalNextTime = Math.Min(getEndPlayFrame(), nextFrame);
			return finalNextTime;
		}

		public AutoFaderTarget calcNextAutoPlayFader(float curFrame)
		{
			var currentFaderPlace = SingletonMonoBehaviour<GameEngine>.instance.gameDeviceManager.getFader();

			var nextFrame = calcNextAutoPlayFaderFrame(curFrame);

			var getBellNotes = _pNotesList
				.OfType<BellNoteEx>()
				.Select(bell => new NoteFrameInfo { frame = bell.BellNoteCore.getAvaliableFrameHit, note = bell })
				.Where(x => curFrame <= x.frame && x.frame <= nextFrame)
				.Select(x => x.note)
				.ToArray();

			var shellFieldState = new FieldState()
			{
				area = new FieldObject.Area()
			};

			createFieldState(nextFrame, ref shellFieldState);

			//伤害区域
			var damageRanges = calcDamageRanges(curFrame, nextFrame, currentFaderPlace);

			//Bell(必碰)区域
			var bellRanges = getBellNotes.Length == 0 ?
				Enumerable.Empty<ValueRange>() :
				ValueRange.Union(getBellNotes.OfType<BellNoteEx>().Select(x =>
				{
					var width = 5;
					var place = x.BellNoteCore.getAvaliablePlaceHit;
					return new ValueRange(place - width * 0.5f, place + width * 0.5f);
				}));

			//摇杆可移动区域(没算有效区域的)
			var movableRange = new ValueRange(MOVABLE_LEFT, MOVABLE_RIGHT);

			//安全区域 = 可移动区域 - 伤害区域
			var safeRanges = ValueRange.Except(Enumerable.Repeat(movableRange, 1), damageRanges);

			//最终目标区域 = 安全区域 和 Bell区域(如果有的话) 和 有效区域(如果有的话) 的交集
			var targetRanges = safeRanges;
			if (bellRanges.Any())
				targetRanges = ValueRange.Intersect(targetRanges, bellRanges).ToArray();

			//有效可移动区域(没算有效区域的)
			//为啥放到最后呢，是因为考虑到有轨道外的Bell
			var vaildMovableRange = new ValueRange(shellFieldState.area.posCenterL, shellFieldState.area.posCenterR);
			targetRanges = ValueRange.Intersect(targetRanges, Enumerable.Repeat(vaildMovableRange, 1)).ToArray();

			//这里将要附上可选的了

			//选择必去区域最近的一个点去插值
			var calcRanges = targetRanges.Select(x =>
			{
				//x表示排序依据，表示中点位置
				return new Vector3(x.max - x.min, x.min, x.max);
			}).OrderByDescending(x => x.x).ToArray();

			var targetPlaceRange = calcRanges.Select(x => new ValueRange(x.y, x.z)).FirstOrDefault();

			var newFaderTarget = new AutoFaderTarget();
			newFaderTarget.finalTargetFrame = nextFrame;
			newFaderTarget.targetPlaceRange = targetPlaceRange;

			//record result
			newFaderTarget.bellRanges = bellRanges.ToArray();
			newFaderTarget.targetRanges = targetRanges.ToArray();
			newFaderTarget.damageRanges = damageRanges.ToArray();

			newFaderTarget.moveableRange = movableRange;

			return newFaderTarget;
		}

		public float calcAutoPlayFader()
		{
			var forceFaderTarget = apfList.CalculateFaderXUnit(_curFrame);

			var curFrame = _curFrame;
			var currentFaderPlace = SingletonMonoBehaviour<GameEngine>.instance.gameDeviceManager.getFader();

			if (curFrame > curFaderTarget.finalTargetFrame)
			{
				var newFaderTarget = calcNextAutoPlayFader(curFrame);

				prevFaderTarget = curFaderTarget;
				curFaderTarget = newFaderTarget;

				if (prevFaderTarget.finalTargetFrame == curFaderTarget.finalTargetFrame)
				{
					//warn!!!
					curFaderTarget = newFaderTarget;
				}

				recordAudioTargets?.Enqueue(curFaderTarget);
			}

			if (forceFaderTarget is not double f)
			{
				//calc actualX
				var prevPlaceTarget = (prevFaderTarget.targetPlaceRange.min + prevFaderTarget.targetPlaceRange.max) / 2;
				var curPlaceTarget = (curFaderTarget.targetPlaceRange.min + curFaderTarget.targetPlaceRange.max) / 2;
				var calcFrame = curFrame;

				var frameDiff = curFaderTarget.finalTargetFrame - prevFaderTarget.finalTargetFrame;
				if (frameDiff <= 1)
					calcFrame = curFaderTarget.finalTargetFrame;

				Func<double, double> easingFunc = curFaderTarget.damageRanges?.Length > 0 ? easingFuncOutExpo : easingFuncLinear;

				var normalizedY = frameDiff == 0 ? 1 : ((calcFrame - prevFaderTarget.finalTargetFrame) / frameDiff);
				var normalizedX = Math.Min(1, Math.Max(0, easingFunc(normalizedY)));

				var adjustPlace = prevPlaceTarget + (curPlaceTarget - prevPlaceTarget) * normalizedX;
				//var adjustPlace = MathUtils.CalculateXFromTwoPointFormFormula(calcFrame, prevPlaceTarget, prevFaderTarget.finalTargetFrame, curPlaceTarget, curFaderTarget.finalTargetFrame);

				//limit range for absolute safe
				adjustPlace = Math.Max(fieldState.area.posCenterL, Math.Min(fieldState.area.posCenterR, adjustPlace));
				//adjustPlace = Math.Max(curFaderTarget.targetPlaceRange.min, Math.Min(curFaderTarget.targetPlaceRange.max, adjustPlace));

				return (float)adjustPlace;
			}
			else
			{
				return (float)f;
			}
		}

		public float autoFaderPre = 0;

		double easingFuncOutExpo(double x) => x == 1 ? 1 : 1 - Math.Pow(2, -10 * x);
		double easingFuncLinear(double x) => x;

		private void updateFader()
		{
			var gameObj = SingletonMonoBehaviour<GameEngine>.instance.gameDeviceManager as GameDeviceManagerEx;
			if (isAutoPlay())
				gameObj?.setFader(autoFaderPre);

			var autoFader = autoFaderPre = calcAutoPlayFader();

			if (isAutoPlay())
				gameObj?.setFader(autoFader);

			orig_updateFader();
		}

		public void invaildCurrentAutoFaderTargets()
		{
			curFaderTarget = default;
			prevFaderTarget = default;
			autoFaderPre = default;
		}

		public void enableAutoPlay(bool isEnable)
		{
			isEnableAutoPlay = isEnable;
		}

		public void enablePauseIfMissBellOrDamaged(bool isEnable)
		{
			_isPauseIfMissBellOrDamaged = isEnable;
		}

		public float pauseGame()
		{
			var bgm = Singleton<GameSound>.instance.gameBGM;
			var pauseMsec = bgm.msec;
			bgm.stop();
			setPause(true);
			return pauseMsec;
		}

		public void resumeGame(float pauseMsec)
		{
			Singleton<GameSound>.instance.gameBGM.playMusic(_sessionInfo.musicData, (int)pauseMsec);
			setPause(false);
			startPlay(pauseMsec);
		}

		public void applyAutoFaderTargets(AutoFaderTarget? cur = default, AutoFaderTarget? prev = default)
		{
			curFaderTarget = cur ?? default;
			prevFaderTarget = prev ?? default;
		}

		public string dumpFailedAutoTargetData()
		{
			var dirPath = Path.GetFullPath("FaildAutoFaderTargetData");

			try
			{
				var musicData = this._sessionInfo.musicData;
				var time = DateTime.Now;
				var fileName = $"({time.Month,2}-{time.Day,2} {time.Hour,2}-{time.Minute,2}-{time.Second,2}-{time.Millisecond,3}) [{musicData.id,4}] {musicData.artistName} - {musicData.name}.afdList";

				Directory.CreateDirectory(dirPath);
				var filePath = Path.Combine(dirPath, fileName);

				using var stream = File.OpenWrite(filePath);
				using var writer = new StreamWriter(stream);

				var count = 0;
				foreach (var item in recordAudioTargets.OfType<AutoFaderTarget>().OrderBy(x => x.finalTargetFrame))
				{
					writer.WriteLine(item.Serialize());
					writer.WriteLine();
					count++;
				}

				PatchLog.WriteLine($"call dumpFailedAutoTargetData() saved {count} data to file : {filePath}");

				return filePath;
			}
			catch (Exception e)
			{
				PatchLog.WriteLine($"call dumpFailedAutoTargetData() throwed exception : {e.Message}");
			}

			return string.Empty;
		}


		public extern bool orig_loadScore(SessionInfo sessionInfo, bool isStageDazzling);

		public bool loadScore(SessionInfo sessionInfo, bool isStageDazzling)
		{
			var r = orig_loadScore(sessionInfo, isStageDazzling);
			apfList = (Singleton<ReaderMain>.instance as ReaderMainEx)?.APFLanes;
			return r;
		}

		public void DumpUnfinishInfo()
		{
			PatchLog.WriteLine($"-------OnDumpUnfinishInfo()------");
			PatchLog.WriteLine($"Singleton<GameSound>.instance.gameBGM.isPlay:{Singleton<GameSound>.instance.gameBGM.isPlay}");
			PatchLog.WriteLine($"_curFrame > _frameNoteEnd:{_curFrame > _frameNoteEnd}");
			foreach (var ctrl in _noteControlList.Where(x => !x.isEnd))
			{
				var str = $"frame:{ctrl.frame}  speed:{ctrl.speed}  state:{ctrl.notesBase?.getCurrentState()}  ";

				if (ctrl.objBeam is BeamObj bo)
					str += $"[BEAM] totalGrid:{bo.grid} ";
				if (ctrl.objNote is NoteObj no)
					str += $"[NOTE] totalGrid:{no.grid}";
				if (ctrl.objBullet is BulletObj blt)
					str += $"[BULT] totalGrid:{blt.grid}  strId:{blt.pallete?.strID}";

				PatchLog.WriteLine(str);
			}
			PatchLog.WriteLine($"-------------");
		}

		public void ForceEndGamePlay()
		{
			_isForceFinish = true;
		}
	}
}
