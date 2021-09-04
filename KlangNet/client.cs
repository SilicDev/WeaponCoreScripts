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

namespace KlangNet
{
    public class Program : MyGridProgram
    {

        /*----------------- <NO TOUCHEY BELOW HERE!> -----------------*/

        //Script constants
        const string GeneralIniTag = "General Config";
        const string scriptVersion = "1.0.0";
        const string protocolVersion = "1.0.0";
        const string IGC_TAG = "ClangNet";
        const string RGS_TAG = "REGISTER_ME";
        const string URGS_TAG = "UNREGISTER_ME";
        const string CHAT_TAG = "CHAT";
        const string WHIS_TAG = "WHISPER";
        //const string PING_TAG = "PING";
        const string CW_TAG = "CHAT_WARN";
        const string NAME = "name";
        const string CNCT = "connect";
        const string TELL = "tell";
        const string DCNT = "disconnect";
        StringBuilder EchoString = new StringBuilder();

        List<IMyTerminalBlock> lcds = new List<IMyTerminalBlock>();
        string name= "";
        long serverId;
        string chat = "";

        readonly MyCommandLine _commandLine = new MyCommandLine();

        //IGC variables
        IMyUnicastListener uniListener;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            uniListener = IGC.UnicastListener;
            uniListener.SetMessageCallback(CHAT_TAG);
            uniListener.SetMessageCallback(CW_TAG);
            uniListener.SetMessageCallback(WHIS_TAG);
        }

        public void Main(string argument, UpdateType updateType){
            if(updateType==UpdateType.Terminal||updateType==UpdateType.Script||updateType==UpdateType.Trigger){
                if(_commandLine.TryParse(argument))
                    HandleArguments();
            }
            if(updateType==UpdateType.Update100||updateType==UpdateType.Script){
                EchoString.Append("Scriptversion: "+scriptVersion+"\n");
                EchoString.Append("Protocolversion: "+protocolVersion+"\n");
                EchoString.Append("Username: "+name+"\n");
                lcds.Clear();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(lcds, b => b.CustomName.Contains("[chat]")&&b is IMyTextSurfaceProvider);
                if(lcds.Count==0)
                    lcds.Add(Me);
                foreach(IMyTerminalBlock tBlock in lcds){
                    if(chat.Length!=0)
                        handleTextLCD(tBlock,TextHelper.WrapText(chat));
                    else
                        handleTextLCD(tBlock,TextHelper.WrapText("ClangIO"));
                }
                Echo(EchoString.ToString());
                EchoString.Clear();
            }
            if(updateType == UpdateType.IGC){
                while(uniListener.HasPendingMessage){
                    MyIGCMessage message = uniListener.AcceptMessage();
                    object messageData = message.Data;
                    if(message.Tag.Equals(CHAT_TAG)){
                        if(messageData is string){
                            var data = (string)messageData;
                            chat = chat + data;
                            serverId = message.Source;
                        }
                    }
                    if(message.Tag.Equals(WHIS_TAG)){
                        if(messageData is string){
                            var data = (string)messageData;
                            chat = chat + data;
                            serverId = message.Source;
                        }
                    }
                    if(message.Tag.Equals(CW_TAG)){
                        if(messageData is string){
                            var data = (string)messageData;
                            chat = chat + data;
                        }
                    }
                }
            }
        }

        void HandleArguments()
        {
            int argCount = _commandLine.ArgumentCount;

            if (argCount == 0){
                EchoString.Append("No arguments entered\n");
                return;
            }

            switch (_commandLine.Argument(0).ToLowerInvariant())
            {
                case NAME:{
                    if(argCount != 2){
                        EchoString.Append("Wrong amount of arguments entered: Needed 2 Found "+argCount+"\n");
                        return;
                    }
                    if(!_commandLine.Argument(1).Equals("")){
                        if(!_commandLine.Argument(1).Contains(":")||!_commandLine.Argument(1).Contains("<")||!_commandLine.Argument(1).Contains(">")||!_commandLine.Argument(1).Contains("[")
                            ||!_commandLine.Argument(1).Contains("]"))
                                name = _commandLine.Argument(1);
                    }else
                        EchoString.Append("You need to enter an username. Try again");
                    return;
                }
                case CNCT:{
                    if(!name.Equals("")){
                        IGC.SendBroadcastMessage(RGS_TAG,new MyTuple<string,long,long,string>(protocolVersion,Me.CubeGrid.EntityId,Me.OwnerId,name));
                    }else
                        EchoString.Append("You need to set your username first using the command name <username>");
                    return;
                }
                case DCNT:{
                    IGC.SendUnicastMessage(serverId,URGS_TAG,new MyTuple<long,long,string>(Me.CubeGrid.EntityId,Me.OwnerId,name));
                    return;
                }
                case TELL:{
                    if(argCount < 3){
                        EchoString.Append("Wrong amount of arguments entered: Needed 3 Found "+argCount+"\n");
                        return;
                    }
                    if(!name.Equals("")){
                        if(!_commandLine.Argument(1).Equals("")){
                            StringBuilder msg = new StringBuilder();
                            for(int i = 2;i<argCount;i++)
                                msg.Append(_commandLine.Argument(i));
                            IGC.SendBroadcastMessage(WHIS_TAG,new MyTuple<long,string,string,string>(Me.CubeGrid.EntityId,_commandLine.Argument(1),name,msg.ToString()));
                        }else
                            EchoString.Append("You need to enter an adressee. Try again");
                    }else
                        EchoString.Append("You need to set your username first using the command name <username>");
                    return;
                }
                default:{
                    StringBuilder msg = new StringBuilder();
                    for(int i = 0;i<argCount;i++)
                         msg.Append(_commandLine.Argument(i)+" ");
                    IGC.SendUnicastMessage(serverId,CHAT_TAG,new MyTuple<long,string,string>(Me.CubeGrid.EntityId,name,msg.ToString()));
                    return;
                }
            }
        }

