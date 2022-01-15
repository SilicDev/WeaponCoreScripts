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

namespace WCTurretScript
{
    public class Program : MyGridProgram
    {
        /*-----------------DO NOT EDIT BELOW THIS LINE--------------------------------*/
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        
        public const string VERSION = "1.16.3";

        public const string GeneralIniTag = "General Config";
        public const string GroupNameKey = "Turret Group name tag";
        public const string DesignatorNameKey = "Designator Turret name tag (Vanilla only)";
        public const string AzimuthNameKey = "Azimuth rotor name tag";
        public const string ElevationNameKey = "Elevation rotor name tag";
        public const string TimerNameKey = "Fire Timer name tag";
        public const string WcTargetKey = "Allow Main Target Tracking (Beta)";
        public const string waitCyclesKey = "Minor Cycles";

        public const string maxSpeedKey = "Maximum turning speed";
        public const string engageDistKey = "Engagement distance (m)";
        public const string maxDivergeKey = "Maximum divergence from target";
        public const string offsetKey = "Offset ticks between shots";
        public const string invertRotKey = "Invert Elevation Rotor Rotation";
            
        public static string ElevationNameTag = "Elevation";
        public static string AzimuthNameTag = "Azimuth";
        public static string DesignatorNameTag = "Designator";
        public static string TimerNameTag = "Fire";
        public static string GroupNameTag = "Turret Group";
        public static bool allowWcTarget = false;
        public static int waitCycles = 500;

        public static WcPbApi api = new WcPbApi();
        public static bool apiActivated = false;

        public static List<MyDefinitionId> WeaponDefinitions = new List<MyDefinitionId>();
        public static List<MyDefinitionId> TurretDefinitions = new List<MyDefinitionId>();
        public static List<string> definitionSubIds = new List<string>();
        public static List<string> turretDefinitionSubIds = new List<string>();
        
        public static List<Turret> turrets = new List<Turret>();
        public List<IMyBlockGroup> turretGroups = new List<IMyBlockGroup>();
        
        public static List<IMyTerminalBlock> WC_DIRECTORS = new List<IMyTerminalBlock>();
        public IMyTerminalBlock WC_DIRECTOR = null;
        public static List<IMyLargeTurretBase> DIRECTORS = new List<IMyLargeTurretBase>();
        public IMyLargeTurretBase DIRECTOR;

        public static StringBuilder EchoString = new StringBuilder();
        public static MyIni generalIni = new MyIni();
        public static List<string> iniSections = new List<string>();
        public Vector3D targetpos = Vector3D.Zero;
        public bool ISTARGETING = false;
        public IMyEntity strongestTarget;
        public float highestThreat;
        public static bool targetOverride = false;

        public int runCount = waitCycles;

