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
        public class WeaponController
        {
            /// <summary>The weapon controlled by this instance.</summary>
            public IMyTerminalBlock Weapon { get; }
            /// <summary>Whether or not this instance holds a WC weapon</summary>
            public bool IsWC { get; }

            public bool IsShooting
            {
                get
                {
                    if (IsWC)
                    {
                        //undefined, taking best assumption
                        MyDetectedEntityInfo? info = Api.GetWeaponTarget(Weapon);
                        return _shoot || (info.HasValue && Api.IsTargetAligned(Weapon, info.Value.EntityId, 0));
                    }
                    return (Weapon as IMyUserControllableGun).IsShooting;
                }
            }

            /// <summary>Toggle shooting on or off.</summary>
            public bool Shoot
            {
                get
                {
                    return IsShootToggleOn();
                }
                set
                {
                    ToggleShooting(value);
                }
            }

            private bool _shoot = false;

            /// <summary>Shortcut for IMyCubeBlock.CubeGrid</summary>
            public IMyCubeGrid CubeGrid
            {
                get
                {
                    return Weapon.CubeGrid;
                }
            }

            /// <summary>Shortcut for IMyEntity.WorldMatrix</summary>
            public MatrixD WorldMatrix
            {
                get
                {
                    return Weapon.WorldMatrix;
                }
            }
            
            /// <summary>Shortcut for IMyCubeBlock.Position</summary>
            public Vector3I Position
            {
                get
                {
                    return Weapon.Position;
                }
            }

            /// <summary>Creates a new controller for the weapon.</summary>
            public WeaponController(IMyTerminalBlock weapon)
            {
                Weapon = weapon;
                IsWC = Api.HasCoreWeapon(Weapon);
            }

            public void ShootOnce(int weaponId = 0)
            {
                if (IsWC)
                {
                    Api.FireWeaponOnce(Weapon, false, weaponId);
                }
                else
                {
                    Weapon.ApplyAction("ShootOnce");
                }
            }

            public virtual MyDetectedEntityInfo? GetTarget()
            {
                if (IsWC)
                {
                    return Api.GetWeaponTarget(Weapon);
                }
                return null;
            }

            /// <summary>Checks if the weapon is ready to shoot.</summary>
            /// <returns>if the weapon is ready to fire (always true for vanilla style weapons)</returns>
            public bool IsReady()
            {
                if (IsWC)
                {
                    return Api.IsWeaponReadyToFire(Weapon, 0, true, true);
                }
                return Weapon.IsWorking;
            }

            /// <summary>Returns the maximum range of this weapon.</summary>
            /// <returns>the maximum range of this weapon (always 800.0 for vanilla style weapons)</returns>
            public virtual float GetMaxRange(int weaponId = 0)
            {
                if (IsWC)
                {
                    return Api.GetMaxWeaponRange(Weapon, weaponId);
                }
                return 800.0f; // TODO: parse out of Vanilla data/class. might not be worth it tho
            }

            public bool IsTargetAligned(long targetId, int weaponId = 0)
            {
                if (IsWC)
                {
                    return Api.IsTargetAligned(Weapon, targetId, weaponId);
                }
                return (Weapon as IMyUserControllableGun).IsShooting;
            }

            /// <summary>Checks if the weapon can shoot the given target.</summary>
            /// <returns>if the weapon can shoot the given target (always true for vanilla style weapons)</returns>
            public bool CanShootTarget(long targetId, int weaponId = 0)
            {
                if (IsWC)
                {
                    return Api.CanShootTarget(Weapon, targetId, weaponId);
                }
                return Weapon.IsWorking;
            }

            /// <summary>Shortcut for IMyEntity.GetPosition()</summary>
            public Vector3 GetPosition()
            {
                return Weapon.GetPosition();
            }

            private bool IsShootToggleOn()
            {
                if (IsWC)
                {
                    return _shoot;
                }
                return (Weapon as IMyUserControllableGun).Shoot;
            }

            private void ToggleShooting(bool on)
            {
                if (IsWC)
                {
                    Api.ToggleWeaponFire(Weapon, on, false);
                    _shoot = on;
                }
                else
                {
                    (Weapon as IMyUserControllableGun).Shoot = on;
                }
            }
        }
    }
}