﻿using AkariMindControllers.AkariMind.MU3.Notes;
using AkiraMindController.Communication.AkariCommand;
using AkiraMindController.Communication.Connectors;
using MonoMod;
using MU3.Battle;
using MU3.Data;
using MU3.Game;
using MU3.Notes;
using MU3.Reader;
using MU3.Sequence;
using MU3.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static MU3.Notes.NotesManager;

namespace AkariMindControllers.AkariMind.MU3.Sequence
{
    [MonoModPatch("global::MU3.Sequence.PlayMusic")]
    internal class PlayMusicPatch : PlayMusic
    {
        private GameEngine _gameEngine;
        private SessionInfo _sessionInfo;

        private NotesManager ntMgr => (!(_gameEngine != null)) ? null : _gameEngine.notesManager;
        private NotesManagerEx ntMgrEx => ntMgr as NotesManagerEx;

        private bool isPause = false;
        private float pauseMsec;

        private extern void orig_Enter_Play();

        private void Enter_Play()
        {
            orig_Enter_Play();

            //register messages.
            Controller.RegisterMessageHandler<RestartGamePlay>(OnRequestRestartGamePlay);
            Controller.RegisterMessageHandler<ResumeGamePlay>(OnRequestResumeGamePlay);
            Controller.RegisterMessageHandler<PauseGamePlay>(OnRequestPauseGamePlay);
            Controller.RegisterMessageHandler<PrintGamePlayStatus>(OnRequestPrintGamePlayStatus);
            Controller.RegisterMessageHandler<ReloadFumen>(OnReloadFumen);
            Controller.RegisterMessageHandler<PlayGuideSE>(OnPlayGuideSE);
            Controller.RegisterMessageHandler<GetNoteManagerValue>(OnRequestGetNoteManagerValue);
            Controller.RegisterMessageHandler<AutoPlay>(OnAutoPlay);
            Controller.RegisterMessageHandler<SeekToGamePlay>(OnRequestSeekToGamePlay);
            Controller.RegisterMessageHandler<SetNoteManagerValue>(OnSetNoteManagerValue);

            isPause = false;
        }

        private void OnSetNoteManagerValue(SetNoteManagerValue message, IResponser responser)
        {
            switch (message.name)
            {
                case nameof(NotesManagerEx.fakeButtomMsec):
                    ntMgrEx.fakeButtomMsec = message.value;
                    break;
                default:
                    break;
            }
        }

        private void OnAutoPlay(AutoPlay message, IResponser responser)
        {
            ntMgrEx.enableAutoPlay(message.isEnable);
        }

        private void OnPlayGuideSE(PlayGuideSE message, IResponser responser)
        {
            ntMgrEx.playGuideSE();
        }

        private void OnReloadFumen(ReloadFumen message, IResponser responser)
        {
            PatchLog.WriteLine($"call OnReloadFumen() message.checkOgkrFilePath = {message.checkOgkrFilePath}");
            Singleton<ReaderMain>.instance.loadScore(message.checkOgkrFilePath);
        }

        private void OnRequestGetNoteManagerValue(GetNoteManagerValue message, IResponser responser)
        {
            var x = ntMgr.getPlayerPos().x;
            var field = ntMgr.fieldState;

            responser.Response(new GetNoteManagerValue.ReturnValue()
            {
                playEndFrame = ntMgr.getEndPlayFrame(),
                noteEndFrame = ntMgr.getEndNoteFrame(),

                playStartFrame = ntMgrEx.getStartPlayFrame(),
                noteStartFrame = ntMgrEx.getStartNoteFrame(),

                visibleFrame = ntMgrEx.getFrameVisible(),
                invisibleFrame = ntMgrEx.getFrameInvisible(),

                currentFrame = ntMgrEx.getCurrentFrame(),
                playProgress = ntMgrEx.getPlayProgress(),

                isPlaying = ntMgrEx.isPlaying,
                isPlayEnd = ntMgrEx.isPlayEnd,

                ogkrFilePath = SingletonStateMachine<DataManager, DataManager.EState>.instance.getOgkrPath(_sessionInfo.musicData.id, _sessionInfo.musicLevel),

                playerPosX = x * 10,//adjust

                posInC = field.area.posInC,
                posInL = field.area.posInL,
                posInR = field.area.posInR,

                autoPlay = ntMgrEx.isAutoPlay()
            });
        }

        private void OnRequestPrintGamePlayStatus(PrintGamePlayStatus message)
        {
            PatchLog.WriteLine($"call OnRequestPrintGamePlayStatus()");
            PatchLog.WriteLine($"isPause : {isPause}");
            PatchLog.WriteLine($"pauseMsec : {pauseMsec}");
            PatchLog.WriteLine($"Singleton<ReaderMain>.instance.enable : {Singleton<ReaderMain>.instance.enable}");
            PatchLog.WriteLine($"ntMgr.getCurrentFrame() : {ntMgr.getCurrentFrame()}");
            PatchLog.WriteLine($"ntMgr.isPlaying : {ntMgr.isPlaying}");
            PatchLog.WriteLine($"ntMgr.isPlayEnd : {ntMgr.isPlayEnd}");
            PatchLog.WriteLine($"ntMgr.getAddFrame() : {ntMgr.getAddFrame()}");
            PatchLog.WriteLine($"ntMgr.getCurrentMsec() : {ntMgr.getCurrentMsec()}");
            var gameSound = Singleton<GameSound>.instance;
            PatchLog.WriteLine($"gameBGM.isPlay : {gameSound.gameBGM.isPlay}");
            PatchLog.WriteLine($"gameBGM.msec : {gameSound.gameBGM.msec}");
        }

