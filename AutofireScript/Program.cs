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
        /*--------------------< NO TOUCHEY BELOW HERE! >--------------------*/

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
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
        private StringBuilder EchoString = new StringBuilder(); 
        private List<IMyBlockGroup> weapongroups = new List<IMyBlockGroup>();
        private List<WeaponGroup> groups = new List<WeaponGroup>();
        private int counter = 200;

        public const string GeneralIniTag = "General Config";
        public static MyIni generalIni = new MyIni();
        public static List<string> iniSections = new List<string>();
        private static string groupNameTag = "Auto-Fire";

        public void Main(string argument, UpdateType updateSource)
        {
            EchoString.Clear();
            ParseIni();
            api = new WcPbApi();
            try{
                api.Activate(Me);
            }
            catch{
                Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); return;
            }
            if(api.HasGridAi(Me.CubeGrid.EntityId)){
                dict.Clear();
                api.GetSortedThreats(Me,dict);
                if(dict.Count!=0){
                    foreach(MyDetectedEntityInfo k in dict.Keys)
                    {
                        if(k.Relationship==MyRelationsBetweenPlayerAndBlock.Enemies&&(k.Type == MyDetectedEntityType.SmallGrid||k.Type == MyDetectedEntityType.LargeGrid)){
                            EchoString.Append("Enemy Detected; Firing weapons");
                            if(counter>=200){
                                GridTerminalSystem.GetBlockGroups(weapongroups,b => b.Name.Contains(groupNameTag));
                                groups.Clear();
                                foreach (IMyBlockGroup g in weapongroups){
                                    WeaponGroup wg = WeaponGroup.create(g);
                                    if(wg!=null)
                                        groups.Add(wg);
                                }
                                counter=0;
                            }
                            foreach (WeaponGroup wg in groups){
                                wg.tick();
                            }
                            counter++;
                            break;
                        }
                    }
                }
            }
            Echo(EchoString.ToString());
        }

        public void ParseIni(){
            generalIni.Clear();
            generalIni.TryParse(Me.CustomData);

            iniSections.Clear();
            generalIni.GetSections(iniSections);
            if(iniSections.Count==0)
                generalIni.EndContent = Me.CustomData;
            groupNameTag = generalIni.Get(GeneralIniTag,"Weapon Group name tag").ToString(groupNameTag);
            groups.ForEach(g=>g.ParseGroupIni());
            WriteIni();
        }

        public void WriteIni(){
            generalIni.Set(GeneralIniTag,"Weapon Group name tag",groupNameTag);
            groups.ForEach(g=>g.WriteGroupIni());
            string output = generalIni.ToString();
            if (!string.Equals(output, Me.CustomData))
                Me.CustomData = output;
        }

        public class WeaponGroup{
            public List<IMyTerminalBlock> Weapons = new List<IMyTerminalBlock>();
            public static List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
            private int sequenceTimer = 0;
            private int shootingTimer = 0;
            private int shootingDelay = 10;
            private string Name;
            private bool shootUntilEmpty = false;

            private WeaponGroup(List<IMyTerminalBlock> weapons, string _Name){
                Weapons.AddRange(weapons);
                Name = _Name.Replace(groupNameTag,"Group: ");
            }

            public static WeaponGroup create(IMyBlockGroup bGroup){
                temp.Clear();
                bGroup.GetBlocks(temp,b => api.HasCoreWeapon(b)&&b.IsFunctional);
                if(temp.Count!=0)
                    return new WeaponGroup(temp,bGroup.Name);
                return null;
            }

            public void tick(){
                if(shootingTimer>=shootingDelay){
                    if(sequenceTimer>=Weapons.Count)
                        sequenceTimer=0;
                    api.FireWeaponOnce(Weapons[sequenceTimer]);
                    if(!shootUntilEmpty)
                        sequenceTimer++;
                    else{
                        if(!api.IsWeaponReadyToFire(Weapons[sequenceTimer],0,true,false))
                            sequenceTimer++;
                    }
                    shootingTimer = 0;
                }
                shootingTimer++;
            }
            
            public void ParseGroupIni(){
                shootingDelay = generalIni.Get(Name,"Time between Shots").ToInt32(shootingDelay);
                shootUntilEmpty = generalIni.Get(Name,"Shoot until empty").ToBoolean(shootUntilEmpty);
            }

            public void WriteGroupIni(){
                generalIni.Set(Name,"Time between Shots",shootingDelay);
                generalIni.Set(Name,"Shoot until empty",shootUntilEmpty);
            }
        }
    }
}