        public void Main(string argument, UpdateType updateSource)
        {
            runCount++;
            EchoString.Clear();
            
            EchoString.Append("Managing " + turrets.Count + " Turret(s)\n");
            EchoString.Append("WC Designators: " + WC_DIRECTORS.Count + "\n");
            EchoString.Append("Vanilla Designators: " + DIRECTORS.Count + "\n");
            
            if(!apiActivated) {
                try {
                    api.Activate(Me);
                    apiActivated = true;
                }
                catch {
                    Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!");
                    return;
                }
            }
            
            if(api.HasGridAi(Me.CubeGrid.EntityId)) {
                if(runCount >= waitCycles) {
                    WeaponDefinitions.Clear();
                    TurretDefinitions.Clear();
            
                    api.GetAllCoreStaticLaunchers(WeaponDefinitions);
                    api.GetAllCoreTurrets(TurretDefinitions);
            
                    definitionSubIds.Clear();
                    turretDefinitionSubIds.Clear();
            
                    WeaponDefinitions.ForEach(d => definitionSubIds.Add(d.SubtypeName));
                    TurretDefinitions.ForEach(t => turretDefinitionSubIds.Add(t.SubtypeName));
            
                    WC_DIRECTORS.Clear();
                    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(WC_DIRECTORS,
                            b => !(definitionSubIds.Contains(b.BlockDefinition.SubtypeName)) 
                                && turretDefinitionSubIds.Contains(b.BlockDefinition.SubtypeName));
            
                    turretGroups.Clear();
                    GridTerminalSystem.GetBlockGroups(turretGroups, g => g.Name.Contains(GroupNameTag));
                    turretGroups.ForEach(g => {
                        Turret newTurret = Turret.AttemptCreateFromGroup(g);
                        if(newTurret != null)
                            turrets.Add(newTurret);
                    });
            
                    DIRECTORS.Clear();
                    GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(DIRECTORS, b => b.CubeGrid == Me.CubeGrid);
                    WC_DIRECTORS.ForEach(d => {
                        if(d is IMyLargeTurretBase) 
                            DIRECTORS.Remove((IMyLargeTurretBase)d);
                    });
            
                    ParseIni();
                    runCount = 0;
               }
                EchoString.Append("Attempting WC Targeting\n");
                
                if(allowWcTarget) {
                    MyDetectedEntityInfo? info = api.GetAiFocus(Me.CubeGrid.EntityId);
                    if(info.HasValue && !(info.Value.IsEmpty())) {
                        targetpos = info.Value.Position;
                        targetOverride = true;
                        ISTARGETING = true;
                    }
                } else {
                    targetOverride = false;
                    if(WC_DIRECTORS.Count != 0) {
                        WC_DIRECTOR = null;
                        foreach (var item in WC_DIRECTORS) {
                            if (item != null) {
                                MyDetectedEntityInfo? info = api.GetWeaponTarget(item);
                                if (info != null && info.HasValue && !(info.Value.IsEmpty()) && api.IsTargetAligned(item, info.Value.EntityId, 0))
                                {
                                    WC_DIRECTOR = item;
                                    if(item is IMyLargeTurretBase)
                                        DIRECTORS.Remove((IMyLargeTurretBase)item);
                                    break;
                                }
                            }
                        }
                        if(WC_DIRECTOR!=null) {
                            MyDetectedEntityInfo? info = api.GetWeaponTarget(WC_DIRECTOR);
                            if(info.HasValue && !(info.Value.IsEmpty()))
                                turrets.ForEach(t => t.AimAtTarget());
                            else
                                AttemptVanillaTargeting();
                        } else
                            AttemptVanillaTargeting();
                    } else
                        AttemptVanillaTargeting();
                }
            } else {
                if(runCount >= waitCycles) {
                    turretGroups.Clear();
                    GridTerminalSystem.GetBlockGroups(turretGroups,g => g.Name.Contains(GroupNameTag));
                    turretGroups.ForEach(g => {
                        Turret newTurret = Turret.AttemptCreateFromGroup(g);
                        if(newTurret != null)
                            turrets.Add(newTurret);
                    });
                }
                AttemptVanillaTargeting();
            }
            if(ISTARGETING) {
                turrets.ForEach(t => t.AimAtTarget(targetpos));
            } else if(allowWcTarget) {
                turrets.ForEach(t => t.MoveToRest());
            }
            EchoString.Append("------------------------------------\ndebug area:\n");
            turrets.ForEach(t=>t.debug());
            Echo(EchoString.ToString());
        }

        /*
         * handles Vanilla targeting
         * relevant docs: https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyLargeTurretBase
         */
        public void AttemptVanillaTargeting() {
            if(runCount >= waitCycles){
                runCount = 0;
            }
            ISTARGETING = false;
            if(DIRECTORS.Count != 0){
                EchoString.Append("Attempting Vanilla Targeting\n");
                foreach (var item in DIRECTORS) {
                    if (item.IsShooting && item.HasTarget) { 
                        DIRECTOR = item;
                        break; 
                    }
                }
                if (DIRECTOR != null && DIRECTOR.HasTarget) { 
                    turrets.ForEach(t => t.AimAtTarget());
                }
            } else {
                turrets.ForEach(t => t.MoveToRest());
            }
        }

