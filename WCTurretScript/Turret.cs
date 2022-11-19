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
            private static List<IMyTerminalBlock> s_helper = new List<IMyTerminalBlock>();
            public string Name;
            public List<RotorController> Elevations = new List<RotorController>();
            public List<StaticWeaponController> StaticWeapons = new List<StaticWeaponController>();
            public RotorController Azimuth;
            public RotorController MainElevation;
            public TurretWeaponController Designator;
            public IMyTimerBlock Timer;
            public IMyBlockGroup BlockGroup;

            public bool HasUpdated = false;
            public double MaxSpeed = 5;
            public int EngageDist = 1000;
            public float MaxDiverge = 1.5F;
            private int _sequenceTimer = 0;
            private int _offsetTimer = 0;
            private int _offsetTimerLength = 5;
            private bool _isResting = false;
            private bool _shouldInvertRotation = false;

            private Vector3D _targetPos = Vector3D.Zero;

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

            public static void AttemptUpdateFromGroup(IMyBlockGroup blocks, string name)
            {
                Turret oldTurret = Turrets.Find(t => t.Name.ToLower().Equals(name.ToLower()));
                oldTurret.UpdateTurret(blocks);
            }

            public void UpdateTurret(IMyBlockGroup blocks)
            {
                s_helper.Clear();
                BlockGroup = blocks;
                if (Azimuth == null)
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
                    && !b.CustomName.Contains(AzimuthNameTag) && b.CubeGrid == Azimuth.Rotor.TopGrid);
                s_helper.ForEach(r => Elevations.Add(new RotorController((IMyMotorStator)r)));
                if (Elevations.Count == 0)
                    return;
                MainElevation = Elevations[0];
                if (MainElevation.Rotor.CustomName.Contains("#LEFT#"))
                    _shouldInvertRotation = true;
                foreach (RotorController e in Elevations)
                {
                    GetElevationBlocks(blocks, e.Rotor);
                }
                #endregion

                s_helper.Clear();
                blocks.GetBlocksOfType<IMyTerminalBlock>(s_helper, b => b.CustomName.Contains(DesignatorNameTag));
                s_helper.RemoveAll(d => !d.IsWorking);
                s_helper.Sort((lhs, rhs) => CompareByDistance(lhs, rhs));
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

            public void GetElevationBlocks(IMyBlockGroup blocks, IMyMotorStator e)
            {
                // Inefficient as heck
                StaticWeapons.Clear();
                s_helper.Clear();
                blocks.GetBlocksOfType<IMyFunctionalBlock>(s_helper, b => StaticWeaponDefinitionSubIds.Contains(b.BlockDefinition.SubtypeName) && b.CubeGrid == e.TopGrid);
                s_helper.ForEach(b => StaticWeapons.Add(new StaticWeaponController(b)));
                s_helper.Clear();
                blocks.GetBlocksOfType<IMyUserControllableGun>(s_helper, b => b.CubeGrid == e.TopGrid);
                s_helper.RemoveAll(x => StaticWeaponDefinitionSubIds.Contains(x.BlockDefinition.SubtypeName));
                s_helper.ForEach(b => StaticWeapons.Add(new StaticWeaponController(b)));
            }

            public void ParseTurretIni()
            {
                MaxSpeed = ConfigIni.Get(Name, MAX_SPEED_KEY).ToDouble(MaxSpeed);
                EngageDist = ConfigIni.Get(Name, ENGAGE_DIST_KEY).ToInt32(EngageDist);
                MaxDiverge = ConfigIni.Get(Name, MAX_DIVERGE_KEY).ToSingle(MaxDiverge);
                _offsetTimerLength = ConfigIni.Get(Name, OFFSET_KEY).ToInt32(_offsetTimerLength);
                _shouldInvertRotation = ConfigIni.Get(Name, INVERT_ELEVATION_KEY).ToBoolean(_shouldInvertRotation);
            }

            public void WriteTurretIni()
            {
                ConfigIni.Set(Name, MAX_SPEED_KEY, MaxSpeed);
                ConfigIni.Set(Name, ENGAGE_DIST_KEY, EngageDist);
                ConfigIni.Set(Name, MAX_DIVERGE_KEY, MaxDiverge);
                ConfigIni.Set(Name, OFFSET_KEY, _offsetTimerLength);
                ConfigIni.Set(Name, INVERT_ELEVATION_KEY, _shouldInvertRotation);
            }

            public void AimAtTarget()
            {
                MyDetectedEntityInfo? info = FindTarget();
                if (info != null && info.HasValue && !info.Value.IsEmpty())
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

                        Azimuth.SetRotorSpeedFromOffset(-(float)armOffset, 10, (float)MaxSpeed);

                        double biggestUpperOffset = RotateElevators(targetVec);

                        if (IsAimed(armOffset, biggestUpperOffset))
                        {
                            FireWeapons(target);
                        }
                        return; //Early return if targeting, but not aimed
                    }
                }
                MoveToRest();
            }

            private bool IsAimed(double armOffset, double biggestUpperOffset)
            {
                return Math.Abs(armOffset) < MathHelper.ToRadians(MaxDiverge) && Math.Abs(biggestUpperOffset) < MathHelper.ToRadians(MaxDiverge);
            }

            private void FireWeapons(MyDetectedEntityInfo target)
            {
                if (_offsetTimer > 0)
                    _offsetTimer--;
                if (_offsetTimer == 0 && StaticWeapons.Count != 0)
                {
                    StaticWeaponController w = StaticWeapons[_sequenceTimer];
                    if (w.CanShootTarget(target.EntityId))
                    {
                        if (w.IsReady() && Timer != null && Timer.IsWorking && !Timer.IsCountingDown)
                            Timer.Trigger();
                        if (!AllowLOS || w.LineOfSightCheck(_targetPos, Azimuth.Rotor.CubeGrid))
                            w.ShootOnce();
                    };
                    _offsetTimer = _offsetTimerLength;
                    _sequenceTimer++;
                    if (_sequenceTimer >= StaticWeapons.Count)
                        _sequenceTimer = 0;
                }
            }

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
                        Vector3D muzzleVec = targetLead.WorldMatrix.Forward;
                        double aimUpperAngle = Math.Acos(Vector3D.Dot(muzzleVec, -down));
                        double targetUpperAngle = Math.Acos(Vector3D.Dot(targetVec, -down));
                        double upperOffset = aimUpperAngle - targetUpperAngle;
                        if (upperOffset == double.NaN || double.IsInfinity(upperOffset))
                        {
                            upperOffset = 0;
                        }
                        if (upperOffset > biggestUpperOffset)
                            biggestUpperOffset = Math.Abs(upperOffset);
                        int invFac = _shouldInvertRotation ? -1 : 1;
                        e.RotateElevation((float)upperOffset * invFac, (float)MaxSpeed, MainElevation.Rotor.WorldMatrix.Up);
                    });
                }

                return biggestUpperOffset;
            }

            private MyDetectedEntityInfo? FindTarget()
            {
                if (Designator != null)
                {
                    if (Api.HasCoreWeapon(Designator))
                    {
                        return Api.GetWeaponTarget(Designator);
                    }
                    else if (Designator is IMyLargeTurretBase)
                    {
                        return (Designator as IMyLargeTurretBase).GetTargetedEntity();
                    }
                }
                if (AllowWcTargeting)
                {
                    MyDetectedEntityInfo? info = Api.GetAiFocus(Azimuth.Rotor.CubeGrid.EntityId);
                    if (info != null && info.HasValue && !info.Value.IsEmpty() && !(info.Value.TimeStamp < (DateTime.Now.TimeOfDay - new TimeSpan(0, 0, 10)).TotalMilliseconds))
                        return info;
                }
                double highestScore = 0;
                if (Targets.Count != 0) {
                    MyDetectedEntityInfo? info = null;
                    foreach (MyDetectedEntityInfo k in Targets.Keys)
                    {
                        if (!k.IsEmpty() && ((int)k.Relationship) == 1)
                        {
                            double distance = (k.Position - Azimuth.GetPosition()).Length();
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
            private double CalculateTargetScore(float threat, double distance)
            {
                return threat * 1000 + (5000 - distance);
            }


            public void MoveToRest()
            {
                _sequenceTimer = 0;
                _offsetTimer = 0;
                _isResting = true;
                Azimuth.MoveToRest((float)MaxSpeed);
                Elevations.ForEach(e => e.MoveToRest((float)MaxSpeed));
            }

            public void Debug()
            {
                EchoString.Append($"Debug for turret group: {Name}\n");
                EchoString.Append($"Resting: {_isResting}\n");
                EchoString.Append($"Elevation Reversed: {_shouldInvertRotation}\n");
                EchoString.Append($"Engagement Distance: {EngageDist}\n");
                if (Azimuth == null)
                {
                    EchoString.Append("No AZIMUTH rotor found!\n");
                }
                if (MainElevation == null)
                {
                    EchoString.Append("No ELEVATION rotor found!\n");
                }
                if (StaticWeapons.Count == 0)
                {
                    EchoString.Append("No WEAPONS found!\n");
                }
                else
                {
                    int wc = 0;
                    int v = 0;
                    StaticWeapons.ForEach(w => {
                        if (w.IsWC) 
                            wc++;
                        else
                            v++;
                        });
                    EchoString.Append($"{wc} WC weapons\n{v} Vanilla Weapons\n");
                }
                EchoString.Append("\n");
            }

            public int CompareByDistance(IMyCubeBlock lhs, IMyCubeBlock rhs) =>
                    (lhs.Position - Azimuth.Rotor.Position).Length() - (rhs.Position - Azimuth.Rotor.Position).Length();
        }
    }
}
