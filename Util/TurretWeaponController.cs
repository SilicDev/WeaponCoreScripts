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
        class TurretWeaponController : WeaponController
        {
            public enum Threat
            {
                Projectiles,
                Characters,
                Grids,
                Neutrals,
                Meteors,
                Other
            }

            ///<summary>Indicates whether a block is locally or remotely controlled.</summary>
            public bool IsUnderControl { 
                get
                {
                    if (IsWC)
                        return API.GetPlayerController(Weapon) != -1;
                    return (Weapon as IMyLargeTurretBase).IsUnderControl;
                }
            }

            ///<summary>Gets and Sets shooting range of the turret</summary>
            public float Range { 
                get 
                { 
                    if (IsWC)
                    {
                        if (_range == -1)
                        {
                            _range = GetMaxRange();
                        }
                        return _range;
                    }
                    return (Weapon as IMyLargeTurretBase).Range; 
                } 
                set 
                {
                    if (IsWC)
                    {
                        API.SetBlockTrackingRange(Weapon, value);
                        _range = value;
                    }
                    else
                        (Weapon as IMyLargeTurretBase).Range = value; 
                }
            }

            private float _range = -1;

            ///<summary>Gets/sets if the turret should target characters.</summary>
            public bool TargetCharacters {
                get { return GetTargetType(Threat.Characters); }
                set { SetTargetType(Threat.Characters, value); }
            }

            ///<summary>Gets/sets if the turret should target large grids.</summary>
            public bool TargetLargeGrids
            {
                get { return GetTargetType(Threat.Grids); }
                set { SetTargetType(Threat.Grids, value); }
            }

            ///<summary>Gets/sets if the turret should target meteors.</summary>
            public bool TargetMeteors
            {
                get { return GetTargetType(Threat.Meteors); }
                set { SetTargetType(Threat.Meteors, value); }
            }

            ///<summary>Gets/sets if the turret should target missiles.</summary>
            public bool TargetMissiles
            {
                get { return GetTargetType(Threat.Projectiles); }
                set { SetTargetType(Threat.Projectiles, value); }
            }

            ///<summary>Gets/sets if the turret should target neutrals.</summary>
            public bool TargetNeutrals
            {
                get { return GetTargetType(Threat.Neutrals); }
                set { SetTargetType(Threat.Neutrals, value); }
            }

            ///<summary>Gets/sets if the turret should target small grids.</summary>
            public bool TargetSmallGrids
            {
                get { return GetTargetType(Threat.Grids); }
                set { SetTargetType(Threat.Grids, value); }
            }

            ///<summary>Gets/sets if the turret should target stations.</summary>
            public bool TargetStations
            {
                get { return GetTargetType(Threat.Grids); }
                set { SetTargetType(Threat.Grids, value); }
            }

            private readonly List<string> _types = new List<string>();

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
                    return API.GetWeaponTarget(Weapon);
                }
                return (Weapon as IMyLargeTurretBase).GetTargetedEntity();
            }

            /// <summary>Returns the maximum range of this weapon.</summary>
            /// <returns>the maximum range of this weapon</returns>
            public override float GetMaxRange(int weaponId = 0)
            {
                if (IsWC)
                {
                    return API.GetMaxWeaponRange(Weapon, weaponId);
                }
                return (Weapon as IMyLargeTurretBase).Range; // TODO: parse out of Vanilla data/class. might not be worth it tho
            }

            /// <summary>Returns the current target types of the selected weapon.</summary>
            public bool GetTargetType(Threat targetType, int weaponId = 0)
            {
                if (IsWC)
                {
                    API.GetTurretTargetTypes(Weapon, _types, weaponId);
                    return _types.Contains(targetType.ToString());
                }
                switch(targetType)
                {
                    case Threat.Projectiles:
                        return (Weapon as IMyLargeTurretBase).TargetMissiles;

                    case Threat.Characters:
                        return (Weapon as IMyLargeTurretBase).TargetCharacters;

                    case Threat.Grids:
                        return (Weapon as IMyLargeTurretBase).TargetSmallGrids ||
                            (Weapon as IMyLargeTurretBase).TargetLargeGrids ||
                            (Weapon as IMyLargeTurretBase).TargetStations;

                    case Threat.Neutrals:
                        return (Weapon as IMyLargeTurretBase).TargetNeutrals;

                    case Threat.Meteors:
                        return (Weapon as IMyLargeTurretBase).TargetMeteors;
                }
                return false;
            }

            /// <summary>Sets the current target types of the selected weapon.</summary>
            public void SetTargetType(Threat targetType, bool onOff, int weaponId = 0)
            {
                if (IsWC)
                {
                    API.GetTurretTargetTypes(Weapon, _types, weaponId);
                    if (onOff)
                    {
                        if (!_types.Contains(targetType.ToString()))
                            _types.Add(targetType.ToString());
                    }
                    else
                    {
                        _types.RemoveAll(s => s.Equals(targetType.ToString()));
                    }
                }
                switch (targetType)
                {
                    case Threat.Projectiles:
                        (Weapon as IMyLargeTurretBase).TargetMissiles = onOff;
                        break;

                    case Threat.Characters:
                        (Weapon as IMyLargeTurretBase).TargetCharacters = onOff;
                        break;

                    case Threat.Grids:
                       (Weapon as IMyLargeTurretBase).TargetSmallGrids =
                            (Weapon as IMyLargeTurretBase).TargetLargeGrids =
                            (Weapon as IMyLargeTurretBase).TargetStations = onOff;
                        break;

                    case Threat.Neutrals:
                        (Weapon as IMyLargeTurretBase).TargetNeutrals = onOff;
                        break;

                    case Threat.Meteors:
                        (Weapon as IMyLargeTurretBase).TargetMeteors = onOff;
                        break;
                }
            }
        }
    }
}