        /// <summary>Parses config data from CustomData.</summary>
        /// relevant docs: https://github.com/malware-dev/MDK-SE/wiki/VRage.Game.ModAPI.Ingame.Utilities.MyIni
        ///      https://github.com/malware-dev/MDK-SE/wiki/Handling-configuration-and-storage
        /// called functions: Turret.ParseTurretIni(), WriteIni()
        public void ParseIni(){
            generalIni.Clear();
            generalIni.TryParse(Me.CustomData);

            iniSections.Clear();
            generalIni.GetSections(iniSections);
            if(iniSections.Count == 0)
                generalIni.EndContent = Me.CustomData;
            GroupNameTag = generalIni.Get(GeneralIniTag, GroupNameKey).ToString(GroupNameTag);
            DesignatorNameTag = generalIni.Get(GeneralIniTag, DesignatorNameKey).ToString(DesignatorNameTag);
            AzimuthNameTag = generalIni.Get(GeneralIniTag, AzimuthNameKey).ToString(AzimuthNameTag);
            ElevationNameTag = generalIni.Get(GeneralIniTag, ElevationNameKey).ToString(ElevationNameTag);
            TimerNameTag = generalIni.Get(GeneralIniTag, TimerNameKey).ToString(TimerNameTag);
            allowWcTarget = generalIni.Get(GeneralIniTag, WcTargetKey).ToBoolean(allowWcTarget);
            waitCycles = generalIni.Get(GeneralIniTag, waitCyclesKey).ToInt32(waitCycles);
            turrets.ForEach(t => t.ParseTurretIni());
            WriteIni();
        }

        /// <summary>Writes config data to CustomData.</summary>
        /// relevant docs: https://github.com/malware-dev/MDK-SE/wiki/VRage.Game.ModAPI.Ingame.Utilities.MyIni
        ///      https://github.com/malware-dev/MDK-SE/wiki/Handling-configuration-and-storage
        ///  called functions: Turret.WriteTurretIni()
        public void WriteIni(){
            generalIni.Set(GeneralIniTag, GroupNameKey, GroupNameTag);
            generalIni.Set(GeneralIniTag, DesignatorNameKey, DesignatorNameTag);
            generalIni.Set(GeneralIniTag, AzimuthNameKey, AzimuthNameTag);
            generalIni.Set(GeneralIniTag, ElevationNameKey, ElevationNameTag);
            generalIni.Set(GeneralIniTag, TimerNameKey, TimerNameTag);
            generalIni.Set(GeneralIniTag, WcTargetKey, allowWcTarget);
            generalIni.Set(GeneralIniTag, waitCyclesKey, waitCycles);
            turrets.ForEach(t => t.WriteTurretIni());
            string output = generalIni.ToString();
            if (!string.Equals(output, Me.CustomData))
                Me.CustomData = output;
        }

        ///<summary>The player built turret.</summary>
        public class Turret{
            public static List<string> Names = new List<string>();
            public static List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
            public string Name;
            public List<IMyMotorStator> Elevations = new List<IMyMotorStator>();
            public List<IMyTerminalBlock> StaticWeapons = new List<IMyTerminalBlock>();
            public List<IMyUserControllableGun> StaticVanilla = new List<IMyUserControllableGun>();
            public IMyMotorStator Azimuth;
            public IMyMotorStator MainElevation;
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
            
