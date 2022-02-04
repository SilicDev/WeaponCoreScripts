using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        public class ShieldPbApi
        {
            private IMyTerminalBlock _block;

            private readonly Func<IMyTerminalBlock, float> _getShieldPercent;
            private readonly Func<IMyTerminalBlock, float> _getMaxHpCap;
            // Fields below do not require SetActiveShield to be defined first.
            private readonly Func<IMyCubeGrid, bool> _gridHasShield;
            private readonly Func<IMyEntity, IMyTerminalBlock> _getShieldBlock;
            private readonly Func<IMyTerminalBlock, bool> _isShieldBlock;

            public void SetActiveShield(IMyTerminalBlock block) => _block = block; // AutoSet to TapiFrontend(block) if shield exists on grid.

            public ShieldPbApi(IMyTerminalBlock block)
            {
                _block = block;
                var delegates = _block.GetProperty("DefenseSystemsPbAPI")?.As<Dictionary<string, Delegate>>().GetValue(_block);
                if (delegates == null) return;

                _getShieldPercent = (Func<IMyTerminalBlock, float>)delegates["GetShieldPercent"];
                _getMaxHpCap = (Func<IMyTerminalBlock, float>)delegates["GetMaxHpCap"];
                _gridHasShield = (Func<IMyCubeGrid, bool>)delegates["GridHasShield"];
                _getShieldBlock = (Func<IMyEntity, IMyTerminalBlock>)delegates["GetShieldBlock"];
                _isShieldBlock = (Func<IMyTerminalBlock, bool>)delegates["IsShieldBlock"];

                if (!IsShieldBlock()) _block = GetShieldBlock(_block.CubeGrid) ?? _block;
            }
            public float GetShieldPercent() => _getShieldPercent?.Invoke(_block) ?? -1;
            public float GetMaxHpCap() => _getMaxHpCap?.Invoke(_block) ?? -1;
            public bool GridHasShield(IMyCubeGrid grid) => _gridHasShield?.Invoke(grid) ?? false;
            public IMyTerminalBlock GetShieldBlock(IMyEntity entity) => _getShieldBlock?.Invoke(entity) ?? null;
            public bool IsShieldBlock() => _isShieldBlock?.Invoke(_block) ?? false;
        }
    }
}
