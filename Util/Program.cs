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

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        public Program()
        {
            
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

        public static WcPbApi API;

        public static List<MyDefinitionId> StaticWeaponDefinitions = new List<MyDefinitionId>();
        public static List<string> StaticWeaponDefinitionSubIds = new List<string>();
        public static List<MyDefinitionId> TurretDefinitions = new List<MyDefinitionId>();
        public static List<string> TurretDefinitionSubIds = new List<string>();

        public void Main(string argument, UpdateType updateSource)
        {
            API = new WcPbApi();
            try{
                API.Activate(Me);
            }
            catch (Exception e){
                Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); 
                Echo(e.StackTrace);
                return;
            }
            StaticWeaponDefinitions.Clear();
            StaticWeaponDefinitionSubIds.Clear();
            API.GetAllCoreStaticLaunchers(StaticWeaponDefinitions);
            StaticWeaponDefinitions.ForEach(d => StaticWeaponDefinitionSubIds.Add(d.SubtypeName));
            
            TurretDefinitions.Clear();
            TurretDefinitionSubIds.Clear();
            API.GetAllCoreTurrets(TurretDefinitions);
            TurretDefinitions.ForEach(d => TurretDefinitionSubIds.Add(d.SubtypeName));
        }
    }
}