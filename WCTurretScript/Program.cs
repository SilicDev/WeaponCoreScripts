using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        #region mdkpreserve

        /*-----------------DO NOT EDIT BELOW THIS LINE--------------------------------*/

        #endregion mdkpreserve

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

        public static WcPbAPI api = new WcPbAPI();
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

            if (!apiActivated)
            {
                try
                {
                    api.Activate(Me);
                    apiActivated = true;
                }
                catch
                {
                    Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!");
                    return;
                }
            }

            if (api.HasGridAi(Me.CubeGrid.EntityId))
            {
                if (runCount >= waitCycles)
                {
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
                    turretGroups.ForEach(g =>
                    {
                        Turret newTurret = Turret.AttemptCreateFromGroup(g);
                        if (newTurret != null)
                            turrets.Add(newTurret);
                    });

                    DIRECTORS.Clear();
                    GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(DIRECTORS, b => b.CubeGrid == Me.CubeGrid);
                    WC_DIRECTORS.ForEach(d =>
                    {
                        if (d is IMyLargeTurretBase)
                            DIRECTORS.Remove((IMyLargeTurretBase)d);
                    });

                    ParseIni();
                    runCount = 0;
                }
                EchoString.Append("Attempting WC Targeting\n");

                if (allowWcTarget)
                {
                    MyDetectedEntityInfo? info = api.GetAiFocus(Me.CubeGrid.EntityId);
                    if (info.HasValue && !(info.Value.IsEmpty()))
                    {
                        targetpos = info.Value.Position;
                        targetOverride = true;
                        ISTARGETING = true;
                    }
                }
                else
                {
                    targetOverride = false;
                    if (WC_DIRECTORS.Count != 0)
                    {
                        WC_DIRECTOR = null;
                        foreach (var item in WC_DIRECTORS)
                        {
                            if (item != null)
                            {
                                MyDetectedEntityInfo? info = api.GetWeaponTarget(item);
                                if (info != null && info.HasValue && !(info.Value.IsEmpty()) && api.IsTargetAligned(item, info.Value.EntityId, 0))
                                {
                                    WC_DIRECTOR = item;
                                    if (item is IMyLargeTurretBase)
                                        DIRECTORS.Remove((IMyLargeTurretBase)item);
                                    break;
                                }
                            }
                        }
                        if (WC_DIRECTOR != null)
                        {
                            MyDetectedEntityInfo? info = api.GetWeaponTarget(WC_DIRECTOR);
                            if (info.HasValue && !(info.Value.IsEmpty()))
                                turrets.ForEach(t => t.AimAtTarget());
                            else
                                AttemptVanillaTargeting();
                        }
                        else
                            AttemptVanillaTargeting();
                    }
                    else
                        AttemptVanillaTargeting();
                }
            }
            else
            {
                if (runCount >= waitCycles)
                {
                    turretGroups.Clear();
                    GridTerminalSystem.GetBlockGroups(turretGroups, g => g.Name.Contains(GroupNameTag));
                    turretGroups.ForEach(g =>
                    {
                        Turret newTurret = Turret.AttemptCreateFromGroup(g);
                        if (newTurret != null)
                            turrets.Add(newTurret);
                    });
                }
                AttemptVanillaTargeting();
            }
            if (ISTARGETING)
            {
                turrets.ForEach(t => t.AimAtTarget(targetpos));
            }
            else if (allowWcTarget)
            {
                turrets.ForEach(t => t.MoveToRest());
            }
            EchoString.Append("------------------------------------\ndebug area:\n");
            turrets.ForEach(t => t.debug());
            Echo(EchoString.ToString());
        }

        /*
         * handles Vanilla targeting
         * relevant docs: https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyLargeTurretBase
         */

        public void AttemptVanillaTargeting()
        {
            if (runCount >= waitCycles)
            {
                runCount = 0;
            }
            ISTARGETING = false;
            if (DIRECTORS.Count != 0)
            {
                EchoString.Append("Attempting Vanilla Targeting\n");
                foreach (var item in DIRECTORS)
                {
                    if (item.IsShooting && item.HasTarget)
                    {
                        DIRECTOR = item;
                        break;
                    }
                }
                if (DIRECTOR != null && DIRECTOR.HasTarget)
                {
                    turrets.ForEach(t => t.AimAtTarget());
                }
            }
            else
            {
                turrets.ForEach(t => t.MoveToRest());
            }
        }

        /// <summary>Parses config data from CustomData.</summary>
        /// relevant docs: https://github.com/malware-dev/MDK-SE/wiki/VRage.Game.ModAPI.Ingame.Utilities.MyIni
        ///      https://github.com/malware-dev/MDK-SE/wiki/Handling-configuration-and-storage
        /// called functions: Turret.ParseTurretIni(), WriteIni()
        public void ParseIni()
        {
            generalIni.Clear();
            generalIni.TryParse(Me.CustomData);

            iniSections.Clear();
            generalIni.GetSections(iniSections);
            if (iniSections.Count == 0)
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
        public void WriteIni()
        {
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
    }
}