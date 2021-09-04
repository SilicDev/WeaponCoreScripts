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

namespace FleetThreatIndicator
{
    public class Program : MyGridProgram
    {
        string AlarmString = "[Alarm]";
        string AmbientString = "[Ambient]";
        string ProximityString = "[Proximity]";
        string ALCDString = "[Alarm-LCD]";
        string TLCDString = "[Threat-LCD]";
        string ALightsString = "[Alarm-Light]";
        int warnThreatLevel = 7;

        //-----------------------<No Touchey Below Here!>-----------------------//
        public static WcPbApi api;
        public static ShieldPbApi sApi;

        public float fleetOffenseRating = 0;
        public float fleetShieldStrength = 0;
        public int totalThreatLevel = 0;
        public int enemyCount = 0;
        public int lastEnemyCount = 0;
        bool gridsDetected = false;
        bool alarmPlaying = false;
        bool musicPlaying = false;
        bool reseted = false;
        int counter = 600;
        Random rnd = new Random();

        List<IMySoundBlock> prox = new List<IMySoundBlock>();
        List<IMySoundBlock> alarms = new List<IMySoundBlock>();
        List<IMySoundBlock> ambient = new List<IMySoundBlock>();
        List<IMyTerminalBlock> aLcds = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> tLcds = new List<IMyTerminalBlock>();
        List<MyTuple<IMyLightingBlock,Color,bool>> alarmLights = new List<MyTuple<IMyLightingBlock,Color,bool>>();
        //List<MyTuple<IMyProgrammableBlock,float>>

