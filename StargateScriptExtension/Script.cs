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

namespace StargateScriptExtension
{
    public class Program : MyGridProgram
    {
        // Block name of the gate that will be dialing, must be on same grid as this PB.
        string SourceGate = "Stargate";
        //Name-Tag to find all LCDs that should display the state of the gate
        string LCDString = "[SG-LCD]";
        //Name-Tag to find all LCDs that should display recent connections
        string LCDLogString = "[SG-LCD-Log]";
        //Name-Tag to find all blocks that should get active in any case
        string AlarmString = "[SG-Alarm]";
        //Name-Tag to find all blocks that should get active when dialing out
        string OutAlarmString = "[SG-Alarm-Outgoing]";
        //Name-Tag to find all blocks that should get active when the gate gets dialed at
        string InAlarmString = "[SG-Alarm-Incoming]";
        //should the iris close when the gate gets dialed at?
        bool irisAlarm = false;
        //should recent connections be displayed on LCDs?
        bool showConnections = false;
        //how long should the gate shutdown status be displayed?
        int shutdowntime = 20;

        //----------------------------NO TOUCHEY BELOW HERE------------------------------//

        List<IMyTerminalBlock> lcds = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> lcdlogs = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> outblocks = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> inblocks = new List<IMyTerminalBlock>();

        string baseTextureName = "Stargate SG1 ";
        string irissuffix = " Iris";
        List<string> destinations = new List<string>();
        string destination = "";
        bool irisStatus = false;
        bool incoming = false;
        bool dialsuccesful = false;
        bool alarmsActive = false;
        int shutdowncounter = 0;
        int counter = 60;
        IMyTerminalBlock gate;

        public Program()
        {
           Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument)
        {
            if(argument!="")
            {
               if(argument.ToLower()=="clear")
               {
                   destinations = new List<string>();
                   destination = "";
                   irisAlarm = false;
                   showConnections = false;
               }
               if(argument.ToLower()=="toggleiris")
               {
                   irisAlarm = !irisAlarm;
               }
               if(argument.ToLower()=="toggleshow")
               {
                   showConnections = !showConnections;
               }
            }
            if(counter<60)
                counter++;
            if(counter==60){
                lcds.Clear(); 
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(lcds,x=>x.CustomName.ToLower().Contains(LCDString.ToLower()));
                lcdlogs.Clear(); 
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(lcdlogs,x => (x.CustomName.ToLower().Contains(LCDLogString.ToLower())));
                blocks.Clear(); 
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks,x=>(x.CustomName.ToLower().Contains(AlarmString.ToLower())));
                outblocks.Clear(); 
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(outblocks,x => (x.CustomName.ToLower().Contains(OutAlarmString.ToLower())));
                inblocks.Clear(); 
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(inblocks,x => !(x.CustomName.ToLower().Contains(InAlarmString.ToLower())));
                gate = GridTerminalSystem.GetBlockWithName(SourceGate);
            }
            irisStatus = gate.GetValue<bool>("Phoenix.Stargate.Iris");
            destination = gate.GetValue<string>("Phoenix.Stargate.Destination");
            Echo("Incoming Wormhole activates Iris:"+irisAlarm);
            if(destination!=""&&dialsuccesful)
            {
               if(destinations.Count>0)
               {
                   if(destinations[destinations.Count-1]!=destination)
                   {
                       destinations.Add(destination);
                   }
               }
               else
               {
                   destinations.Add(destination);
               }
            }
            if(showConnections)
            {
               for (int i =0; i <lcdlogs.Count;++i) 
               { 
                   string txt = "";
                   for (int j =destinations.Count-1; j >=0;--j) 
                   { 
                       txt=txt+(destinations[j])+"\n";
                   }
                   handleTextLCD(lcdlogs[i],txt);
               }
               Echo((destination));
            }
            else
            {
               for (int i =0; i <lcdlogs.Count;++i) 
               {
                   handleTextLCD(lcdlogs[i],"");
               }
               Echo("");
            }                  
            if (gate != null) 
            { 

                // Get status of gate
                var state = gate.GetValue<string>("Phoenix.Stargate.Status"); 
                if (state == "Idle") 
                {
                   for (int i =0; i <lcds.Count;++i) 
                   { 
                       if (shutdowncounter>0)
                       {
                           handleLCD(lcds[i],baseTextureName+"Shutdown");
                           shutdowncounter--; 
                       }
                       else
                       {
                           handleLCD(lcds[i],baseTextureName+state+(irisStatus?irissuffix:""));
                       } 
                   }  
                   for (int i =0; i < blocks.Count;++i) 
                   { 
                       deactivateBlock(blocks[i]); 
                   } 
                   for (int i =0; i < outblocks.Count;++i) 
                   { 
                       deactivateBlock(outblocks[i]); 
                   } 
                   for (int i =0; i < inblocks.Count;++i) 
                   { 
                       deactivateBlock(inblocks[i]); 
                   }
                   incoming = false;
                   dialsuccesful = false;
                   alarmsActive = false;   
                } 
                else if (state == "Active") 
                {
                    for (int i =0; i <lcds.Count;++i) 
                   { 
                       handleLCD(lcds[i],baseTextureName+(incoming?"Incoming":"Outgoing")+(irisStatus?irissuffix:"")); 
                   } 
                   shutdowncounter = shutdowntime;
                   dialsuccesful = true; 
                } 
                else if (state == "Dialing")  
                {
                   for (int i =0; i <lcds.Count;++i) 
                   { 
                       handleLCD(lcds[i],baseTextureName+(incoming?"Incoming"+(irisStatus?irissuffix:""):state));
                   }   
                   for (int i =0; i < outblocks.Count;++i) 
                   { 
                       activateBlock(outblocks[i]); 
                   }    
                   for (int i =0; i < blocks.Count;++i) 
                   { 
                       activateBlock(blocks[i]); 
                   } 
                   shutdowncounter = shutdowntime;
                   alarmsActive = true;  
                }  
                else if (state == "Incoming")   
                {
                   for (int i =0; i <lcds.Count;++i) 
                   { 
                       handleLCD(lcds[i],baseTextureName+"Dialing"+(irisStatus?irissuffix:""));
                   }    
                   if(irisAlarm)
                       gate.ApplyAction("Phoenix.Stargate.Iris_On");
                   for (int i =0; i < inblocks.Count;++i) 
                   { 
                       activateBlock(inblocks[i]); 
                   } 
                   for (int i =0; i < blocks.Count;++i) 
                   { 
                       activateBlock(blocks[i]); 
                   } 
                   incoming = true;
                   shutdowncounter = shutdowntime;
                   alarmsActive = true; 
                }   
            } 
        }

