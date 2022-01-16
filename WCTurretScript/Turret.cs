using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        ///<summary>The player built turret.</summary>
        public class Turret
        {
            public static List<string> Names = new List<string>();
            public static List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
            public string Name;
            public List<RotorController> Elevations = new List<RotorController>();
            public List<IMyTerminalBlock> StaticWeapons = new List<IMyTerminalBlock>();
            public List<IMyUserControllableGun> StaticVanilla = new List<IMyUserControllableGun>();
            public RotorController Azimuth;
            public RotorController MainElevation;
            public IMyTerminalBlock Designator;
            public IMyTimerBlock Timer;
            public IMyBlockGroup bGroup;

            public List<IMyTerminalBlock> designatorWcCandidates = new List<IMyTerminalBlock>();
            public List<IMyLargeTurretBase> designatorCandidates = new List<IMyLargeTurretBase>();

            public bool hasUpdated = false;
            public double maxSpeed = 5;
            public int engageDist = 1000;
            public float maxDiverge = 1.5F;
            private int sequenceTimerWC = 0;
            private int sequenceTimerV = 0;
            private int offsetTimer = 0;
            private int offset = 5;
            private bool lastFiredWC = false;
            private bool resting = false;
            private bool invertRot = false;

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
                Turret oldTurret = turrets.Find(t => t.Name.ToLower().Equals(name.ToLower()));
                oldTurret.UpdateTurret(blocks);
            }

            public void UpdateTurret(IMyBlockGroup blocks)
            {
                temp.Clear();
                bGroup = blocks;
                if (Azimuth == null)
                {
                    blocks.GetBlocksOfType<IMyMotorStator>(temp, b => !b.CustomName.Contains(ElevationNameTag)
                        && b.CustomName.Contains(AzimuthNameTag));
                    if (temp.Count == 0)
                        return;
                    Azimuth = new RotorController((IMyMotorStator)temp[0]);
                }

                #region INEFFICIENT
                this.Elevations.Clear();
                blocks.GetBlocksOfType<IMyMotorStator>(temp, b => b.CustomName.Contains(ElevationNameTag)
                    && !b.CustomName.Contains(AzimuthNameTag) && b.CubeGrid == Azimuth.Rotor.TopGrid);
                temp.ForEach(r => Elevations.Add(new RotorController((IMyMotorStator)r)));
                if (Elevations.Count == 0)
                    return;
                MainElevation = Elevations[0];
                if (MainElevation.Rotor.CustomName.Contains("#LEFT#"))
                    invertRot = true;
                foreach (RotorController e in Elevations)
                {
                    getElevationBlocks(blocks, e.Rotor);
                }
                #endregion

                blocks.GetBlocksOfType<IMyTerminalBlock>(temp, b => b.CustomName.Contains(DesignatorNameTag));
                temp.Sort((lhs, rhs) => CompareByDistance(lhs, rhs));
                IMyTerminalBlock tempDes = null;
                if (temp.Count != 0)
                {
                    tempDes = temp[0];
                    if (tempDes != null && (tempDes is IMyLargeTurretBase || api.HasCoreWeapon(tempDes)))
                        if (tempDes.IsWorking)
                        {
                            Designator = tempDes;
                        }
                        else
                        {
                            getDesignators();
                        }
                }
                else
                {
                    getDesignators();
                }
                blocks.GetBlocksOfType<IMyTimerBlock>(temp, b => b.CustomName.Contains(TimerNameTag));
                if (temp.Count != 0)
                    this.Timer = (IMyTimerBlock)temp[0];
                this.hasUpdated = true;
            }

            public void getElevationBlocks(IMyBlockGroup blocks, IMyMotorStator e)
            {
                blocks.GetBlocksOfType<IMyTerminalBlock>(StaticWeapons, b => definitionSubIds.Contains(b.BlockDefinition.SubtypeName));
                blocks.GetBlocksOfType<IMyUserControllableGun>(StaticVanilla, b => true);
                StaticVanilla.RemoveAll(x => (StaticWeapons.Contains(x) || definitionSubIds.Contains(x.BlockDefinition.SubtypeName)));
            }

            public void getDesignators()
            {
                designatorWcCandidates.Clear();
                designatorWcCandidates = WC_DIRECTORS.Where(d =>
                {
                    MyDetectedEntityInfo? target = api.GetWeaponTarget(d, 0);
                    if (target != null && target.HasValue && !(target.Value.IsEmpty()))
                    {
                        return ((IMyFunctionalBlock)d).IsWorking && api.IsTargetAligned(d, target.Value.EntityId, 0);
                    }
                    else
                    {
                        return false;
                    }
                }).ToList();
                designatorWcCandidates.Sort((lhs, rhs) => CompareByDistance(lhs, rhs));

                Designator = designatorWcCandidates.Any() ? designatorWcCandidates[0] : null;
                if (Designator == null)
                {
                    designatorCandidates.Clear();
                    designatorCandidates = DIRECTORS.Where(d =>
                    {
                    //Does this also need to be changed?
                    long? id = d.GetTargetedEntity().EntityId;
                        if (id != null)
                        {
                            return d.IsWorking;
                        }
                        else
                        {
                            return false;
                        }
                    }).ToList();
                    designatorCandidates.Sort((lhs, rhs) => CompareByDistance(lhs, rhs));
                    Designator = designatorCandidates.Any() ? designatorCandidates[0] : null;
                }
            }

            public void ParseTurretIni()
            {
                maxSpeed = generalIni.Get(Name, maxSpeedKey).ToDouble(maxSpeed);
                engageDist = generalIni.Get(Name, engageDistKey).ToInt32(engageDist);
                maxDiverge = generalIni.Get(Name, maxDivergeKey).ToSingle(maxDiverge);
                offset = generalIni.Get(Name, offsetKey).ToInt32(offset);
                invertRot = generalIni.Get(Name, invertRotKey).ToBoolean(invertRot);
            }

            public void WriteTurretIni()
            {
                generalIni.Set(Name, maxSpeedKey, maxSpeed);
                generalIni.Set(Name, engageDistKey, engageDist);
                generalIni.Set(Name, maxDivergeKey, maxDiverge);
                generalIni.Set(Name, offsetKey, offset);
                generalIni.Set(Name, invertRotKey, invertRot);
            }

            /*
             * Aims at target using WC Grid Ai Focus
             * @param targetpos: WC Grid Ai Focus Position
             */

            public void AimAtTarget(Vector3D targetpos)
            {
                resting = false;
                var targetLead = StaticWeapons.Count != 0 ? StaticWeapons[0] : StaticVanilla.Count != 0 ? StaticVanilla[0] : null;
                if (MainElevation == null || Elevations.Count == 0 || Azimuth == null || targetLead == null)
                    return;
                Vector3D middle = targetLead.GetPosition();
                if (StaticWeapons.Count != 0)
                {
                    StaticWeapons.ForEach(w =>
                    {
                        middle = middle + Vector3D.Multiply((w.GetPosition() - middle), 0.5);
                        MyDetectedEntityInfo? info = api.GetAiFocus(Azimuth.Rotor.CubeGrid.EntityId);
                        if (info.HasValue && !(info.Value.IsEmpty()))
                        {
                            var targetposraw = api.GetPredictedTargetPosition(w, info.Value.EntityId, 0);
                            if (targetposraw == null)
                                return;
                            targetpos = (Vector3D)targetposraw;
                        }
                    });
                }
                if (StaticVanilla.Count != 0)
                    StaticVanilla.ForEach(w => middle = middle + Vector3D.Multiply((w.GetPosition() - middle), 0.5));
                Vector3D targetVec = Vector3D.Normalize(targetpos - middle);
                double distance = (targetpos - middle).Length();
                if (distance <= engageDist)
                {
                    Vector3D aimVec = targetLead.WorldMatrix.Forward;
                    Vector3D down = Azimuth.Rotor.WorldMatrix.Down;

                    //Sets Rotor Angles
                    double armAngle = MathHelper.Clamp((Vector3D.Dot(aimVec, targetVec)), -1, 1);
                    double hemiSphereAngle = Vector3D.Dot(Vector3D.Cross(aimVec, targetVec), down);
                    double armOffset = (-Math.Acos(armAngle)) * Math.Sign(hemiSphereAngle);

                    double biggestUpperOffset = 0;

                    if (armOffset == double.NaN || double.IsInfinity(armOffset))
                    {
                        armOffset = 0;
                    }
                    Azimuth.SetRotorSpeedFromOffset(-(float)armOffset, 10, (float)maxSpeed);
                    if (Elevations.Count != 0)
                    {
                        Elevations.ForEach(e =>
                        {
                            StaticVanilla.ForEach(w =>
                            {
                                if (e != null && w.CubeGrid.Equals(e.Rotor.TopGrid))
                                    targetLead = w;
                            });
                            StaticWeapons.ForEach(w =>
                            {
                                if (e != null && w.CubeGrid.Equals(e.Rotor.TopGrid))
                                    targetLead = w;
                            });
                            aimVec = targetLead.WorldMatrix.Forward;
                            double aimUpperAngle = Math.Acos(Vector3D.Dot(aimVec, -down));
                            double targetUpperAngle = Math.Acos(Vector3D.Dot(targetVec, -down));
                            double upperOffset = (aimUpperAngle - targetUpperAngle);
                            if (upperOffset == double.NaN || double.IsInfinity(upperOffset))
                            { upperOffset = 0; }
                            if (upperOffset > biggestUpperOffset)
                                biggestUpperOffset = Math.Abs(upperOffset);
                            int invFac = invertRot ? -1 : 1;
                            e.RotateElevation((float)upperOffset * invFac, (float)maxSpeed, MainElevation.Rotor.WorldMatrix.Up);
                        });
                    }
                    if (Math.Abs(armOffset) < MathHelper.ToRadians(maxDiverge) && Math.Abs(biggestUpperOffset) < MathHelper.ToRadians(maxDiverge))
                    {
                        if (offsetTimer > 0)
                            offsetTimer--;
                        if (offsetTimer == 0 && lastFiredWC && StaticVanilla.Count != 0)
                        {
                            IMyUserControllableGun v = StaticVanilla[sequenceTimerV];
                            v.ApplyAction("ShootOnce");
                            offsetTimer = offset;
                            sequenceTimerV++;
                            if (sequenceTimerV >= StaticVanilla.Count)
                                sequenceTimerV = 0;
                            lastFiredWC = false;
                            return;
                        }
                        else if (StaticVanilla.Count == 0)
                            lastFiredWC = false;
                        if (offsetTimer == 0 && !lastFiredWC && StaticWeapons.Count != 0)
                        {
                            IMyTerminalBlock w = StaticWeapons[sequenceTimerWC];
                            MyDetectedEntityInfo? info = api.GetAiFocus(Azimuth.Rotor.CubeGrid.EntityId);
                            if (info.HasValue && !(info.Value.IsEmpty()) && api.CanShootTarget(w, info.Value.EntityId, 0))
                            {
                                if (api.IsWeaponReadyToFire(w, 0, true, true) && Timer != null && Timer.IsWorking && !(Timer.IsCountingDown))
                                    Timer.Trigger();
                                api.FireWeaponOnce(w, false);
                            };
                            offsetTimer = offset;
                            sequenceTimerWC++;
                            if (sequenceTimerWC >= StaticWeapons.Count)
                                sequenceTimerWC = 0;
                            lastFiredWC = true;
                        }
                        else if (StaticWeapons.Count == 0)
                            lastFiredWC = true;
                    }
                }
            }

            public void AimAtTarget()
            {
                resting = false;
                Vector3D targetpos = Vector3D.Zero;
                bool shouldFire = false;
                if (!targetOverride && Designator != null)
                {
                    if (api.HasCoreWeapon(Designator))
                    {
                        MyDetectedEntityInfo? info = api.GetWeaponTarget(Designator);
                        if (info.HasValue && !(info.Value.IsEmpty()))
                        {
                            targetpos = info.Value.Position;
                            shouldFire = api.CanShootTarget(Designator, info.Value.EntityId, 0);
                        }
                    }
                    if (Designator is IMyLargeTurretBase && ((IMyLargeTurretBase)Designator).HasTarget)
                    {
                        targetpos = ((IMyLargeTurretBase)Designator).GetTargetedEntity().Position;
                        shouldFire = ((IMyLargeTurretBase)Designator).IsShooting;
                    }
                }
                else if (Designator == null || !Designator.IsWorking)
                    getDesignators();
                if (Designator == null)
                    return;
                if (shouldFire)
                {
                    var targetLead = StaticWeapons.Count != 0 ? StaticWeapons[0] : StaticVanilla.Count != 0 ? StaticVanilla[0] : null;
                    if (MainElevation == null || Elevations.Count == 0 || Azimuth == null || targetLead == null)
                        return;
                    Vector3D middle = targetLead.GetPosition();
                    if (StaticWeapons.Count != 0)
                        StaticWeapons.ForEach(w =>
                        {
                            middle = middle + Vector3D.Multiply((w.GetPosition() - middle), 0.5);
                            MyDetectedEntityInfo? info = api.GetWeaponTarget(Designator);
                            if (info.HasValue && !(info.Value.IsEmpty()))
                            {
                                var targetposraw = api.GetPredictedTargetPosition(w, info.Value.EntityId, 0);
                                if (targetposraw == null)
                                    return;
                                targetpos = (Vector3D)targetposraw;
                            }
                        });
                    if (StaticVanilla.Count != 0)
                        StaticVanilla.ForEach(w => middle = middle + Vector3D.Multiply((w.GetPosition() - middle), 0.5));
                    Vector3D targetVec = Vector3D.Normalize(targetpos - middle);
                    double distance = (targetpos - middle).Length();
                    if (distance <= engageDist)
                    {
                        Vector3D aimVec = targetLead.WorldMatrix.Forward;
                        Vector3D down = Azimuth.Rotor.WorldMatrix.Down;

                        //Sets Rotor Angles
                        double armAngle = MathHelper.Clamp((Vector3D.Dot(aimVec, targetVec)), -1, 1);
                        double hemiSphereAngle = Vector3D.Dot(Vector3D.Cross(aimVec, targetVec), down);
                        double armOffset = (-Math.Acos(armAngle)) * Math.Sign(hemiSphereAngle);

                        double biggestUpperOffset = 0;

                        if (armOffset == double.NaN || double.IsInfinity(armOffset))
                        {
                            armOffset = 0;
                        }
                        Azimuth.SetRotorSpeedFromOffset(-(float)armOffset, 10, (float)maxSpeed);
                        if (Elevations.Count != 0)
                            Elevations.ForEach(e =>
                            {
                                StaticVanilla.ForEach(w =>
                                {
                                    if (e != null && w.CubeGrid.Equals(e.Rotor.TopGrid))
                                        targetLead = w;
                                });
                                StaticWeapons.ForEach(w =>
                                {
                                    if (e != null && w.CubeGrid.Equals(e.Rotor.TopGrid))
                                        targetLead = w;
                                });
                                aimVec = targetLead.WorldMatrix.Forward;
                                double aimUpperAngle = Math.Acos(Vector3D.Dot(aimVec, -down));
                                double targetUpperAngle = Math.Acos(Vector3D.Dot(targetVec, -down));
                                double upperOffset = (aimUpperAngle - targetUpperAngle);
                                if (upperOffset == double.NaN || double.IsInfinity(upperOffset))
                                {
                                    upperOffset = 0;
                                }
                                if (upperOffset > biggestUpperOffset)
                                    biggestUpperOffset = Math.Abs(upperOffset);
                                int invFac = invertRot ? -1 : 1;
                                e.RotateElevation((float)upperOffset * invFac, (float)maxSpeed, MainElevation.Rotor.WorldMatrix.Up);
                            });
                        if (Math.Abs(armOffset) < MathHelper.ToRadians(maxDiverge) && Math.Abs(biggestUpperOffset) < MathHelper.ToRadians(maxDiverge))
                        {
                            if (offsetTimer > 0)
                                offsetTimer--;
                            if (offsetTimer == 0 && lastFiredWC && StaticVanilla.Count != 0)
                            {
                                IMyUserControllableGun v = StaticVanilla[sequenceTimerV];
                                v.ApplyAction("ShootOnce");
                                offsetTimer = offset;
                                sequenceTimerV++;
                                if (sequenceTimerV >= StaticVanilla.Count)
                                    sequenceTimerV = 0;
                                lastFiredWC = false;
                                return;
                            }
                            else if (StaticVanilla.Count == 0)
                                lastFiredWC = false;
                            if (offsetTimer == 0 && !lastFiredWC && StaticWeapons.Count != 0)
                            {
                                IMyTerminalBlock w = StaticWeapons[sequenceTimerWC];
                                if (api.IsWeaponReadyToFire(w, 0, true, true) && Timer != null && Timer.IsWorking && !(Timer.IsCountingDown))
                                    Timer.Trigger();
                                api.FireWeaponOnce(w);
                                offsetTimer = offset;
                                sequenceTimerWC++;
                                if (sequenceTimerWC >= StaticWeapons.Count)
                                    sequenceTimerWC = 0;
                                lastFiredWC = true;
                            }
                            else if (StaticWeapons.Count == 0)
                                lastFiredWC = true;
                        }
                    }
                }
                else
                    MoveToRest();
            }

            public void MoveToRest()
            {
                StaticWeapons.ForEach(w => api.ToggleWeaponFire(w, false, false));
                sequenceTimerWC = 0;
                sequenceTimerV = 0;
                offsetTimer = 0;
                resting = true;
                Azimuth.MoveToRest((float)maxSpeed);
                Elevations.ForEach(e => e.MoveToRest((float)maxSpeed));
            }

            public void debug()
            {
                EchoString.Append("debug for turret group: " + Name + "\n");
                EchoString.Append("Resting: " + resting + "\n");
                EchoString.Append("Elevation Reversed: " + invertRot + "\n");
                EchoString.Append("Engagement Distance: " + engageDist + "\n");
                if (Azimuth == null)
                {
                    EchoString.Append("No AZIMUTH rotor found!\n");
                }
                if (MainElevation == null)
                {
                    EchoString.Append("No ELEVATION rotor found!\n");
                }
                if (StaticWeapons.Count == 0 && StaticVanilla.Count == 0)
                {
                    EchoString.Append("No WEAPONS found!\n");
                }
                else
                    EchoString.Append(StaticWeapons.Count + " WC weapons\n" + StaticVanilla.Count + " Vanilla Weapons\n");
                EchoString.Append("\n");
            }

            public int CompareByDistance(IMyCubeBlock lhs, IMyCubeBlock rhs) =>
                    (lhs.Position - Azimuth.Rotor.Position).Length() - (rhs.Position - Azimuth.Rotor.Position).Length();
        }
    }
}