            public static Turret AttemptCreateFromGroup(IMyBlockGroup blocks){
                if(blocks != null){
                    string name = blocks.Name;
                    if(Names.Contains(name)){
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

            public static void AttemptUpdateFromGroup(IMyBlockGroup blocks, string name){
                Turret oldTurret = turrets.Find(t => t.Name.ToLower().Equals(name.ToLower()));
                oldTurret.UpdateTurret(blocks);
            }

            public void UpdateTurret(IMyBlockGroup blocks){
                temp.Clear();
                bGroup = blocks;
                blocks.GetBlocksOfType<IMyMotorStator>(temp, b => !b.CustomName.Contains(ElevationNameTag) 
                    && b.CustomName.Contains(AzimuthNameTag));
                if(temp.Count == 0)
                    return;
                this.Azimuth = (IMyMotorStator)temp[0];
                blocks.GetBlocksOfType<IMyMotorStator>(this.Elevations, b => b.CustomName.Contains(ElevationNameTag) 
                    && !b.CustomName.Contains(AzimuthNameTag) && b.CubeGrid == Azimuth.TopGrid);
                if(Elevations.Count == 0)
                    return; 
                MainElevation = Elevations[0];
                if (MainElevation.CustomName.Contains("#LEFT#"))
                    invertRot = true;
                foreach (IMyMotorStator e in Elevations){
                    getElevationBlocks(blocks, e);
                }
                blocks.GetBlocksOfType<IMyTerminalBlock>(temp, b => b.CustomName.Contains(DesignatorNameTag));
                temp.Sort((lhs, rhs) => ((lhs.Position - Azimuth.Position).Length() - (rhs.Position - Azimuth.Position).Length()));
                IMyTerminalBlock tempDes = null;
                if(temp.Count != 0){
                    tempDes = temp[0];
                    if(tempDes != null && (tempDes is IMyLargeTurretBase || api.HasCoreWeapon(tempDes)))
                        if(tempDes.IsWorking){
                            Designator = tempDes;
                        }
                        else {
                            getDesignators();
                        }
                } else {
                    getDesignators();
                }
                blocks.GetBlocksOfType<IMyTimerBlock>(temp, b => b.CustomName.Contains(TimerNameTag));
                if(temp.Count != 0)
                    this.Timer = (IMyTimerBlock)temp[0];
                this.hasUpdated = true;
            }

            public void getElevationBlocks(IMyBlockGroup blocks,IMyMotorStator e){
                blocks.GetBlocksOfType<IMyTerminalBlock>(StaticWeapons, b => definitionSubIds.Contains(b.BlockDefinition.SubtypeName));
                blocks.GetBlocksOfType<IMyUserControllableGun>(StaticVanilla,b => true);
                StaticVanilla.RemoveAll(x => (StaticWeapons.Contains(x) || definitionSubIds.Contains(x.BlockDefinition.SubtypeName)));
            }

            public void getDesignators(){
                designatorWcCandidates.Clear();
                designatorWcCandidates = WC_DIRECTORS.Where(d => {
                    MyDetectedEntityInfo? target = api.GetWeaponTarget(d, 0);
                    if (target != null && target.HasValue && !(target.Value.IsEmpty())) {
                        return ((IMyFunctionalBlock)d).IsWorking && api.IsTargetAligned(d, target.Value.EntityId, 0);
                    } else {
                        return false;
                    }
                }).ToList();
                designatorWcCandidates.Sort((lhs, rhs) => ((lhs.Position - Azimuth.Position).Length() - (rhs.Position - Azimuth.Position).Length()));

                Designator = designatorWcCandidates.Any() ? designatorWcCandidates[0] : null;
                if(Designator == null) {
                    designatorCandidates.Clear();
                    designatorCandidates = DIRECTORS.Where(d => {
                        //Does this also need to be changed?
                        long? id = d.GetTargetedEntity().EntityId;
                        if (id != null) {
                            return d.IsWorking;
                        } else {
                            return false;
                        }
                    }).ToList();
                    designatorCandidates.Sort((lhs, rhs) => ((lhs.Position - Azimuth.Position).Length() - (rhs.Position - Azimuth.Position).Length()));
                    Designator = designatorCandidates.Any() ? designatorCandidates[0] : null;
                }
            }

            public void ParseTurretIni(){
                maxSpeed = generalIni.Get(Name, maxSpeedKey).ToDouble(maxSpeed);
                engageDist = generalIni.Get(Name, engageDistKey).ToInt32(engageDist);
                maxDiverge = generalIni.Get(Name, maxDivergeKey).ToSingle(maxDiverge);
                offset = generalIni.Get(Name, offsetKey).ToInt32(offset);
                invertRot = generalIni.Get(Name, invertRotKey).ToBoolean(invertRot);
            }

            public void WriteTurretIni(){
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
            public void AimAtTarget(Vector3D targetpos){
                resting = false;
                var targetLead = StaticWeapons.Count != 0 ? StaticWeapons[0] : StaticVanilla.Count!=0 ? StaticVanilla[0]: null;
                if(MainElevation == null || Elevations.Count == 0 || Azimuth == null || targetLead == null)
                    return;
                Vector3D middle = targetLead.GetPosition();
                if(StaticWeapons.Count != 0)
                {
                    StaticWeapons.ForEach(w => {
                        middle = middle + Vector3D.Multiply((w.GetPosition() - middle), 0.5);
                        MyDetectedEntityInfo? info = api.GetAiFocus(Azimuth.CubeGrid.EntityId);
                        if (info.HasValue && !(info.Value.IsEmpty())){
                            var targetposraw = api.GetPredictedTargetPosition(w, info.Value.EntityId, 0);
                            if(targetposraw == null)
                                return;
                            targetpos= (Vector3D)targetposraw;
                        }
                    });
                }
                if(StaticVanilla.Count!=0)
                    StaticVanilla.ForEach(w => middle = middle + Vector3D.Multiply((w.GetPosition() - middle), 0.5));
                Vector3D targetVec = Vector3D.Normalize(targetpos-middle);
                double distance = (targetpos - middle).Length();
                if (distance <= engageDist) 
                {
                    Vector3D aimVec = targetLead.WorldMatrix.Forward;
                    Vector3D down = Azimuth.WorldMatrix.Down;
                    
                    //Sets Rotor Angles
                    double armAngle = MathHelper.Clamp((Vector3D.Dot(aimVec, targetVec)), -1, 1);
                    double hemiSphereAngle = Vector3D.Dot(Vector3D.Cross(aimVec, targetVec), down);
                    double armOffset = (-Math.Acos(armAngle)) * Math.Sign(hemiSphereAngle);

                    double biggestUpperOffset = 0;

                    if (armOffset == double.NaN || double.IsInfinity(armOffset)) {
                        armOffset = 0;
                    }

                    Azimuth.RotorLock = false;
                    Azimuth.TargetVelocityRad = MathHelper.Clamp(-(float)armOffset * 10, -1 * (float)maxSpeed, (float)maxSpeed);
                    if(Elevations.Count != 0) {
                        Elevations.ForEach(e => {
                            StaticVanilla.ForEach(w => {
                                if(e != null && w.CubeGrid.Equals(e.TopGrid))
                                    targetLead = w;
                            });
                            StaticWeapons.ForEach(w => {
                                if(e != null && w.CubeGrid.Equals(e.TopGrid))
                                    targetLead = w;
                            });
                            aimVec = targetLead.WorldMatrix.Forward;
                            double aimUpperAngle = Math.Acos(Vector3D.Dot(aimVec, -down));
                            double targetUpperAngle = Math.Acos(Vector3D.Dot(targetVec, -down));
                            double upperOffset = (aimUpperAngle - targetUpperAngle);
                            if (upperOffset == double.NaN ||double.IsInfinity(upperOffset))
                                {upperOffset = 0; }
                            if(upperOffset > biggestUpperOffset)
                                biggestUpperOffset = Math.Abs(upperOffset);
                            e.RotorLock = false;
                            if(invertRot){
                                if(e != null&&e.WorldMatrix.Up != MainElevation.WorldMatrix.Up || e.BlockDefinition.SubtypeName.Contains("Hinge")){
                                    e.TargetVelocityRad = MathHelper.Clamp(-(float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                }else{
                                    e.TargetVelocityRad = MathHelper.Clamp((float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                }
                            } else {
                                if(e != null && e.WorldMatrix.Up != MainElevation.WorldMatrix.Up || e.BlockDefinition.SubtypeName.Contains("Hinge")){
                                    e.TargetVelocityRad = MathHelper.Clamp((float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                }else{
                                    e.TargetVelocityRad = MathHelper.Clamp(-(float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                }
                            }
                        });
                    }
                    if(Math.Abs(armOffset) < MathHelper.ToRadians(maxDiverge) && Math.Abs(biggestUpperOffset) < MathHelper.ToRadians(maxDiverge)){
                        if(offsetTimer > 0)
                                offsetTimer--;
                        if(offsetTimer == 0 && lastFiredWC && StaticVanilla.Count != 0){
                            IMyUserControllableGun v = StaticVanilla[sequenceTimerV];
                            v.ApplyAction("ShootOnce");
                            offsetTimer = offset;
                            sequenceTimerV++;
                            if(sequenceTimerV >= StaticVanilla.Count)
                                sequenceTimerV = 0;
                            lastFiredWC = false;
                            return;
                        }else if (StaticVanilla.Count == 0)
                            lastFiredWC = false;
                        if(offsetTimer == 0 && !lastFiredWC&&StaticWeapons.Count!=0){
                            IMyTerminalBlock w = StaticWeapons[sequenceTimerWC];
                            MyDetectedEntityInfo? info = api.GetAiFocus(Azimuth.CubeGrid.EntityId);
                            if(info.HasValue && !(info.Value.IsEmpty()) && api.CanShootTarget(w, info.Value.EntityId, 0)){
                                if(api.IsWeaponReadyToFire(w, 0, true, true) && Timer != null && Timer.IsWorking && !(Timer.IsCountingDown))
                                    Timer.Trigger();
                                api.FireWeaponOnce(w, false);
                            };
                            offsetTimer = offset;
                            sequenceTimerWC++;
                            if(sequenceTimerWC >= StaticWeapons.Count)
                                sequenceTimerWC = 0;
                            lastFiredWC = true;
                        }else if (StaticWeapons.Count == 0)
                            lastFiredWC = true;
                    }
                }
            }

            public void AimAtTarget(){
                resting = false;
                Vector3D targetpos = Vector3D.Zero;
                bool shouldFire = false;
                if (!targetOverride && Designator != null){
                    if(api.HasCoreWeapon(Designator)){
                        MyDetectedEntityInfo? info = api.GetWeaponTarget(Designator);
                        if(info.HasValue && !(info.Value.IsEmpty())){
                            targetpos = info.Value.Position;
                            shouldFire = api.CanShootTarget(Designator, info.Value.EntityId,0);
                        }
                    }
                    if(Designator is IMyLargeTurretBase && ((IMyLargeTurretBase)Designator).HasTarget)
                    {
                        targetpos = ((IMyLargeTurretBase)Designator).GetTargetedEntity().Position;
                        shouldFire = ((IMyLargeTurretBase)Designator).IsShooting;
                    }
                }else if(Designator == null || !Designator.IsWorking)
                    getDesignators();
                if(Designator == null)
                    return;
                if(shouldFire){
                    var targetLead = StaticWeapons.Count != 0 ? StaticWeapons[0] : StaticVanilla.Count!=0 ? StaticVanilla[0] : null;
                    if(MainElevation == null || Elevations.Count == 0 || Azimuth == null || targetLead == null)
                        return;
                    Vector3D middle = targetLead.GetPosition();
                    if(StaticWeapons.Count != 0)
                        StaticWeapons.ForEach(w => {
                            middle = middle + Vector3D.Multiply((w.GetPosition() - middle),0.5);
                            MyDetectedEntityInfo? info = api.GetWeaponTarget(Designator);
                            if (info.HasValue && !(info.Value.IsEmpty())){
                                var targetposraw = api.GetPredictedTargetPosition(w,info.Value.EntityId,0);
                                if(targetposraw == null)
                                    return;
                                targetpos = (Vector3D)targetposraw;
                            }
                    });
                    if(StaticVanilla.Count != 0)
                        StaticVanilla.ForEach(w => middle = middle + Vector3D.Multiply((w.GetPosition() - middle),0.5));
                    Vector3D targetVec = Vector3D.Normalize(targetpos - middle);
                    double distance = (targetpos - middle).Length();
                    if (distance <= engageDist) 
                    {   
                        Vector3D aimVec = targetLead.WorldMatrix.Forward;
                        Vector3D down = Azimuth.WorldMatrix.Down;
                    
                        //Sets Rotor Angles
                        double armAngle = MathHelper.Clamp((Vector3D.Dot(aimVec, targetVec)), -1, 1);
                        double hemiSphereAngle = Vector3D.Dot(Vector3D.Cross(aimVec, targetVec), down);
                        double armOffset = (-Math.Acos(armAngle)) * Math.Sign(hemiSphereAngle);

                        double biggestUpperOffset = 0;

                        if (armOffset == double.NaN || double.IsInfinity(armOffset)) {
                            armOffset = 0;
                        }

                        Azimuth.RotorLock = false;
                        Azimuth.TargetVelocityRad = MathHelper.Clamp(-(float)armOffset * 10, -1 * (float)maxSpeed, (float)maxSpeed);
                        if(Elevations.Count != 0)
                            Elevations.ForEach(e => {
                                StaticVanilla.ForEach(w => {
                                    if(e!=null&&w.CubeGrid.Equals(e.TopGrid))
                                        targetLead = w;
                                });
                                StaticWeapons.ForEach(w => {
                                    if(e!=null&&w.CubeGrid.Equals(e.TopGrid))
                                        targetLead = w;
                                });
                                aimVec = targetLead.WorldMatrix.Forward;
                                double aimUpperAngle = Math.Acos(Vector3D.Dot(aimVec, -down));
                                double targetUpperAngle = Math.Acos(Vector3D.Dot(targetVec, -down));
                                double upperOffset = (aimUpperAngle - targetUpperAngle);
                                if (upperOffset == double.NaN || double.IsInfinity(upperOffset)) {
                                    upperOffset = 0; 
                                }
                                if(upperOffset > biggestUpperOffset)
                                    biggestUpperOffset = Math.Abs(upperOffset);
                                e.RotorLock = false;
                                if(invertRot){
                                    if(e != null && e.WorldMatrix.Up != MainElevation.WorldMatrix.Up || e.BlockDefinition.SubtypeName.Contains("Hinge")) {
                                        e.TargetVelocityRad = MathHelper.Clamp(-(float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                    }else{
                                        e.TargetVelocityRad = MathHelper.Clamp((float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                    }
                                }else{
                                    if(e != null && e.WorldMatrix.Up != MainElevation.WorldMatrix.Up || e.BlockDefinition.SubtypeName.Contains("Hinge")) {
                                        e.TargetVelocityRad = MathHelper.Clamp((float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                    }else{
                                        e.TargetVelocityRad = MathHelper.Clamp(-(float)upperOffset * 4, -1 * (float)maxSpeed, (float)maxSpeed);
                                    }
                                }
                            });
                        if(Math.Abs(armOffset) < MathHelper.ToRadians(maxDiverge) && Math.Abs(biggestUpperOffset) < MathHelper.ToRadians(maxDiverge)){
                            if(offsetTimer > 0)
                                offsetTimer--;
                            if(offsetTimer == 0 && lastFiredWC&&StaticVanilla.Count != 0){
                                IMyUserControllableGun v = StaticVanilla[sequenceTimerV];
                                v.ApplyAction("ShootOnce");
                                offsetTimer = offset;
                                sequenceTimerV++;
                                if(sequenceTimerV >=StaticVanilla.Count)
                                    sequenceTimerV = 0;
                                lastFiredWC = false;
                                return;
                            } else if (StaticVanilla.Count == 0)
                                lastFiredWC = false;
                            if(offsetTimer == 0 && !lastFiredWC&&StaticWeapons.Count != 0){
                                IMyTerminalBlock w = StaticWeapons[sequenceTimerWC];
                                if(api.IsWeaponReadyToFire(w, 0, true, true) && Timer != null && Timer.IsWorking && !(Timer.IsCountingDown))
                                    Timer.Trigger();
                                api.FireWeaponOnce(w);
                                offsetTimer = offset;
                                sequenceTimerWC++;
                                if(sequenceTimerWC >= StaticWeapons.Count)
                                    sequenceTimerWC = 0;
                                lastFiredWC = true;
                            }else if (StaticWeapons.Count == 0)
                                lastFiredWC = true;
                        }
                    }
                }else
                    MoveToRest();
            }

            public void MoveToRest() {
                StaticWeapons.ForEach(w => api.ToggleWeaponFire(w,false,false));
                sequenceTimerWC = 0;
                sequenceTimerV = 0;
                offsetTimer = 0;
                resting = true;
                MoveRotorToRest(Azimuth);
                Elevations.ForEach(e => MoveRotorToRest(e));
            }

            public void MoveRotorToRest(IMyMotorStator rotor){
                if(rotor != null){
                    var neg = rotor.CustomData.Contains("-");
                    var pos = 0;
                    try {
                        pos = Convert.ToInt32(rotor.CustomData);
                    } 
                    catch {
                        //Empty
                    }
                    if(Math.Abs(MathHelper.ToRadians(pos)-rotor.Angle)<0.01){
                        rotor.TargetVelocityRad = 0;
                    } else {
                        rotor.RotorLock=false;
                        setRestSpeed(rotor,pos);
                    }
                }
            }

            public void setRestSpeed(IMyMotorStator rotor, float targetAngleDeg)
            {
                float currentAngleDeg = MathHelper.ToDegrees(rotor.Angle);
                float angleDiff = 180 - Math.Abs(Math.Abs(targetAngleDeg - currentAngleDeg) - 180);
                if (currentAngleDeg < 180) {
                    angleDiff *= -1;
                }
                float targetSpeed = angleDiff / 360 * (float) maxSpeed;
                if (rotor.BlockDefinition.SubtypeName.Contains("Hinge")) {
                    targetSpeed = (targetAngleDeg - currentAngleDeg) / 180 * (float) maxSpeed;
                }
                rotor.TargetVelocityRad = MathHelper.Clamp(targetSpeed, -1 * (float)maxSpeed, (float)maxSpeed);
            }

            public void debug(){
                EchoString.Append("debug for turret group: "+Name+"\n");
                EchoString.Append("Resting: " + resting +"\n");
                EchoString.Append("Elevation Reversed: " + invertRot +"\n");
                EchoString.Append("Engagement Distance: " + engageDist + "\n");
                if(Azimuth == null){
                    EchoString.Append("No AZIMUTH rotor found!\n");
                }if(MainElevation == null){
                    EchoString.Append("No ELEVATION rotor found!\n");
                }if(StaticWeapons.Count==0&&StaticVanilla.Count==0){
                    EchoString.Append("No WEAPONS found!\n");
                }else
                    EchoString.Append(StaticWeapons.Count+" WC weapons\n"+StaticVanilla.Count+" Vanilla Weapons\n");
                EchoString.Append("\n");
            }
        }
    }
}