        private string IGC_TAG = "[Froid FTI]";
        IMyBroadcastListener broadcastListener;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            // IGC Register
            broadcastListener = IGC.RegisterBroadcastListener(IGC_TAG);
            broadcastListener.SetMessageCallback(IGC_TAG);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if(counter<600)
                counter++;
            if(counter==600)
                fetchBlocks();
            if(api==null||sApi==null){
                api = new WcPbApi();
                sApi = new ShieldPbApi(Me);
            }
            try
            {
                api.Activate(Me);
            }
            catch
            {
                Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); return;
            }
            if(api.HasGridAi(Me.CubeGrid.EntityId))
            {
                fleetOffenseRating = 0;
                fleetShieldStrength = 0;
                totalThreatLevel = -1;
                gridsDetected = false;
                enemyCount = 0;
                Dictionary<MyDetectedEntityInfo, float> dict = new Dictionary<MyDetectedEntityInfo, float>();
                api.GetSortedThreats(Me,dict);
                if(dict.Count!=0){
                    foreach(MyDetectedEntityInfo k in dict.Keys)
                    {
                        if(k.Type == MyDetectedEntityType.SmallGrid||k.Type == MyDetectedEntityType.LargeGrid){
                            gridsDetected = true;
                            enemyCount++;
                            Echo(dict[k]+"");
                            fleetOffenseRating +=dict[k];
                            /*if(sApi.GridHasShield((IMyCubeGrid)k)){
                                sApi.SetActiveShield(sApi.GetShieldBlock(k));
                                fleetShieldStrength +=sApi.GetShieldPercent()*sApi.GetMaxHpCap();
                            }*/
                        }
                    }
                    if(gridsDetected){
                        reseted = false;
                        if(enemyCount>lastEnemyCount){
                            foreach(IMySoundBlock s in prox){
                                s.SelectedSound = "Enemy detected";
                                s.Play();
                            }
                        }
                        if(fleetOffenseRating>5){totalThreatLevel = 9;}
                        else if(fleetOffenseRating>4){totalThreatLevel = 8;}
                        else if(fleetOffenseRating>3){totalThreatLevel = 7;}
                        else if(fleetOffenseRating>2){totalThreatLevel = 6;}
                        else if(fleetOffenseRating>1){totalThreatLevel = 5;}
                        else if(fleetOffenseRating>0.5){totalThreatLevel = 4;}
                        else if(fleetOffenseRating>0.25){totalThreatLevel = 3;}
                        else if(fleetOffenseRating>0.125){totalThreatLevel = 2;}
                        else if(fleetOffenseRating>0.0625){totalThreatLevel = 1;}
                        else if(fleetOffenseRating>0){totalThreatLevel = 0;}
                        if(sApi.GridHasShield(Me.CubeGrid) && fleetShieldStrength!=0){
                            sApi.SetActiveShield(sApi.GetShieldBlock(Me.CubeGrid));
                            if(fleetShieldStrength>=sApi.GetShieldPercent()*sApi.GetMaxHpCap())
                                totalThreatLevel += 1;
                            else
                                totalThreatLevel -= 1;
                        }
                        else if(fleetShieldStrength>0)
                            totalThreatLevel += 1;
                        else if(sApi.GridHasShield(Me.CubeGrid))
                            totalThreatLevel -= 1;
                        if(totalThreatLevel>9)
                            totalThreatLevel = 9;
                        if(totalThreatLevel<0)
                            totalThreatLevel = 0;
                        totalThreatLevel+=1;
                        if(totalThreatLevel>warnThreatLevel&&!alarmPlaying){
                            foreach(IMySoundBlock s in alarms){
                                s.SelectedSound = "Alert 1";
                                s.LoopPeriod = 600;
                                s.Play();
                                alarmPlaying = true;
                            }
                            foreach(IMyTerminalBlock p in aLcds){
                                handleLCD(p,"Danger");
                            }
                            foreach(MyTuple<IMyLightingBlock,Color,bool> tuple in alarmLights){
                                tuple.Item1.Color = Color.Red;
                                tuple.Item1.BlinkLength = 80F;
                                tuple.Item1.BlinkIntervalSeconds = 1;
                                tuple.Item1.Enabled = true;
                            }
                        }else if (totalThreatLevel>warnThreatLevel&&alarmPlaying)
                        {
                            foreach(IMySoundBlock s in alarms){
                                s.Stop();
                                alarmPlaying = false;
                            }
                            foreach(IMyTerminalBlock p in aLcds){
                                handleTextLCD(p,"",Color.White);
                            }
                            foreach(MyTuple<IMyLightingBlock,Color,bool> tuple in alarmLights){
                                tuple.Item1.Color = tuple.Item2;
                                tuple.Item1.BlinkLength = 1;
                                tuple.Item1.BlinkIntervalSeconds = 0;
                                tuple.Item1.Enabled = tuple.Item3;
                            }
                        }
                        if(!musicPlaying){
                            int music = rnd.Next(1,15);
                            if(totalThreatLevel>=7){
                                foreach(IMySoundBlock s in ambient){
                                    s.SelectedSound = "Heavy Fight Music "+ (music<10?"0"+music:""+music);
                                    s.LoopPeriod = 600;
                                    s.Play();
                                    musicPlaying = true;
                                }
                            }else if(totalThreatLevel>=3){
                                foreach(IMySoundBlock s in ambient){
                                    s.SelectedSound = "Light Fight Music "+ (music<10?"0"+music:""+music);
                                    s.LoopPeriod = 600;
                                    s.Play();
                                    musicPlaying = true;
                                }
                            }
                        }
                        if(totalThreatLevel>8)
                            foreach(IMyTerminalBlock p in tLcds){
                                handleTextLCD(p,"Threat Level: Extreme\nEvasive actions recommended",Color.Red);
                            }
                        else if(totalThreatLevel>6)
                            foreach(IMyTerminalBlock p in tLcds){
                                handleTextLCD(p,"Threat Level: High",Color.Orange);
                            }
                        else if(totalThreatLevel>4)
                            foreach(IMyTerminalBlock p in tLcds){
                                handleTextLCD(p,"Threat Level: Moderate",Color.Yellow);
                            }
                        else if(totalThreatLevel>2)
                            foreach(IMyTerminalBlock p in tLcds){
                                handleTextLCD(p,"Threat Level: Low",Color.White);
                            }
                        else
                            foreach(IMyTerminalBlock p in tLcds){
                                handleTextLCD(p,"Threat Level: Minimal",Color.AliceBlue);
                            }
                        Echo("Total Threat: "+(totalThreatLevel));
                    }
                }
                lastEnemyCount = enemyCount;
            }
            else
            {
                Echo("No WeaponCore Weapon Installed!");
            }
        }

        void fetchBlocks(){
            if (!gridsDetected&&!reseted){
                reseted = true;
                alarmPlaying = false;
                foreach(IMySoundBlock s in prox){
                    s.Stop();
                }
                foreach(IMySoundBlock s in alarms){
                    s.Stop();
                    alarmPlaying = false;
                }
                foreach(IMySoundBlock s in ambient){
                    s.Stop();
                    musicPlaying = false;
                }
                foreach(IMyTerminalBlock p in aLcds){
                    handleTextLCD(p,"",Color.White);
                }
                foreach(IMyTerminalBlock p in tLcds){
                    handleTextLCD(p,"No threats in sight",Color.White);
                }
                foreach(MyTuple<IMyLightingBlock,Color,bool> tuple in alarmLights){
                    tuple.Item1.Color = tuple.Item2;
                    tuple.Item1.BlinkLength = 1;
                    tuple.Item1.BlinkIntervalSeconds = 0;
                    tuple.Item1.Enabled = tuple.Item3;
                }
            }
            if(!gridsDetected){
                prox.Clear();
                GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(prox,b=>b.CustomName.Contains(ProximityString));
                alarms.Clear();
                GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(alarms,b=>b.CustomName.Contains(AlarmString));
                ambient.Clear();
                GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(ambient,b=>b.CustomName.Contains(AmbientString));
                aLcds.Clear();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(aLcds,b=>b.CustomName.Contains(ALCDString));
                tLcds.Clear();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(tLcds,b=>b.CustomName.Contains(TLCDString));
                alarmLights.Clear();
                List<IMyLightingBlock> temp = new List<IMyLightingBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(temp,b=>b.CustomName.Contains(ALightsString));
                foreach(IMyLightingBlock l in temp){
                    alarmLights.Add(new MyTuple<IMyLightingBlock,Color,bool>(l,l.Color,l.Enabled));
                }
                reseted = true;
                alarmPlaying = false;
                foreach(IMySoundBlock s in prox){
                    s.Stop();
                }
                foreach(IMySoundBlock s in alarms){
                    s.Stop();
                    alarmPlaying = false;
                }
                foreach(IMySoundBlock s in ambient){
                    s.Stop();
                    musicPlaying = false;
                }
                foreach(IMyTerminalBlock p in aLcds){
                    handleTextLCD(p,"",Color.White);
                }
                foreach(IMyTerminalBlock p in tLcds){
                    handleTextLCD(p,"No threats in sight",Color.White);
                }
                foreach(MyTuple<IMyLightingBlock,Color,bool> tuple in alarmLights){
                    tuple.Item1.Color = tuple.Item2;
                    tuple.Item1.BlinkLength = 1;
                    tuple.Item1.BlinkIntervalSeconds = 0;
                    tuple.Item1.Enabled = tuple.Item3;
                }
            }
            counter = 0;
        }

        public void handleTextLCD(IMyTerminalBlock lcd, string txt,Color fontColor,TextAlignment alignment = TextAlignment.CENTER)
        {
           if(lcd.CustomData!="")
           {
               var pos = int.Parse(lcd.CustomData, System.Globalization.CultureInfo.InvariantCulture);
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(pos);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.ClearImagesFromSelection();
               surf.FontColor = fontColor;
               surf.FontSize = (float)(surf.SurfaceSize.X/surf.SurfaceSize.Y);
               if(surf is IMyTextPanel)
                    surf.FontSize *=0.75f;
               surf.Alignment=alignment;
               surf.WriteText(txt,false);
               frame.Dispose();
           }
           else
           {
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(0);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.ClearImagesFromSelection();
               surf.FontColor = fontColor;
               surf.FontSize = (float)(surf.SurfaceSize.X/surf.SurfaceSize.Y);
               if(surf is IMyTextPanel)
                    surf.FontSize *=0.75f;
               surf.Alignment=alignment;
               surf.WriteText(txt,false);
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

        public class WcPbApi
        {
            private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
            private Func<long, bool> _hasGridAi;

            public bool Activate(IMyTerminalBlock pbBlock)
            {
                var dict = pbBlock.GetProperty("WcPbAPI")?.As<Dictionary<string, Delegate>>().GetValue(pbBlock);
                if (dict == null) throw new Exception($"WcPbAPI failed to activate");
                return ApiAssign(dict);
            }

            public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
            {
                if (delegates == null)
                    return false;
                AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
                AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
                return true;
            }

            private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
            {
                if (delegates == null) {
                    field = null;
                    return;
                }
                Delegate del;
                if (!delegates.TryGetValue(name, out del))
                    throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
                field = del as T;
                if (field == null)
                    throw new Exception(
                        $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
            }
            public void GetSortedThreats(IMyTerminalBlock pbBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
                _getSortedThreats?.Invoke(pbBlock, collection);
            public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
        }

        public class ShieldPbApi
        {
            private IMyTerminalBlock _block;

            private readonly Func<IMyTerminalBlock, float> _getShieldPercent;
            private readonly Func<IMyTerminalBlock, float> _getMaxHpCap;
            // Fields below do not require SetActiveShield to be defined first.
            private readonly Func<IMyCubeGrid, bool> _gridHasShield;
            private readonly Func<IMyEntity, IMyTerminalBlock> _getShieldBlock;
            private readonly Func<IMyTerminalBlock, bool> _isShieldBlock;

            public void SetActiveShield(IMyTerminalBlock block) => _block = block; // AutoSet to TapiFrontend(block) if shield exists on grid.

            public ShieldPbApi(IMyTerminalBlock block)
            {
                _block = block;
                var delegates = _block.GetProperty("DefenseSystemsPbAPI")?.As<Dictionary<string, Delegate>>().GetValue(_block);
                if (delegates == null) return;

                _getShieldPercent = (Func<IMyTerminalBlock, float>)delegates["GetShieldPercent"];
                _getMaxHpCap = (Func<IMyTerminalBlock, float>)delegates["GetMaxHpCap"];
                _gridHasShield = (Func<IMyCubeGrid, bool>)delegates["GridHasShield"];
                _getShieldBlock = (Func<IMyEntity, IMyTerminalBlock>)delegates["GetShieldBlock"];
                _isShieldBlock = (Func<IMyTerminalBlock, bool>)delegates["IsShieldBlock"];

                if (!IsShieldBlock()) _block = GetShieldBlock(_block.CubeGrid) ?? _block;
            }
            public float GetShieldPercent() => _getShieldPercent?.Invoke(_block) ?? -1;
            public float GetMaxHpCap() => _getMaxHpCap?.Invoke(_block) ?? -1;
            public bool GridHasShield(IMyCubeGrid grid) => _gridHasShield?.Invoke(grid) ?? false;
            public IMyTerminalBlock GetShieldBlock(IMyEntity entity) => _getShieldBlock?.Invoke(entity) ?? null;
            public bool IsShieldBlock() => _isShieldBlock?.Invoke(_block) ?? false;
        }
    }
}