        public void activateBlock(IMyTerminalBlock block)
        {
            (block as IMyFunctionalBlock).ApplyAction("OnOff_On");
           if((block is IMySoundBlock) && !alarmsActive)
           {
               (block as IMySoundBlock).Play();
           }
           if (block is IMyProgrammableBlock && !alarmsActive)
           {
               block.ApplyAction("Run");
           }
           else if (block is IMyTimerBlock && !alarmsActive)
           {
                block.ApplyAction("TriggerNow");
           }
        }

        public void deactivateBlock(IMyTerminalBlock block)
        {
            (block as IMyFunctionalBlock).ApplyAction("OnOff_Off");
           if((block is IMySoundBlock) && alarmsActive)
           {
               (block as IMySoundBlock).Stop();
           }
        }

        public void handleTextLCD(IMyTerminalBlock lcd, string txt)
        {
           if(lcd.CustomData!="")
           {
               var pos = int.Parse(lcd.CustomData, System.Globalization.CultureInfo.InvariantCulture);
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(pos);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.ClearImagesFromSelection();
               surf.WriteText(txt);
               frame.Dispose();
           }
           else
           {
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(0);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.ClearImagesFromSelection();
               surf.WriteText(txt);
               frame.Dispose();
           }
        }

        public void handleLCD(IMyTerminalBlock lcd, string picture)
        {
           if(lcd.CustomData!="")
           {
               var pos = int.Parse(lcd.CustomData, System.Globalization.CultureInfo.InvariantCulture);
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(pos);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.WriteText("");
               surf.ClearImagesFromSelection();
               surf.AddImageToSelection(picture);  
               surf.PreserveAspectRatio = true;
               frame.Dispose();
           }
           else
           {
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(0);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.WriteText("");
               surf.ClearImagesFromSelection();
               surf.AddImageToSelection(picture);  
               surf.PreserveAspectRatio = true;
               frame.Dispose();
           }
        }
    }
}