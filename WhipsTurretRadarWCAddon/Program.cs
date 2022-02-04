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
        //----------------###Config options#####--------------
        //Below which threat level should targets be ignored
        int minimumThreat = 5;

        //----------------###No touchey below#####--------------
        const string IGC_TAG = "IGC_IFF_MSG";
        const string INI_SECTION_GENERAL = "Radar - General";
        const string INI_USE_RANGE_OVERRIDE = "Use radar range override";

        IMyBroadcastListener broadcastListener;

        List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();

        float minimumOffRat = 1;

        readonly MyIni generalIni = new MyIni();

        public static WcPbApi api;

        public void Main(string argument, UpdateType updateSource)
        {
            if(minimumThreat>=9)
                minimumOffRat = 5F;
            if(minimumThreat==8)
                minimumOffRat = 4F;
            if(minimumThreat==7)
                minimumOffRat = 3F;
            if(minimumThreat==6)
                minimumOffRat = 2F;
            if(minimumThreat==5)
                minimumOffRat = 1F;
            if(minimumThreat==4)
                minimumOffRat = 0.5F;
            if(minimumThreat==3)
                minimumOffRat = 0.25F;
            if(minimumThreat==2)
                minimumOffRat = 0.125F;
            if(minimumThreat==1)
                minimumOffRat = 0.0625F;
            if(minimumThreat<=0)
                minimumOffRat = 0F;
            api = new WcPbApi();
            try
            {
                api.Activate(Me);
            }
            catch
            {
                Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); return;
            }
            var myTuple = new MyTuple<byte, long, Vector3D, double>((byte)2, Me.CubeGrid.EntityId, Me.CubeGrid.WorldAABB.Center, 0);
            IGC.SendBroadcastMessage(IGC_TAG, myTuple);
            if(api.HasGridAi(Me.CubeGrid.EntityId))
            {
                pbs.Clear(); 
                GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbs);
                pbs.RemoveAll(x => !(x.CustomData.Contains("Use radar range override=false")));
                pbs.ForEach(pb=>{
                    generalIni.Clear();
                    generalIni.TryParse(pb.CustomData);
                    generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, true);
                    string output = generalIni.ToString();
                    if (!string.Equals(output, pb.CustomData))
                        pb.CustomData = output;
                }); 
                Echo("Sending Target Data");
                Dictionary<MyDetectedEntityInfo, float> dict = new Dictionary<MyDetectedEntityInfo, float>();
                api.GetSortedThreats(Me,dict);
                foreach(MyDetectedEntityInfo k in dict.Keys)
                {
                    if(dict[k]>=minimumOffRat)
                    {
                        if(!k.IsEmpty())
                        {
                            if(k.Type == MyDetectedEntityType.SmallGrid||k.Type == MyDetectedEntityType.LargeGrid)
                            {
                                MyRelationsBetweenPlayerAndBlock relation = k.Relationship;
                                int relationvalue=1;
                                switch(relation)
                                {
                                    case MyRelationsBetweenPlayerAndBlock.Enemies : {relationvalue = 1;break;};
                                    case MyRelationsBetweenPlayerAndBlock.NoOwnership : {relationvalue = 0;break;};
                                    case MyRelationsBetweenPlayerAndBlock.Owner : {relationvalue = 2;break;};
                                    case MyRelationsBetweenPlayerAndBlock.FactionShare : {relationvalue = 2;break;};
                                    case MyRelationsBetweenPlayerAndBlock.Neutral : {relationvalue = 0;break;};
                                    case MyRelationsBetweenPlayerAndBlock.Friends : {relationvalue = 2;break;};
                                }
                                myTuple = new MyTuple<byte, long, Vector3D, double>((byte)relationvalue, k.EntityId, k.BoundingBox.Center, 0);
                            }
                            else
                            {
                                myTuple = new MyTuple<byte, long, Vector3D, double>((byte)1, k.EntityId, k.BoundingBox.Center, 0);
                            }
                            IGC.SendBroadcastMessage(IGC_TAG, myTuple);
                        }
                    }
                }
            }
            else
            {
                Echo("No WeaponCore Weapon Installed!");
            }
        }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            // IGC Register
            broadcastListener = IGC.RegisterBroadcastListener(IGC_TAG);
            broadcastListener.SetMessageCallback(IGC_TAG);
        }

        
    }
}