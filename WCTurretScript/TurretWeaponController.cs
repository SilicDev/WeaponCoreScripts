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
        public class TurretWeaponController : WeaponController
        {

            ///<summary>Indicates whether a block is locally or remotely controlled.</summary>
            public bool IsUnderControl
            {
                get
                {
                    if (IsWC)
                        return Api.GetPlayerController(Weapon) != -1;
                    return (Weapon as IMyLargeTurretBase).IsUnderControl;
                }
            }

            ///<summary>Creates a new controller for the weapon.</summary>
            public TurretWeaponController(IMyTerminalBlock weapon) : base(weapon)
            {
                if (IsWC)
                {
                    if (!TurretDefinitionSubIds.Contains(Weapon.BlockDefinition.SubtypeName))
                    {
                        throw new Exception("Expected a Turret Weapon for Controller");
                    }
                }
                else
                {
                    if (!(Weapon is IMyLargeTurretBase))
                    {
                        throw new Exception("Expected a Turret Weapon for Controller");
                    }
                }
            }

            public override MyDetectedEntityInfo? GetTarget()
            {
                if (IsWC)
                {
                    return Api.GetWeaponTarget(Weapon);
                }
                return (Weapon as IMyLargeTurretBase).GetTargetedEntity();
            }

            /// <summary>Returns the maximum range of this weapon.</summary>
            /// <returns>the maximum range of this weapon</returns>
            public override float GetMaxRange(int weaponId = 0)
            {
                if (IsWC)
                {
                    return Api.GetMaxWeaponRange(Weapon, weaponId);
                }
                return (Weapon as IMyLargeTurretBase).Range; // TODO: parse out of Vanilla data/class. might not be worth it tho
            }

            /// <summary>Returns if the grid with the given id is aligned.</summary>
            /// <returns>if the target aligns, or if a vanilla weapon is shooting</returns>
            public override bool IsTargetAligned(long targetId, int weaponId = 0)
            {
                if (IsWC)
                {
                    return Api.IsTargetAligned(Weapon, targetId, weaponId);
                }
                return (Weapon as IMyLargeTurretBase).IsShooting; // Best estimate
            }
        }
    }
}