using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    partial class Program
    {
        /// <summary>Emulates the interface between Vanilla and WeaponCore weapons</summary>
        public class StaticWeaponController : WeaponController
        {

            /// <summary>Creates a new controller for the weapon.</summary>
            public StaticWeaponController(IMyTerminalBlock weapon) : base(weapon)
            {
                if (IsWC)
                {
                    if (!StaticWeaponDefinitionSubIds.Contains(Weapon.BlockDefinition.SubtypeName))
                    {
                        throw new Exception("Expected a Static Weapon for Controller");
                    }
                }
            }
        }
    }
}
