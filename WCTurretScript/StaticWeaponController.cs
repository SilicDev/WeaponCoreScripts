using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
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

            /// <summary>Determines if a block of the main grid blocks the weapon.</summary>
            /// <returns>true if no block is in the way, false otherwise</returns>
            public bool LineOfSightCheck(Vector3 TargetPos, IMyCubeGrid parentGrid)
            {
                Vector3 step = Weapon.WorldMatrix.Forward;
                Vector3 temp = GetPosition();
                while ((temp - GetPosition()).Length() < (TargetPos - GetPosition()).Length())
                {
                    temp += step;
                    if (parentGrid.CubeExists(parentGrid.WorldToGridInteger(temp)))
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>Calculates the position the target will be at when the projectile of the weapon reaches its distance.</summary>
            /// Not implemented for Vanilla
            /// <returns>The updated targetpos</returns>
            public Vector3 CalculatePredictedTargetPosition(long targetId, Vector3 TargetPos)
            {
                if (IsWC)
                {
                    var targetposraw = Api.GetPredictedTargetPosition(Weapon, targetId, 0);
                    if (targetposraw != null && targetposraw.HasValue)
                        return targetposraw.Value;
                }
                return TargetPos;
            }
        }
    }
}
