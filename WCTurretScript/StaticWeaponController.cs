using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class StaticWeaponController
        {
            /// <summary>The weapon controlled by this instance.</summary>
            public IMyTerminalBlock Weapon { get; }
            /// <summary>Whether or not this instance holds a WC weapon</summary>
            public bool isWC { get; }

            public IMyCubeGrid CubeGrid { get
                {
                    return Weapon.CubeGrid;
                }
            }

            public MatrixD WorldMatrix
            {
                get
                {
                    return Weapon.WorldMatrix;
                }
            }

            /// <summary>Creates a new controller for the weapon.</summary>
            public StaticWeaponController(IMyTerminalBlock weapon)
            {
                Weapon = weapon;
                isWC = api.HasCoreWeapon(Weapon);
            }

            /// <summary>Triggers a single shot.</summary>
            public void ShootOnce()
            {
                if(isWC)
                {
                    //api.FireWeaponOnce(Weapon, false);
                    api.ToggleWeaponFire(Weapon, true, false);
                }
                else
                {
                    Weapon.ApplyAction("ShootOnce");
                }
            }

            public void ToggleShoot(bool on)
            {
                if(isWC)
                {
                    api.ToggleWeaponFire(Weapon, on, false);
                }
                else
                {
                    (Weapon as IMyUserControllableGun).Shoot = on;
                }
            }

            /// <summary>Checks if the weapon is ready to shoot.</summary>
            /// <returns>if the weapon is ready to fire (always true for vanilla style weapons)</returns>
            public bool IsReady()
            {
                if(isWC)
                {
                    return api.IsWeaponReadyToFire(Weapon, 0, true, true);
                }
                return true;
            }

            public bool CanShootTarget(long targetId)
            {
                if(isWC)
                {
                    return api.CanShootTarget(Weapon, targetId, 0);
                }
                return true;
            }

            public bool LineOfSightCheck(Vector3 targetpos, IMyCubeGrid parentGrid)
            {
                Vector3 step = Weapon.WorldMatrix.Forward;
                Vector3 temp = GetPosition();
                while ((temp - GetPosition()).Length() < (targetpos - GetPosition()).Length())
                {
                    temp += step;
                    if (parentGrid.CubeExists(parentGrid.WorldToGridInteger(temp)))
                    {
                        return false;
                    }
                }
                return true;
            }

            public Vector3 CalculatePredictedTargetPosition(long shooterGridId, Vector3 targetpos)
            {
                if (isWC)
                {
                    MyDetectedEntityInfo? info = api.GetAiFocus(shooterGridId);
                    if (info.HasValue && !info.Value.IsEmpty())
                    {
                        var targetposraw = api.GetPredictedTargetPosition(Weapon, info.Value.EntityId, 0);
                        if (targetposraw != null && targetposraw.HasValue)
                            return (targetpos + targetposraw.Value) / 2;
                    }
                }
                return targetpos;
            }

            public Vector3 GetPosition()
            {
                return Weapon.GetPosition();
            }
        }
    }
}
