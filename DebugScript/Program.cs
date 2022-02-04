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

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public static WcPbApi api;

        Dictionary<MyDetectedEntityInfo, float> dict = new Dictionary<MyDetectedEntityInfo, float>();
        public List<MyDefinitionId> WeaponDefinitions = new List<MyDefinitionId>();
        public List<IMyTerminalBlock> Weapons = new List<IMyTerminalBlock>();
        public List<string> targetTypes = new List<string>();
        private StringBuilder EchoString = new StringBuilder(); 
        private IMyTerminalBlock weaponBlock;
        long id = -1;
        bool result = false;
        IDictionary<string, int> temp = new Dictionary<string, int>();

        public void Main(string argument, UpdateType updateSource)
        {
            api = new WcPbApi();
            try{
                api.Activate(Me);
            }
            catch{
                Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); return;
            }
            EchoString.Clear();
            weaponBlock = null;
            result = false;
            id = Me.CubeGrid.EntityId;
            WeaponDefinitions.Clear();
            Weapons.Clear();
            api.GetAllCoreWeapons(WeaponDefinitions);
            EchoString.Append("Total Weapons registered: "+WeaponDefinitions.Count+"\n");
            WeaponDefinitions.Clear();
            api.GetAllCoreStaticLaunchers(WeaponDefinitions);
            EchoString.Append("Total Static Weapons registered: "+WeaponDefinitions.Count+"\n");
            WeaponDefinitions.Clear();
            api.GetAllCoreTurrets(WeaponDefinitions);
            EchoString.Append("Total Turret Weapons registered: "+WeaponDefinitions.Count+"\n");
            EchoString.Append("Grid Info:\n");
            EchoString.Append("-----------------------\n");
            EchoString.Append("Has GridAi: "+ api.HasGridAi(id));
            EchoString.Append("GridAi Target Data:\n");
            GetTargetInfo((MyDetectedEntityInfo)api.GetAiFocus(id));
            api.GetSortedThreats(Me,dict);
            EchoString.Append("Available Targets: "+dict.Count+"\n");
            EchoString.Append("Optimal DPS:"+api.GetOptimalDps(id) +"\n");
            EchoString.Append("Construct DPS:"+api.GetConstructEffectiveDps(id) +"\n");
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(Weapons, b => b.CustomName.ToLower().Contains("[debug]"));
            if(Weapons.Count>0){
                weaponBlock = Weapons[0];
                EchoString.Append("Weapon Info:\n");
                EchoString.Append("-----------------------\n");
                EchoString.Append("Has Core Weapon: "+ api.HasCoreWeapon(weaponBlock));
                temp.Clear();
                result = api.GetBlockWeaponMap(weaponBlock,temp);
                EchoString.Append("Muzzles: "+(result?temp.Count:0)+"\n");
                result = false;
                EchoString.Append("Weapon Target Info:\n");
                GetTargetInfo((MyDetectedEntityInfo)api.GetWeaponTarget(weaponBlock));
                EchoString.Append("Ready To Fire:"+api.IsWeaponReadyToFire(weaponBlock)+"\n");
                EchoString.Append("Max Range:"+api.GetMaxWeaponRange(weaponBlock, 0)+"\n");
                result = api.GetTurretTargetTypes(weaponBlock,targetTypes);
                EchoString.Append("Target Types: ");
                for(int i = 0; i<targetTypes.Count;i++){
                    EchoString.Append(targetTypes[i]);
                    if(i<(targetTypes.Count-1))
                        EchoString.Append(", ");
                }
                EchoString.Append("\n");
                EchoString.Append("Active Ammo: "+api.GetActiveAmmo(weaponBlock,0)+"\n");
                EchoString.Append("Controlled By: "+api.GetPlayerController(weaponBlock)+"\n");
                if (WeaponDefinitions.Contains(weaponBlock.BlockDefinition))
                {
                    //Retrieves Current Turret Direction
                    //--------------------------------
                    #region Turret Vector Snippet Originally by Rdav in Rdav's Fleet Command MkII
                    //GETTING TURRET VECTOR                                           //If tracking identify direction and position
                    Matrix AZ = api.GetWeaponAzimuthMatrix(weaponBlock, 0);
                    Matrix EL = api.GetWeaponElevationMatrix(weaponBlock, 0);
                    //-----------------------------------------------------------------------------------------      
                    Vector3D ORIGINPOS = weaponBlock.GetPosition();
                    //---------------------------------------- 
                    //Get forward unit vector       
                    var FORWARDPOS = weaponBlock.Position + Base6Directions.GetIntVector(weaponBlock.Orientation.TransformDirection(Base6Directions.Direction.Forward));
                    var FORWARD = weaponBlock.CubeGrid.GridIntegerToWorld(FORWARDPOS);
                    var FORWARDVECTOR = Vector3D.Normalize(FORWARD - ORIGINPOS);

                    Vector3D tmpVECTOR = Vector3D.Rotate(FORWARDVECTOR, AZ);
                    Vector3D TURRETVECTOR = Vector3D.Rotate(tmpVECTOR, EL);
                    //---------------------------------------- 
                    //Get Up unit vector        
                    var UPPOS = weaponBlock.Position + Base6Directions.GetIntVector(weaponBlock.Orientation.TransformDirection(Base6Directions.Direction.Up));
                    var UP = weaponBlock.CubeGrid.GridIntegerToWorld(UPPOS);
                    var UPVECTOR = Vector3D.Normalize(UP - ORIGINPOS);
                    //----------------------------------------     
                    Quaternion QUAT_ONE = Quaternion.CreateFromForwardUp(FORWARDVECTOR, UPVECTOR);
                    //---------------------------------------- 
                    //APPLYING QUAT TO A         
                    Vector3D TARGETPOS1 = Vector3D.Transform(TURRETVECTOR, QUAT_ONE);
                    TARGETPOS1 = Vector3D.Negate(TARGETPOS1);
                    #endregion
                    EchoString.Append("Aim Vector: " + TARGETPOS1.ToString() + "\n");
                }
                Me.CustomData = "";
                Me.CustomData = EchoString.ToString();
                Echo(Me.CustomData);
            }

        }

        public void GetTargetInfo(MyDetectedEntityInfo info){
            if(info.IsEmpty()){
                EchoString.Append("None\n");
                return;
            }
            EchoString.Append("Id: "+ info.EntityId+ "\n");
            EchoString.Append("Name: "+ info.Name+ "\n");
            EchoString.Append("Type: "+ info.Type+ "\n");
            EchoString.Append("HitPosition: "+ (info.HitPosition==null?"unknown":info.HitPosition.ToString())+ "\n");
            EchoString.Append("Relation: "+ info.Relationship.ToString()+ "\n");
            EchoString.Append("Position: "+ info.Position.ToString()+ "\n");
        }
    }
}