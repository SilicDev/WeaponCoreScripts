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

        public static WcPbApi api;

        public List<IMyTerminalBlock> StaticWeapons = new List<IMyTerminalBlock>();
        public List<MyDefinitionId> WeaponDefinitions = new List<MyDefinitionId>();
        public List<string> definitionSubIds = new List<string>();

        public void Main(string argument, UpdateType updateSource)
        {
            api = new WcPbApi();
            try{
                api.Activate(Me);
            }
            catch (Exception e){
                Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); 
                Echo(e.StackTrace);
                return;
            }
            StaticWeapons.Clear();
            WeaponDefinitions.Clear();
            api.GetAllCoreStaticLaunchers(WeaponDefinitions);
            definitionSubIds.Clear();
            WeaponDefinitions.ForEach(d=>definitionSubIds.Add(d.SubtypeName));
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(StaticWeapons,b => b.CubeGrid == Me.CubeGrid &&definitionSubIds.Contains(b.BlockDefinition.SubtypeName));
            StaticWeapons.ForEach(b => api.FireWeaponOnce(b));
        }
    }
}