        private void PauseGameInternal()
        {
            if (isPause)
                return;
            var bgm = Singleton<GameSound>.instance.gameBGM;
            pauseMsec = bgm.msec;
            bgm.stop();
            ntMgr.setPause(true);

            isPause = true;
        }

        private void ResumeGameInternal()
        {
            if (!isPause)
                return;

            Singleton<GameSound>.instance.gameBGM.playMusic(_sessionInfo.musicData, (int)pauseMsec);
            ntMgr.setPause(false);
            ntMgr.startPlay(pauseMsec);

            isPause = false;
        }

        private IEnumerator OnRequestSeekToGamePlay(SeekToGamePlay message)
        {
            PauseGameInternal();
            var msec = message.audioTimeMsec;

            yield return null;
            //clean objects and reset status
            ntMgr.reset();
            //reload&parse fumen file again
            ntMgr.loadScore(_sessionInfo, _gameEngine.IsStageDazzling);
            yield return null;
            //reset counter
            _gameEngine.reset();
            //seek timeline of notes
            ntMgr.setFrameForce(msec / 16.666666f);

            //redraw notes and make them visible
            ntMgrEx.refreshNotesVisible();

            pauseMsec = msec;

            if (message.playAfterSeek)
                ResumeGameInternal();

            isPause = !message.playAfterSeek;
        }

        private void OnRequestPauseGamePlay(PauseGamePlay message)
        {
            PatchLog.WriteLine($"call OnRequestPauseGamePlay() isPause = {isPause}");
            PauseGameInternal();
            PatchLog.WriteLine($"pause game , pauseMsec = {pauseMsec:F4} , ntMgr.currentMsec = {ntMgr.getCurrentMsec():F4} , ntMgr.currentFrame = {ntMgr.getCurrentFrame():F4}");
        }

        private IEnumerator OnRequestResumeGamePlay(ResumeGamePlay message)
        {
            PatchLog.WriteLine($"call OnRequestResumeGamePlay() isPause = {isPause} pauseMsec:{pauseMsec}");

            if (message.playGuideSEBeforePlay)
            {
                ReaderMain instance = Singleton<ReaderMain>.instance;
                var currentBpm = instance.composition.bpmList.getLatest(pauseMsec);

                string serialize(TGrid grid, float l) => $"[{(int)(grid.grid / l)},{(int)(grid.grid % l)}]({grid.frame})";

                float msec2grid(BPM t, float msec) => (t.resT * t.bpm) * msec / 2400000f;
                float grid2msec(BPM t, float grid) => (float)(240000.0 * (double)grid / (double)((float)t.resT * t.bpm));

                var gridOffset = msec2grid(currentBpm, pauseMsec - currentBpm.msec);
                PatchLog.WriteLine($"call OnRequestResumeGamePlay() gridOffset = {gridOffset}");
                // 1. 获取pauseMsec对应的tGrid
                var pauseTGrid = new TGrid(currentBpm.grid + (int)gridOffset);
                pauseTGrid.setMsec(pauseMsec);
                PatchLog.WriteLine($"call OnRequestResumeGamePlay() pauseTGrid.grid = {serialize(pauseTGrid, currentBpm.resT)} pauseTGrid.msec = {pauseTGrid.msec}");

                // 2. 通过tGrid获取对应的节拍met
                var meter = instance.composition.meterChangeList.getLatest(pauseTGrid);
                PatchLog.WriteLine($"call OnRequestResumeGamePlay() meter.bunshi = {meter.bunshi} meter.bunbo = {meter.bunbo}");

                // 3. 获取节拍长度,计算出delay
                float delay = grid2msec(currentBpm, meter.gridBeat) / 1000f;
                PatchLog.WriteLine($"call OnRequestResumeGamePlay() delay = {delay}s");

                var clickDefault = instance.header.clickDefault;
                if (clickDefault > 0)
                {
                    int num = 0;
                    while (num < clickDefault)
                    {
                        yield return new WaitForSeconds(delay);
                        ntMgrEx.playGuideSE(NotesManagerSE.GuideSE_Count);
                        num += meter.gridBeat;
                    }
                    //补多一拍
                    yield return new WaitForSeconds(delay);
                }
                PatchLog.WriteLine($"call OnRequestResumeGamePlay() play guideSE all done.");
            }
            ResumeGameInternal();
        }

        private void Leave_Play()
        {
            //unregister messages.
            Controller.UnregisterSpecifyMessageAllHandler<RestartGamePlay>();
            Controller.UnregisterSpecifyMessageAllHandler<ResumeGamePlay>();
            Controller.UnregisterSpecifyMessageAllHandler<PauseGamePlay>();
            Controller.UnregisterSpecifyMessageAllHandler<SeekToGamePlay>();
            Controller.UnregisterSpecifyMessageAllHandler<PrintGamePlayStatus>();
            Controller.UnregisterSpecifyMessageAllHandler<GetNoteManagerValue>();
            Controller.UnregisterSpecifyMessageAllHandler<PlayGuideSE>();
            Controller.UnregisterSpecifyMessageAllHandler<ReloadFumen>();
            Controller.UnregisterSpecifyMessageAllHandler<AutoPlay>();
            Controller.UnregisterSpecifyMessageAllHandler<SetNoteManagerValue>();
        }

        private IEnumerator OnRequestRestartGamePlay(RestartGamePlay message)
        {
            PatchLog.WriteLine("call OnRequestRestartGamePlay()");
            yield return null;
            Singleton<GameSound>.instance.gameBGM.stop();
            ntMgr.stopPlay();
            yield return null;
            _gameEngine.finishGame();
            _gameEngine.playFinish();
            base.setNextState(EState.Init);
        }
    }
}
