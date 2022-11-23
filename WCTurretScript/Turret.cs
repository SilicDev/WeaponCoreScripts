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
    public partial class Program : MyGridProgram
    {
        ///<summary>The player built turret.</summary>
        public class Turret
        {
            public static List<string> Names = new List<string>();

            private readonly static List<IMyTerminalBlock> s_helper = new List<IMyTerminalBlock>();

            // Components
            public string Name;
            public List<RotorController> Elevations = new List<RotorController>();
            public List<StaticWeaponController> StaticWeapons = new List<StaticWeaponController>();
            public RotorController Azimuth;
            public RotorController MainElevation;
            public TurretWeaponController Designator;
            public IMyTimerBlock Timer;
            public IMyBlockGroup BlockGroup;

            //vars
            public bool HasUpdated = false;
            public float MaxSpeed = 5;
            public int EngageDist = 1000;
            public float MaxDiverge = 1.5F;
            private int _sequenceIdx = 0;
            private int _offsetTimer = 0;
            private int _offsetTimerLength = 5;
            private bool _isResting = false;
            private bool _isAimed = false;
            private bool _shouldInvertRotation = false;
            private float _minimumOffenseRating = 1.0001f;
            private bool _strictGridTesting = true;

            private Vector3D _targetPos = Vector3D.Zero;

            /// <summary>Updates an old turret from the entered block group or creates a new one if none matches the block group</summary>
            /// <returns>The new turret, or null if an old turret was updated</returns>
            public static Turret AttemptCreateFromGroup(IMyBlockGroup blocks)
            {
                if (blocks != null)
                {
                    string name = blocks.Name;
                    if (Names.Contains(name))
                    {
                        AttemptUpdateFromGroup(blocks, name);
                        return null;
                    }
                    Turret newTurret = new Turret();
                    newTurret.Name = name;
                    Names.Add(name);
                    newTurret.UpdateTurret(blocks);
                    return newTurret;
                }
                return null;
            }

            /// <summary>Updates an already registered turret</summary>
            public static void AttemptUpdateFromGroup(IMyBlockGroup blocks, string name)
            {
                Turret oldTurret = Turrets.Find(t => t.Name.ToLower().Equals(name.ToLower()));
                oldTurret.UpdateTurret(blocks);
            }

            /// <summary>Updates the turret the turret from blocks in the entered block group</summary>
            public void UpdateTurret(IMyBlockGroup blocks)
            {
                s_helper.Clear();
                BlockGroup = blocks;
                if (Azimuth == null || Azimuth.Rotor == null)
                {
                    blocks.GetBlocksOfType<IMyMotorStator>(s_helper, b => !b.CustomName.Contains(ElevationNameTag)
                        && b.CustomName.Contains(AzimuthNameTag));
                    if (s_helper.Count == 0)
                        return;
                    Azimuth = new RotorController((IMyMotorStator)s_helper[0]);
                }

                #region INEFFICIENT
                Elevations.Clear();

                blocks.GetBlocksOfType<IMyMotorStator>(s_helper, b => b.CustomName.Contains(ElevationNameTag)
                    && !b.CustomName.Contains(AzimuthNameTag)
                    && (!_strictGridTesting || b.CubeGrid == Azimuth.Rotor.TopGrid));

                s_helper.ForEach(r => Elevations.Add(new RotorController((IMyMotorStator)r)));
                if (Elevations.Count == 0)
                    return;

                MainElevation = Elevations[0];
                if (MainElevation.Rotor.CustomName.Contains("#LEFT#"))
                    _shouldInvertRotation = true;

                StaticWeapons.Clear();
                if (_strictGridTesting)
                {
                    foreach (RotorController e in Elevations)
                    {
                        GetElevationBlocks(blocks, e.Rotor.TopGrid);
                    }
                }
                else
                {
                    GetElevationBlocks(blocks, null);
                }
                #endregion

                s_helper.Clear();
                blocks.GetBlocksOfType<IMyTerminalBlock>(s_helper, b => b.CustomName.Contains(DesignatorNameTag) && b.IsWorking);
                s_helper.Sort((lhs, rhs) => CompareByDistanceToAzimuth(lhs, rhs));
                IMyTerminalBlock tempDes = null;
                if (s_helper.Count != 0)
                {
                    tempDes = s_helper[0];
                    Designator = new TurretWeaponController(tempDes);
                }
                s_helper.Clear();
                blocks.GetBlocksOfType<IMyTimerBlock>(s_helper, b => b.CustomName.Contains(TimerNameTag));
                if (s_helper.Count != 0)
                    Timer = (IMyTimerBlock)s_helper[0];
                HasUpdated = true;
            }

            /// <summary>Collects the Weapons for the turret to use</summary>
            public void GetElevationBlocks(IMyBlockGroup blocks, IMyCubeGrid elevationGrid)
            {
                // Inefficient as heck
                s_helper.Clear();
                blocks.GetBlocksOfType<IMyFunctionalBlock>(s_helper, b => 
                        (StaticWeaponDefinitionSubIds.Contains(b.BlockDefinition.SubtypeName) || b is IMyUserControllableGun)
                        && (elevationGrid == null || b.CubeGrid == elevationGrid) && b.IsWorking);
                s_helper.ForEach(b => StaticWeapons.Add(new StaticWeaponController(b)));
            }

            /// <summary>Parses config data from CustomData.</summary>
            public void ParseTurretIni()
            {
                MaxSpeed = (float)ConfigIni.Get(Name, MAX_SPEED_KEY).ToDouble(MaxSpeed);
                EngageDist = ConfigIni.Get(Name, ENGAGE_DIST_KEY).ToInt32(EngageDist);
                MaxDiverge = ConfigIni.Get(Name, MAX_DIVERGE_KEY).ToSingle(MaxDiverge);
                _offsetTimerLength = ConfigIni.Get(Name, OFFSET_KEY).ToInt32(_offsetTimerLength);
                _shouldInvertRotation = ConfigIni.Get(Name, INVERT_ELEVATION_KEY).ToBoolean(_shouldInvertRotation);
                _minimumOffenseRating = ConvertThreatLevelToOffenseRating(ConfigIni.Get(Name, MINIMUM_THREAT_KEY).ToInt32(ConvertOffenseRatingToThreatLevel(_minimumOffenseRating)));
                _strictGridTesting = ConfigIni.Get(Name, STRICT_GRIDS_KEY).ToBoolean(_strictGridTesting);
            }

            /// <summary>Writes config data to CustomData.</summary>
            public void WriteTurretIni()
            {
                ConfigIni.Set(Name, MAX_SPEED_KEY, MaxSpeed);
                ConfigIni.Set(Name, ENGAGE_DIST_KEY, EngageDist);
                ConfigIni.Set(Name, MAX_DIVERGE_KEY, MaxDiverge);
                ConfigIni.Set(Name, OFFSET_KEY, _offsetTimerLength);
                ConfigIni.Set(Name, INVERT_ELEVATION_KEY, _shouldInvertRotation);
                ConfigIni.Set(Name, MINIMUM_THREAT_KEY, ConvertOffenseRatingToThreatLevel(_minimumOffenseRating));
                ConfigIni.Set(Name, STRICT_GRIDS_KEY, _strictGridTesting);
            }

            /// <summary>Main turret logic.</summary>
            /// If the turret is working, it will determine a target.
            /// Using the position of the target we calculate the direction the target is in.
            /// Using that vector we calculate the similarity between the aim vector of our lead weapon and
            /// adjust the rotation of the rotors to move towards the target.
            /// Once aimed the turret will fire at the target.
            /// Should the turret fail to detect a target it moves into its rest position instead.
            public void AimAtTarget()
            {
                if(!IsWorking())
                {
                    return;
                }
                MyDetectedEntityInfo? info = FindTarget();
                if (info.HasValue && !info.Value.IsEmpty())
                {
                    MyDetectedEntityInfo target = info.Value;
                    _targetPos = target.Position;
                    _isResting = false;
                    StaticWeaponController targetLead = StaticWeapons.Count != 0 ? StaticWeapons[0] : null;
                    if (MainElevation == null || Elevations.Count == 0 || Azimuth == null || targetLead == null)
                        return; // Turret incomplete

                    Vector3D middle = targetLead.GetPosition();
                    if (StaticWeapons.Count != 0)
                    {
                        StaticWeapons.ForEach(w =>
                        {
                            middle += (w.GetPosition() - middle) / 2;
                            _targetPos = w.CalculatePredictedTargetPosition(target.EntityId, _targetPos);
                        });
                    }

                    Vector3D targetVec = Vector3D.Normalize(_targetPos - middle);
                    double distance = (_targetPos - middle).Length();
                    if (distance <= EngageDist)
                    {
                        Vector3D aimVec = targetLead.WorldMatrix.Forward;
                        Vector3D down = Azimuth.Rotor.WorldMatrix.Down;

                        //Sets Rotor Angles
                        double armAngle = MathHelper.Clamp(Vector3D.Dot(aimVec, targetVec), -1, 1);
                        double hemiSphereAngle = Vector3D.Dot(Vector3D.Cross(aimVec, targetVec), down);
                        double armOffset = (-Math.Acos(armAngle)) * Math.Sign(hemiSphereAngle);

                        if (armOffset == double.NaN || double.IsInfinity(armOffset))
                        {
                            armOffset = 0;
                        }

                        Azimuth.SetRotorSpeedFromOffset(-(float)armOffset, 10, MaxSpeed);

                        double biggestUpperOffset = RotateElevators(targetVec);

                        _isAimed = IsAimed(armOffset, biggestUpperOffset);
                        if (_isAimed)
                        {
                            FireWeapons(target);
                        }
                        return;
                    }
                }
                MoveToRest();
            }

            /// <summary>Determines if the offsets of the rotor angles are within the diverge margin</summary>
            /// <returns>If the turret is currently aligned with the target.</returns>
            private bool IsAimed(double armOffset, double biggestUpperOffset)
            {
                return Math.Abs(armOffset) < MathHelper.ToRadians(MaxDiverge) && Math.Abs(biggestUpperOffset) < MathHelper.ToRadians(MaxDiverge);
            }

            /// <summary>Fires a weapon if the offset timer has run out. Also handles updating the timer</summary>
            private void FireWeapons(MyDetectedEntityInfo target)
            {
                if (_offsetTimer > 0)
                    _offsetTimer--;
                if (_offsetTimer == 0 && StaticWeapons.Count != 0)
                {
                    StaticWeaponController w = StaticWeapons[_sequenceIdx];
                    try
                    {
                        if (w.CanShootTarget(target.EntityId))
                        {
                            if (w.IsReady() && Timer != null && Timer.IsWorking && !Timer.IsCountingDown)
                                Timer.Trigger();
                            if (!AllowLOS || w.LineOfSightCheck(_targetPos, Azimuth.Rotor.CubeGrid))
                                w.ShootOnce();
                        };
                        _offsetTimer = _offsetTimerLength;
                        _sequenceIdx++;
                        if (_sequenceIdx >= StaticWeapons.Count)
                            _sequenceIdx = 0;
                    } 
                    catch (NullReferenceException)
                    {
                        // Threat was deleted, destroyed or simply left the target range
                        Targets.Remove(target);
                    }
                    catch (Exception e)
                    {
                        // Log any other exceptions as they are not expected
                        // Saved even if Debug is disabled as we might want to inspect errors that occured when they occur without running a test scenario
                        ErrorLog.Add($"Turret {Name} error: FireWeapons: {e}\n");
                    }
                }
            }

            /// <summary>Sets the rotation for the elevation rotors to align the weapons with the target.</summary>
            /// <returns>the biggest angle offset to the target among the rotors</returns>
            private double RotateElevators(Vector3D targetVec)
            {
                StaticWeaponController targetLead = StaticWeapons.Count != 0 ? StaticWeapons[0] : null;
                Vector3D down = Azimuth.Rotor.WorldMatrix.Down;
                double biggestUpperOffset = 0;
                if (Elevations.Count != 0)
                {
                    Elevations.ForEach(e =>
                    {
                        StaticWeapons.ForEach(w =>
                        {
                            if (e != null && w.CubeGrid.Equals(e.Rotor.TopGrid))
                                targetLead = w;
                        });
                        Vector3D aimVec = targetLead.WorldMatrix.Forward;

                        double aimUpperAngle = Math.Acos(Vector3D.Dot(aimVec, -down));
                        double targetUpperAngle = Math.Acos(Vector3D.Dot(targetVec, -down));
                        double upperOffset = aimUpperAngle - targetUpperAngle;

                        if (upperOffset == double.NaN || double.IsInfinity(upperOffset))
                        {
                            upperOffset = 0;
                        }

                        if (upperOffset > biggestUpperOffset)
                            biggestUpperOffset = Math.Abs(upperOffset);

                        int invFac = _shouldInvertRotation ? -1 : 1;
                        e.RotateElevation((float)upperOffset * invFac, MaxSpeed, MainElevation.Rotor.WorldMatrix.Up);
                    });
                }

                return biggestUpperOffset;
            }

            /// <summary>Determines a target for this turret using the best available detection method.</summary>
            /// <returns>The Detected Entity to target or null</returns>
            private MyDetectedEntityInfo? FindTarget()
            {
                if (Designator != null)
                {
                    if (Api.HasCoreWeapon(Designator.Weapon))
                    {
                        return Api.GetWeaponTarget(Designator.Weapon);
                    }
                    else if (Designator is IMyLargeTurretBase)
                    {
                        return (Designator as IMyLargeTurretBase).GetTargetedEntity();
                    }
                }
                if (AllowWcTargeting)
                {
                    MyDetectedEntityInfo? info = Api.GetAiFocus(Azimuth.Rotor.CubeGrid.EntityId);
                    if (info.HasValue && !info.Value.IsEmpty() && !(info.Value.TimeStamp < (DateTime.Now.TimeOfDay - new TimeSpan(0, 0, 10)).TotalMilliseconds))
                        return info;
                }
                double highestScore = -1;
                if (Targets.Count != 0) {
                    MyDetectedEntityInfo? info = null;
                    foreach (MyDetectedEntityInfo k in Targets.Keys)
                    {
                        if (!k.IsEmpty() && IsValidTarget(k, Targets[k]))
                        {
                            float distance = (float)(k.Position - Azimuth.GetPosition()).Length();
                            if (distance <= EngageDist)
                            {
                                double targetScore = CalculateTargetScore(Targets[k], distance);
                                if (targetScore > highestScore && StaticWeapons[0].CanShootTarget(k.EntityId))
                                {
                                    highestScore = targetScore;
                                    info = k;
                                }
                            }
                        }
                    }
                    return info;
                }
                return null;
            }

            /// <summary>Calculates the priority score for the given threat and distance</summary>
            /// <returns>the priority score</returns>
            private double CalculateTargetScore(float threat, float distance)
            {
                return threat * 1000 + MyMath.Clamp(5000 - distance, 0, 5000);
            }

            /// <summary>Determines if a given target matches the config parameters for this turret</summary>
            /// <returns>If the target is valid</returns>
            public bool IsValidTarget(MyDetectedEntityInfo target, float offenseRating)
            {
                return offenseRating >= _minimumOffenseRating &&
                    target.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies;
            }

            /// <summary>Moves the turret back to a neutral position</summary>
            public void MoveToRest()
            {
                _sequenceIdx = 0;
                _offsetTimer = 0;
                _isResting = true;
                _isAimed = false;
                if (!IsWorking())
                {
                    return; // Early return if broken
                }
                Azimuth.MoveToRest(MaxSpeed);
                Elevations.ForEach(e => e.MoveToRest(MaxSpeed));
            }

            /// <summary>Performs basic checks if this turret is considered complete and ready to be used.
            /// Appends errors to the EchoString if blocks are missing.</summary>
            /// <returns>If this turret has the minimum blocks it requires.</returns>
            public bool IsWorking()
            {
                if (Azimuth == null || Azimuth.Rotor == null)
                {
                    EchoString.Append("No AZIMUTH rotor found!\n");
                    return false;
                }
                if (MainElevation == null || MainElevation.Rotor == null)
                {
                    EchoString.Append("No ELEVATION rotor found!\n");
                    return false;
                }
                if (StaticWeapons.Count == 0)
                {
                    EchoString.Append("No WEAPONS found!\n");
                    return false;
                }
                return true;
            }

            /// <summary>Appends debug information to the EchoString.</summary>
            public void Debug()
            {
                EchoString.Append($"Debug for turret group: {Name}\n");
                EchoString.Append($"Resting: {_isResting}\n");
                EchoString.Append($"Is Aimed: {_isAimed}\n");
                EchoString.Append($"Elevation Reversed: {_shouldInvertRotation}\n");
                EchoString.Append($"Engagement Distance: {EngageDist}\n");
                EchoString.Append($"Minimum Target Threat Level: {ConvertOffenseRatingToThreatLevel(_minimumOffenseRating)}\n");
                int wc = 0;
                int v = 0;
                StaticWeapons.ForEach(w => {
                    if (w.IsWC) 
                        wc++;
                    else
                        v++;
                    });
                EchoString.Append($"{wc} WC weapons\n{v} Vanilla Weapons\n");
                EchoString.AppendLine();
            }

            private int CompareByDistanceToAzimuth(IMyCubeBlock lhs, IMyCubeBlock rhs) =>
                    (lhs.Position - Azimuth.Rotor.Position).Length() - (rhs.Position - Azimuth.Rotor.Position).Length();

            //https://github.com/SilicDev/WeaponCore/blob/master/Data/Scripts/CoreSystems/Ui/Targeting/TargetUiDraw.cs#L966-L976
            private int ConvertOffenseRatingToThreatLevel(float offenseRating)
            {
                if (offenseRating > 5) return 9;
                if (offenseRating > 4) return 8;
                if (offenseRating > 3) return 7;
                if (offenseRating > 2) return 6;
                if (offenseRating > 1) return 5;
                if (offenseRating > 0.5) return 4;
                if (offenseRating > 0.25) return 3;
                if (offenseRating > 0.125) return 2;
                if (offenseRating > 0.0625) return 1;
                if (offenseRating > 0) return 0;
                return -1;
            }

            //https://github.com/SilicDev/WeaponCore/blob/master/Data/Scripts/CoreSystems/Ui/Targeting/TargetUiDraw.cs#L966-L976
            private float ConvertThreatLevelToOffenseRating(int threatLevel)
            {
                switch (threatLevel)
                {
                    case 9: return 6;
                    case 8: return 5;
                    case 7: return 4;
                    case 6: return 3;
                    case 5: return 2;
                    case 4: return 1;
                    case 3: return 0.5f;
                    case 2: return 0.25f;
                    case 1: return 0.125f;
                    case 0: return 0.0625f;
                    default: return -1;
                }
            }
        }
    }
}
