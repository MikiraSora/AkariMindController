﻿using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditorPlugins.AkariMindController.Modules.OngekiGamePlayControllerViewer.ExtraEditorMenuItems
{
    [Export(typeof(IFumenVisualEditorExtraMenuItemHandler))]
    public class SeekToHereMenuItem : IFumenVisualEditorExtraMenuItemHandler
    {
        public string[] RegisterMenuPath { get; } = new[] { IFumenVisualEditorExtraMenuItemHandler.COMMON_EXT_MENUITEM_ROOT, "AkariMindController", "控制游戏跳转到这里" };

        public async void Handle(FumenVisualEditorViewModel editor, EventArgs args)
        {
            var controller = IoC.Get<IOngekiGamePlayControllerViewer>();
            var curTGrid = editor.GetCurrentJudgeLineTGrid();
            var msec = TGridCalculator.ConvertTGridToAudioTime(curTGrid, editor);

            await controller.SeekTo(msec);
        }
    }
}