        #region Whip's TextHelper Class v2
        public class TextHelper
        {
            static StringBuilder textSB = new StringBuilder();
            const float adjustedPixelWidth = (512f / 0.778378367f);
            const int monospaceCharWidth = 24 + 1; //accounting for spacer
            const int spaceWidth = 8;

            #region bigass dictionary
            static Dictionary<char, int> _charWidths = new Dictionary<char, int>()
        {
        {'.', 9},
        {'!', 8},
        {'?', 18},
        {',', 9},
        {':', 9},
        {';', 9},
        {'"', 10},
        {'\'', 6},
        {'+', 18},
        {'-', 10},

        {'(', 9},
        {')', 9},
        {'[', 9},
        {']', 9},
        {'{', 9},
        {'}', 9},

        {'\\', 12},
        {'/', 14},
        {'_', 15},
        {'|', 6},

        {'~', 18},
        {'<', 18},
        {'>', 18},
        {'=', 18},

        {'0', 19},
        {'1', 9},
        {'2', 19},
        {'3', 17},
        {'4', 19},
        {'5', 19},
        {'6', 19},
        {'7', 16},
        {'8', 19},
        {'9', 19},

        {'A', 21},
        {'B', 21},
        {'C', 19},
        {'D', 21},
        {'E', 18},
        {'F', 17},
        {'G', 20},
        {'H', 20},
        {'I', 8},
        {'J', 16},
        {'K', 17},
        {'L', 15},
        {'M', 26},
        {'N', 21},
        {'O', 21},
        {'P', 20},
        {'Q', 21},
        {'R', 21},
        {'S', 21},
        {'T', 17},
        {'U', 20},
        {'V', 20},
        {'W', 31},
        {'X', 19},
        {'Y', 20},
        {'Z', 19},

        {'a', 17},
        {'b', 17},
        {'c', 16},
        {'d', 17},
        {'e', 17},
        {'f', 9},
        {'g', 17},
        {'h', 17},
        {'i', 8},
        {'j', 8},
        {'k', 17},
        {'l', 8},
        {'m', 27},
        {'n', 17},
        {'o', 17},
        {'p', 17},
        {'q', 17},
        {'r', 10},
        {'s', 17},
        {'t', 9},
        {'u', 17},
        {'v', 15},
        {'w', 27},
        {'x', 15},
        {'y', 17},
        {'z', 16}
        };
            #endregion

            public static int GetWordWidth(string word)
            {
                int wordWidth = 0;
                foreach (char c in word)
                {
                    int thisWidth = 0;
                    bool contains = _charWidths.TryGetValue(c, out thisWidth);
                    if (!contains)
                        thisWidth = monospaceCharWidth; //conservative estimate

                    wordWidth += (thisWidth + 1);
                }
                return wordWidth;
            }

            public static string WrapText(string text, float fontSize=16, float pixelWidth = adjustedPixelWidth)
            {
                textSB.Clear();
                var words = text.Split(' ');
                var screenWidth = (pixelWidth / fontSize);
                int currentLineWidth = 0;
                foreach (var word in words)
                {
                    if (currentLineWidth == 0)
                    {
                        textSB.Append($"{word}");
                        currentLineWidth += GetWordWidth(word);
                        continue;
                    }

                    currentLineWidth += spaceWidth + GetWordWidth(word);
                    if (currentLineWidth > screenWidth) //new line
                    {
                        currentLineWidth = GetWordWidth(word);
                        textSB.Append($"\n{word}");
                    }
                    else
                    {
                        textSB.Append($" {word}");
                    }
                }

                return textSB.ToString();
            }
        }
        #endregion

        public void handleTextLCD(IMyTerminalBlock lcd, string txt)
        {
           if(lcd.CustomData!="")
           {
               var pos = int.Parse(lcd.CustomData, System.Globalization.CultureInfo.InvariantCulture);
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(pos);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.ClearImagesFromSelection();
               surf.WriteText(txt,false);
               frame.Dispose();
           }
           else
           {
               IMyTextSurface surf = (lcd as IMyTextSurfaceProvider).GetSurface(0);
               surf.ContentType = ContentType.TEXT_AND_IMAGE;
               MySpriteDrawFrame frame = surf.DrawFrame();
               surf.ClearImagesFromSelection();
               surf.WriteText(txt,false);
               frame.Dispose();
           }
        }
    }
}