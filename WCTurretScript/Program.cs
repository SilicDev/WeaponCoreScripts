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

        public const string VERSION = "1.18.1";

        public const string GENERAL_INI_TAG = "General Config";
        public const string GROUP_NAME_KEY = "Turret Group name tag";
        public const string DESIGNATOR_NAME_KEY = "Designator Turret name tag (Vanilla only)";
        public const string AZIMUTH_NAME_KEY = "Azimuth rotor name tag";
        public const string ELEVATION_NAME_KEY = "Elevation rotor name tag";
        public const string TIMER_NAME_KEY = "Fire Timer name tag";
        public const string WC_TARGET_KEY = "Allow Main Target Tracking";
        public const string LOS_CHECK_KEY = "Allow Line Of Sight Checking (Performance intensive)";
        public const string WAIT_CYCLES_KEY = "Minor Cycles";
        public const string DEBUG_KEY = "Print Debug";

        public const string MAX_SPEED_KEY = "Maximum turning speed";
        public const string ENGAGE_DIST_KEY = "Engagement distance (m)";
        public const string MAX_DIVERGE_KEY = "Maximum divergence from target";
        public const string OFFSET_KEY = "Offset ticks between shots";
        public const string INVERT_ELEVATION_KEY = "Invert Elevation Rotor Rotation";
        public const string MINIMUM_THREAT_KEY = "Minimum Target Threat Level";
        public const string STRICT_GRIDS_KEY = "Only allow blocks on rotor grids";

        public static string ElevationNameTag = "Elevation";
        public static string AzimuthNameTag = "Azimuth";
        public static string DesignatorNameTag = "Designator";
        public static string TimerNameTag = "Fire";
        public static string GroupNameTag = "Turret Group";
        public static bool AllowWcTargeting = false;
        public static bool AllowLOS = false;
        public static int WaitCycles = 500;
        public static bool IsDebug = false;

        public static WcPbAPI Api = new WcPbAPI();
        public static bool IsApiActivated = false;

        public static List<MyDefinitionId> WeaponDefinitions = new List<MyDefinitionId>();
        public static List<MyDefinitionId> TurretDefinitions = new List<MyDefinitionId>();
        public static List<string> StaticWeaponDefinitionSubIds = new List<string>();
        public static List<string> TurretDefinitionSubIds = new List<string>();

        public static List<Turret> Turrets = new List<Turret>();
        public List<IMyBlockGroup> TurretGroups = new List<IMyBlockGroup>();
        public static Dictionary<MyDetectedEntityInfo, float> Targets = new Dictionary<MyDetectedEntityInfo, float>();

        public static List<IMyLargeTurretBase> Directors = new List<IMyLargeTurretBase>();
        public IMyLargeTurretBase Director;


        public static StringBuilder EchoString = new StringBuilder();
        public static List<string> ErrorLog = new List<string>();
        public static MyIni ConfigIni = new MyIni();
        public static List<string> IniSections = new List<string>();

        public int RunCount = WaitCycles;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            RunCount++;
            EchoString.Clear();

            EchoString.Append($"Managing {Turrets.Count} Turret(s)\n");

            if (!IsApiActivated)
            {
                try
                {
                    Api.Activate(Me);
                    IsApiActivated = true;
                    WeaponDefinitions.Clear();
                    TurretDefinitions.Clear();

                    Api.GetAllCoreStaticLaunchers(WeaponDefinitions);
                    Api.GetAllCoreTurrets(TurretDefinitions);

                    StaticWeaponDefinitionSubIds.Clear();
                    TurretDefinitionSubIds.Clear();

                    WeaponDefinitions.ForEach(d => StaticWeaponDefinitionSubIds.Add(d.SubtypeName));
                    TurretDefinitions.ForEach(t => TurretDefinitionSubIds.Add(t.SubtypeName));
                }
                catch
                {
                    EchoString.Append("WeaponCore Api is failing!\nMake sure WeaponCore is enabled!\n");
                }
            }

            if (IsApiActivated && Api.HasGridAi(Me.CubeGrid.EntityId))
            {
                EchoString.Append("Attempting WC Targeting\n");
                EchoString.Append($"Detected {Targets.Count} target(s)!\n");

                if (RunCount >= WaitCycles)
                {
                    Targets.Clear();
                    Api.GetSortedThreats(Me, Targets);
                }

                Turrets.ForEach(t => t.AimAtTarget());
            }
            else
            {
                AttemptVanillaTargeting();
            }

            if (RunCount >= WaitCycles)
            {
                Turrets.ForEach(t => t.HasUpdated = false);

                TurretGroups.Clear();
                GridTerminalSystem.GetBlockGroups(TurretGroups, g => g.Name.Contains(GroupNameTag));
                TurretGroups.ForEach(g =>
                {
                    Turret newTurret = Turret.AttemptCreateFromGroup(g);
                    if (newTurret != null)
                        Turrets.Add(newTurret);
                });

                // Cleanup stale turrets
                Turrets.RemoveAll(t => {
                    if (!t.HasUpdated)
                    {
                        Turret.Names.Remove(t.Name);
                    }
                    return !t.HasUpdated;
                });

                ParseIni();
                RunCount = 0;
            }

            if (IsDebug)
            {
                EchoString.Append("------------------------------------\ndebug area:\n");
                ErrorLog.ForEach(e => EchoString.AppendLine(e));
                EchoString.AppendLine();
                Turrets.ForEach(t => t.Debug());
            }

            Echo(EchoString.ToString());
        }

        /*
         * handles Vanilla targeting
         * relevant docs: https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyLargeTurretBase
         */

        public void AttemptVanillaTargeting()
        {
            if (RunCount >= WaitCycles)
            {
                RunCount = 0;
            }
            if (Directors.Count != 0)
            {
                EchoString.Append("Attempting Vanilla Targeting\n");
                foreach (var item in Directors)
                {
                    if (item.IsShooting && item.HasTarget)
                    {
                        Director = item;
                        break;
                    }
                }
                if (Director != null && Director.HasTarget)
                {
                    Turrets.ForEach(t => t.AimAtTarget());
                }
            }
            else
            {
                Turrets.ForEach(t => t.MoveToRest());
            }
        }

        //https://github.com/SilicDev/WeaponCore/blob/master/Data/Scripts/CoreSystems/Ui/Targeting/TargetUiDraw.cs#L966-L976
        public static int ConvertOffenseRatingToThreatLevel(float offenseRating)
        {
            if (offenseRating > 5)      return 9;
            if (offenseRating > 4)      return 8;
            if (offenseRating > 3)      return 7;
            if (offenseRating > 2)      return 6;
            if (offenseRating > 1)      return 5;
            if (offenseRating > 0.5)    return 4;
            if (offenseRating > 0.25)   return 3;
            if (offenseRating > 0.125)  return 2;
            if (offenseRating > 0.0625) return 1;
            if (offenseRating > 0)      return 0;
            return -1;
        }

        //https://github.com/SilicDev/WeaponCore/blob/master/Data/Scripts/CoreSystems/Ui/Targeting/TargetUiDraw.cs#L966-L976
        public static float ConvertThreatLevelToOffenseRating(int threatLevel)
        {
            switch(threatLevel)
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

        /// <summary>Parses config data from CustomData.</summary>
        /// relevant docs: https://github.com/malware-dev/MDK-SE/wiki/VRage.Game.ModAPI.Ingame.Utilities.MyIni
        ///      https://github.com/malware-dev/MDK-SE/wiki/Handling-configuration-and-storage
        /// called functions: Turret.ParseTurretIni(), WriteIni()
        public void ParseIni()
        {
            ConfigIni.Clear();
            ConfigIni.TryParse(Me.CustomData);

            IniSections.Clear();
            ConfigIni.GetSections(IniSections);
            if (IniSections.Count == 0)
                ConfigIni.EndContent = Me.CustomData;
            Me.CustomData = "";

            GroupNameTag = ConfigIni.Get(GENERAL_INI_TAG, GROUP_NAME_KEY).ToString(GroupNameTag);
            DesignatorNameTag = ConfigIni.Get(GENERAL_INI_TAG, DESIGNATOR_NAME_KEY).ToString(DesignatorNameTag);
            AzimuthNameTag = ConfigIni.Get(GENERAL_INI_TAG, AZIMUTH_NAME_KEY).ToString(AzimuthNameTag);
            ElevationNameTag = ConfigIni.Get(GENERAL_INI_TAG, ELEVATION_NAME_KEY).ToString(ElevationNameTag);
            TimerNameTag = ConfigIni.Get(GENERAL_INI_TAG, TIMER_NAME_KEY).ToString(TimerNameTag);
            AllowWcTargeting = ConfigIni.Get(GENERAL_INI_TAG, WC_TARGET_KEY).ToBoolean(AllowWcTargeting);
            AllowLOS = ConfigIni.Get(GENERAL_INI_TAG, LOS_CHECK_KEY).ToBoolean(AllowLOS);
            WaitCycles = ConfigIni.Get(GENERAL_INI_TAG, WAIT_CYCLES_KEY).ToInt32(WaitCycles);
            IsDebug = ConfigIni.Get(GENERAL_INI_TAG, DEBUG_KEY).ToBoolean(IsDebug);

            Turrets.ForEach(t => t.ParseTurretIni());
            WriteIni();
        }

        /// <summary>Writes config data to CustomData.</summary>
        /// relevant docs: https://github.com/malware-dev/MDK-SE/wiki/VRage.Game.ModAPI.Ingame.Utilities.MyIni
        ///      https://github.com/malware-dev/MDK-SE/wiki/Handling-configuration-and-storage
        ///  called functions: Turret.WriteTurretIni()
        public void WriteIni()
        {
            ConfigIni.Set(GENERAL_INI_TAG, GROUP_NAME_KEY, GroupNameTag);
            ConfigIni.Set(GENERAL_INI_TAG, DESIGNATOR_NAME_KEY, DesignatorNameTag);
            ConfigIni.Set(GENERAL_INI_TAG, AZIMUTH_NAME_KEY, AzimuthNameTag);
            ConfigIni.Set(GENERAL_INI_TAG, ELEVATION_NAME_KEY, ElevationNameTag);
            ConfigIni.Set(GENERAL_INI_TAG, TIMER_NAME_KEY, TimerNameTag);
            ConfigIni.Set(GENERAL_INI_TAG, WC_TARGET_KEY, AllowWcTargeting);
            ConfigIni.Set(GENERAL_INI_TAG, LOS_CHECK_KEY, AllowLOS);
            ConfigIni.Set(GENERAL_INI_TAG, WAIT_CYCLES_KEY, WaitCycles);
            ConfigIni.Set(GENERAL_INI_TAG, DEBUG_KEY, IsDebug);

            Turrets.ForEach(t => t.WriteTurretIni());
            string output = ConfigIni.ToString();
            if (!string.Equals(output, Me.CustomData))
                Me.CustomData = output;
        }
    }
}