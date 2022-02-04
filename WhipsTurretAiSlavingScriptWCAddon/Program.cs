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

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public static WcPbApi api;

        public List<IMyTerminalBlock> Turrets = new List<IMyTerminalBlock>();
        public List<MyDefinitionId> WeaponDefinitions = new List<MyDefinitionId>();
        public List<string> definitionSubIds = new List<string>();
        Dictionary<IMyEntity, float> dict = new Dictionary<IMyEntity, float>();

        static MyIni generalIni = new MyIni();
        const string INI_GENERAL_SECTION = "General Parameters";
        const string INI_AI_TURRET_NAME = "ai_turret_group_tag";
        const string INI_DESIGNATOR_NAME = "designator_name_tag";
        const string INI_CONVERGENCE = "manual_convergence_range";
        static string AiTurretGroupNameTag = "Slaved Group";
        static string DesignatorNameTag = "Designator";

        static List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();
        static List<IMyBlockGroup> turretGroups = new List<IMyBlockGroup>();
        static List<IMyTerminalBlock> designators = new List<IMyTerminalBlock>();
        static IMyTerminalBlock designator = null;
        static int oldConvergence = 0;
        static int convergence = 0;

        public int runCount = 20;

        public void Main(string argument, UpdateType updateSource)
        {
            api = new WcPbApi();
            try {
                api.Activate(Me);
            }
            catch {
                Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); return;
            }
            if (runCount >= 20)
            {
                pbs.Clear();
                GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbs);
                pbs.RemoveAll(x => !(x.CustomData.Contains(INI_GENERAL_SECTION)) || !(x.CustomData.Contains(INI_AI_TURRET_NAME)) || !(x.CustomData.Contains(INI_CONVERGENCE))
                    || !(x.CustomData.Contains(INI_DESIGNATOR_NAME)));
                Echo("Found " + pbs.Count + " PB(s) with Whip's script");
                pbs.ForEach(pb => {
                    generalIni.Clear();
                    generalIni.TryParse(pb.CustomData);
                    AiTurretGroupNameTag = generalIni.Get(INI_GENERAL_SECTION, INI_AI_TURRET_NAME).ToString(AiTurretGroupNameTag);
                    DesignatorNameTag = generalIni.Get(INI_GENERAL_SECTION, INI_DESIGNATOR_NAME).ToString(DesignatorNameTag);
                });
                dict.Clear();
                api.GetSortedThreats(Me.CubeGrid, dict);
                turretGroups.Clear();
                GridTerminalSystem.GetBlockGroups(turretGroups, g => g.Name.Contains(AiTurretGroupNameTag));
                Echo("Managing " + turretGroups.Count + " Slaved Groups\n");
                turretGroups.ForEach(g => {
                    designators.Clear();
                    designator = null;
                    g.GetBlocksOfType<IMyTerminalBlock>(designators, d => d.CustomName.Contains(DesignatorNameTag));
                    if (designators.Count != 0) {
                        designator = designators[0];
                        if (api.HasCoreWeapon(designator)) {
                            pbs.ForEach(pb => {
                                if (dict.Count != 0) {
                                    //Retrieves Current Turret Direction
                                    //--------------------------------
                                    #region Turret Vector Snippet Originally by Rdav in Rdav's Fleet Command MkII
                                    //GETTING TURRET VECTOR                                           //If tracking identify direction and position
                                    Matrix AZ = api.GetWeaponAzimuthMatrix(designator, 0);
                                    Matrix EL = api.GetWeaponElevationMatrix(designator, 0);
                                    //-----------------------------------------------------------------------------------------      
                                    Vector3D ORIGINPOS = designator.GetPosition();
                                    //---------------------------------------- 
                                    //Get forward unit vector       
                                    var FORWARDPOS = designator.Position + Base6Directions.GetIntVector(designator.Orientation.TransformDirection(Base6Directions.Direction.Forward));
                                    var FORWARD = designator.CubeGrid.GridIntegerToWorld(FORWARDPOS);
                                    var FORWARDVECTOR = Vector3D.Normalize(FORWARD - ORIGINPOS);

                                    Vector3D tmpVECTOR = Vector3D.Rotate(FORWARDVECTOR, AZ);
                                    Vector3D TURRETVECTOR = Vector3D.Rotate(tmpVECTOR, EL);
                                    //---------------------------------------- 
                                    //Get Up unit vector        
                                    var UPPOS = designator.Position + Base6Directions.GetIntVector(designator.Orientation.TransformDirection(Base6Directions.Direction.Up));
                                    var UP = designator.CubeGrid.GridIntegerToWorld(UPPOS);
                                    var UPVECTOR = Vector3D.Normalize(UP - ORIGINPOS);
                                    //----------------------------------------     
                                    Quaternion QUAT_ONE = Quaternion.CreateFromForwardUp(FORWARDVECTOR, UPVECTOR);
                                    //---------------------------------------- 
                                    //APPLYING QUAT TO A         
                                    Vector3D TARGETPOS1 = Vector3D.Transform(TURRETVECTOR, QUAT_ONE);
                                    TARGETPOS1 = Vector3D.Negate(TARGETPOS1);
                                    RayD turretRay = new RayD(ORIGINPOS, TARGETPOS1);
                                    #endregion
                                    foreach (IMyEntity k in dict.Keys)
                                    {
                                        if (k != null)
                                        {
                                            IMyEntity target = k;
                                            if (k.WorldAABB.Intersects(turretRay) < api.GetMaxWeaponRange(designator, 0) && k.WorldAABB.Intersects(turretRay) > 0)
                                            {
                                                convergence = MathHelper.RoundToInt((target.WorldAABB.Center - ORIGINPOS).Length());
                                                if (oldConvergence == 0 || convergence < oldConvergence)
                                                    oldConvergence = convergence;
                                            }
                                        }
                                    }
                                }
                                if (oldConvergence == 0 || double.IsNaN(oldConvergence) || double.IsInfinity(oldConvergence)) {
                                    oldConvergence = (int) api.GetMaxWeaponRange(designator, 0);
                                    generalIni.Set(g.Name, INI_CONVERGENCE, oldConvergence);
                                }
                                string output = generalIni.ToString();
                                if (!string.Equals(output, Me.CustomData))
                                    pb.CustomData = output;
                            });
                        }
                    }
                });
            }
        }
        
    }
}