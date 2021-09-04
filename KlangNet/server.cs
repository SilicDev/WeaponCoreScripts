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
        //Script configs
        string serverName = "Server";
        string BAN_MSG = "You've been banned on this server";
        string RPRT_MSG = "You've been reported before on this server";
        string RPRT_TAG = "[reported]";
        string TELL_TAG = "[tell]";
        string ADMN_TAG = "[admin]";
        int HistoryLength = 5;

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
        const string empty = "";
        const string reset = "reset";
        const string unbanId = "unbanid";
        const string unbanOwner = "unbanowner";
        const string banUser = "ban";
        const string banDisplay = "display_banned";
        Queue<string> chatHistory = new Queue<string>();
        StringBuilder EchoString = new StringBuilder();

        //Script variables
        List<MyTuple<long,long,string>> registeredUsers = new List<MyTuple<long,long,string>>();
        List<string> registeredNames = new List<string>();
        List<MyTuple<long,string>> registeredIds = new List<MyTuple<long,string>>();
        List<long> reportedGrids = new List<long>();
        List<long> bannedGrids = new List<long>();
        List<long> bannedOwners = new List<long>();
        List<MyTuple<long,long>> bannedUsers = new List<MyTuple<long,long>>();
        List<MyTuple<long,long>> unbannedUsers = new List<MyTuple<long,long>>();
        int RGS = 0;
        List<IMyTerminalBlock> debugLcds = new List<IMyTerminalBlock>();

        readonly MyCommandLine _commandLine = new MyCommandLine();

        //IGC variables
        IMyBroadcastListener bdctListener;
        IMyUnicastListener uniListener;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            ParseStorage();
            bdctListener = IGC.RegisterBroadcastListener(RGS_TAG);
            bdctListener.SetMessageCallback(RGS_TAG);
            uniListener = IGC.UnicastListener;
            uniListener.SetMessageCallback(URGS_TAG);
            uniListener.SetMessageCallback(CHAT_TAG);
            uniListener.SetMessageCallback(WHIS_TAG);
            //uniListener.SetMessageCallback(PING_TAG);
        }

        public void Save()
        {
            StringBuilder saveData = new StringBuilder();
            foreach(string message in chatHistory){
                saveData.Insert(0,message,1).Insert(0,"<>",1);
            }
            saveData.Append("<<<>>>");
            foreach(MyTuple<long,long,string> tuple in registeredUsers){
                saveData.Append(tuple.Item1).Append("<>").Append(tuple.Item2).Append("<>").Append(tuple.Item3).Append("<>").Append("<<>>");
            }
            saveData.Append("<<<>>>");
            foreach(MyTuple<long,string> tuple in registeredIds){
                saveData.Append(tuple.Item1).Append("<>").Append(tuple.Item2).Append("<<>>");
            }
            saveData.Append("<<<>>>");
            foreach(MyTuple<long,long> tuple in bannedUsers){
                saveData.Append(tuple.Item1).Append("<>").Append(tuple.Item2).Append("<<>>");
            }
            Storage = saveData.ToString();
        }

        public void ParseStorage(){
            string[] mainParts = Storage.Split("<<<>>>".ToCharArray(),StringSplitOptions.None);
            if(mainParts.Length == 4){
                string[] subParts = mainParts[0].Split("<>".ToCharArray(),StringSplitOptions.None);
                foreach(string msg in subParts){
                    chatHistory.Enqueue(msg);
                }
                subParts = mainParts[1].Split("<<>>".ToCharArray(),StringSplitOptions.None);
                foreach(string users in subParts){
                    string[] subSubParts = users.Split("<>".ToCharArray(),StringSplitOptions.None);
                    try{
                        registeredUsers.Add(new MyTuple<long,long,string>(long.Parse(subSubParts[0]),long.Parse(subSubParts[1]),subSubParts[2]));
                        registeredNames.Add(subSubParts[2]);
                    }catch { EchoString.Append("Couldn't parse Storage!");return;}
                }
                subParts = mainParts[2].Split("<<>>".ToCharArray(),StringSplitOptions.None);
                foreach(string ids in subParts){
                    string[] subSubParts = ids.Split("<>".ToCharArray(),StringSplitOptions.None);
                    try{
                        registeredIds.Add(new MyTuple<long,string>(long.Parse(subSubParts[0]),subSubParts[1]));
                    }catch { EchoString.Append("Couldn't parse Storage!");return;}
                }
                subParts = mainParts[3].Split("<<>>".ToCharArray(),StringSplitOptions.None);
                foreach(string users in subParts){
                    string[] subSubParts = users.Split("<>".ToCharArray(),StringSplitOptions.None);
                    try{
                        bannedUsers.Add(new MyTuple<long,long>(long.Parse(subSubParts[0]),long.Parse(subSubParts[1])));
                    }catch { EchoString.Append("Couldn't parse Storage!");return;}
                }
            }
        }

        public void Main(string argument, UpdateType updateType)
        {
            if(updateType==UpdateType.Terminal||updateType==UpdateType.Script||updateType==UpdateType.Trigger){
                if(_commandLine.TryParse(argument))
                    HandleArguments();
            }
            if(updateType == UpdateType.Update100){
                EchoString.Clear();
                EchoString.Append("Scriptversion: "+scriptVersion+"\n");
                EchoString.Append("Protocolversion: "+protocolVersion+"\n");
                debugLcds.Clear();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(debugLcds, b => b.CustomName.Contains("[server-debug]")&&b is IMyTextSurfaceProvider);
                if(debugLcds.Count==0)
                    debugLcds.Add(Me);
                StringBuilder text =  new StringBuilder();
                foreach(string msg in chatHistory)
                    text.Insert(0,msg,1);
                foreach(IMyTerminalBlock tBlock in debugLcds){
                    if(text.Length!=0)
                        handleTextLCD(tBlock,TextHelper.WrapText(text.ToString()));
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
                    if(message.Tag.Equals(URGS_TAG)){
                        if(messageData is MyTuple<long,long,string>){
                            var data = (MyTuple<long,long,string>)messageData;
                            registeredIds.Remove(new MyTuple<long,string>(message.Source,data.Item3));
                            registeredUsers.Remove(data);
                            registeredNames.Remove(data.Item3);
                        }
                    }
                    if(message.Tag.Equals(CHAT_TAG)){
                        if(messageData is MyTuple<long,string,string>){
                            var data = (MyTuple<long,string,string>)messageData;
                            string text = "";
                            if(reportedGrids.Contains(data.Item1))
                                text =data.Item2+RPRT_TAG+":"+data.Item3;
                            else
                                text = data.Item2+":"+data.Item3;
                            chatHistory.Enqueue(text);
                            while(chatHistory.Count > HistoryLength)
                                chatHistory.Dequeue();
                            foreach(MyTuple<long,string> tuple in registeredIds){
                                IGC.SendUnicastMessage(tuple.Item1,CHAT_TAG,TextHelper.WrapText(text));
                            }
                        }
                    }
                    if(message.Tag.Equals(WHIS_TAG)){
                        if(messageData is MyTuple<long,string,string,string>){
                            var data = (MyTuple<long,string,string,string>)messageData;
                            string msg = "";
                            if(reportedGrids.Contains(data.Item1))
                                msg = data.Item3+RPRT_TAG+TELL_TAG+":"+data.Item4;
                            else
                                msg = data.Item3+TELL_TAG+":"+data.Item4;
                            foreach(MyTuple<long,string> tuple in registeredIds){
                                if(tuple.Item2.Equals(data.Item2))
                                    IGC.SendUnicastMessage(tuple.Item1,WHIS_TAG,TextHelper.WrapText(msg.ToString()));
                            }
                        }
                    }
                }
                while(bdctListener.HasPendingMessage)
                {
                    MyIGCMessage message = bdctListener.AcceptMessage();
                    object messageData = message.Data;
                    if(message.Tag.Equals(RGS_TAG)){
                        if(messageData is MyTuple<string,long,long,string>){
                            var data = (MyTuple<string,long,long,string>)messageData;
                            if(bannedGrids.Contains(data.Item2)){
                                IGC.SendUnicastMessage(message.Source,CW_TAG,serverName+": "+BAN_MSG+"\n");
                                bannedOwners.Add(data.Item3);
                                continue;
                            }
                            if(bannedOwners.Contains(data.Item3)&&data.Item3!=Me.OwnerId){
                                IGC.SendUnicastMessage(message.Source,CW_TAG,serverName+": "+BAN_MSG+"\n");
                                bannedGrids.Add(data.Item2);
                                continue;
                            }
                            if(reportedGrids.Contains(data.Item2)){
                                IGC.SendUnicastMessage(message.Source,CW_TAG,serverName+": "+RPRT_MSG+"\n");
                            }
                            if(data.Item1.Equals(protocolVersion)&&!registeredNames.Contains(data.Item4)&&!registeredIds.Contains(new MyTuple<long,string>(message.Source,data.Item4))){
                                registeredIds.Add(new MyTuple<long,string>(message.Source,data.Item4));
                                registeredUsers.Add(new MyTuple<long,long,string>(data.Item2,data.Item3,data.Item4));
                                registeredNames.Add(data.Item4);
                                StringBuilder text =  new StringBuilder();
                                foreach(string msg in chatHistory)
                                    text.Insert(0,msg,1);
                                IGC.SendUnicastMessage(message.Source,CHAT_TAG,text.ToString());
                            }else if(!data.Item1.Equals(protocolVersion))
                                IGC.SendUnicastMessage(message.Source,CW_TAG,serverName+": You are using an outdated version of this script\n");
                            else if(registeredNames.Contains(data.Item4))
                                IGC.SendUnicastMessage(message.Source,CW_TAG,serverName+": Your desired username is already in use\n");
                            else if(registeredIds.Contains(new MyTuple<long,string>(message.Source,data.Item4)))
                                IGC.SendUnicastMessage(message.Source,CW_TAG,serverName+": Your PB is already registered\n");
                        }
                    }
                }
            }
        }

        void HandleArguments()
        {
            int argCount = _commandLine.ArgumentCount;

            if (argCount == 0)
                return;

            switch (_commandLine.Argument(0).ToLowerInvariant())
            {
                case reset:{
                    if (argCount != 1)
                    {
                        EchoString.Append("Wrong amount of arguments entered: Needed 1 Found "+argCount+"\n");
                        return;
                    }
                    chatHistory.Clear();
                    registeredUsers.Clear();
                    registeredNames.Clear();
                    registeredIds.Clear();
                    reportedGrids.Clear();
                    bannedGrids.Clear();
                    bannedOwners.Clear();
                    bannedUsers.Clear();
                    return;
                }
                case unbanId:{
                    if(argCount != 2){
                        EchoString.Append("Wrong amount of arguments entered: Needed 2 Found "+argCount+"\n");
                        return;
                    }
                    if(bannedGrids.Remove(long.Parse(_commandLine.Argument(1)))){
                        EchoString.Append("Successfully unbaned id  : "+_commandLine.Argument(1));
                        return;
                    }
                    EchoString.Append("Couldn't unban id : "+_commandLine.Argument(1));
                        return;
                }
                case unbanOwner:{
                    if(argCount != 2){
                        EchoString.Append("Wrong amount of arguments entered: Needed 2 Found "+argCount+"\n");
                        return;
                    }
                    if(bannedOwners.Remove(long.Parse(_commandLine.Argument(1)))){
                        foreach(MyTuple<long,long> tuple in bannedUsers){
                            if(tuple.Item2 == long.Parse(_commandLine.Argument(1))){
                                bannedGrids.Remove(tuple.Item1);
                                unbannedUsers.Add(tuple);
                            }
                        }
                        foreach(MyTuple<long,long> tuple in bannedUsers)
                            bannedUsers.Remove(tuple);
                        unbannedUsers.Clear();
                        EchoString.Append("Successfully unbaned owner : " +_commandLine.Argument(1)+"\n");
                        return;
                    }
                    EchoString.Append("Couldn't unban owner : "+_commandLine.Argument(1)+"\n");
                    return;
                }
                case banUser:{
                    if(argCount != 2){
                        EchoString.Append("Wrong amount of arguments entered: Needed 2 Found "+argCount+"\n");
                        return;
                    }
                    if(registeredNames.Contains(_commandLine.Argument(1))){
                        foreach(MyTuple<long,long,string> tuple in registeredUsers){
                            if(tuple.Item3.Equals(_commandLine.Argument(1))){
                                bannedGrids.Add(tuple.Item1);
                                bannedOwners.Add(tuple.Item2);
                                bannedUsers.Add(new MyTuple<long,long>(tuple.Item1,tuple.Item2));
                                registeredNames.Remove(tuple.Item3);
                            }
                        }
                        EchoString.Append("Successfully baned user : "+_commandLine.Argument(1)+"\n");
                        return;
                    }
                    EchoString.Append("Couldn't ban user : "+_commandLine.Argument(1)+"\n");
                    return;
                }
                case banDisplay:{
                    if(argCount != 1){
                        EchoString.Append("Wrong amount of arguments entered: Needed 1 Found "+argCount+"\n");
                        return;
                    }
                    EchoString.Append("\nBanned Owners:\n");
                    foreach(long l in bannedOwners)
                        EchoString.Append(l +"\n");
                    EchoString.Append("\nBanned Grids:\n");
                    foreach(long l in bannedGrids)
                        EchoString.Append(l +"\n");
                    return;
                }
                default:
                    return;
            }
        }

        #region Script Logging
        public static class Log
        {
            static StringBuilder _builder = new StringBuilder();
            static List<string> _errorList = new List<string>();
            static List<string> _warningList = new List<string>();
            static List<string> _infoList = new List<string>();
            const int _logWidth = 530; //chars, conservative estimate

            public static void Clear()
            {
                _builder.Clear();
                _errorList.Clear();
                _warningList.Clear();
                _infoList.Clear();
            }

            public static void Error(string text)
            {
                _errorList.Add(text);
            }

            public static void Warning(string text)
            {
                _warningList.Add(text);
            }

            public static void Info(string text)
            {
                _infoList.Add(text);
            }

            public static string Write(bool preserveLog = false)
            {
                //WriteLine($"Error count: {_errorList.Count}");
                //WriteLine($"Warning count: {_warningList.Count}");
                //WriteLine($"Info count: {_infoList.Count}");

                if (_errorList.Count != 0 && _warningList.Count != 0 && _infoList.Count != 0)
                    WriteLine("");

                if (_errorList.Count != 0)
                {
                    for (int i = 0; i < _errorList.Count; i++)
                    {
                        WriteLine("");
                        WriteElememt(i + 1, "ERROR", _errorList[i]);
                        //if (i < _errorList.Count - 1)
                    }
                }

                if (_warningList.Count != 0)
                {
                    for (int i = 0; i < _warningList.Count; i++)
                    {
                        WriteLine("");
                        WriteElememt(i + 1, "WARNING", _warningList[i]);
                        //if (i < _warningList.Count - 1)
                    }
                }

                if (_infoList.Count != 0)
                {
                    for (int i = 0; i < _infoList.Count; i++)
                    {
                        WriteLine("");
                        WriteElememt(i + 1, "Info", _infoList[i]);
                        //if (i < _infoList.Count - 1)
                    }
                }

                string output = _builder.ToString();

                if (!preserveLog)
                    Clear();

                return output;
            }

            private static void WriteElememt(int index, string header, string content)
            {
                WriteLine($"{header} {index}:");

                string wrappedContent = TextHelper.WrapText(content, 1, _logWidth);
                string[] wrappedSplit = wrappedContent.Split('\n');

                foreach (var line in wrappedSplit)
                {
                    _builder.Append("  ").Append(line).Append('\n');
                }
            }

            private static void WriteLine(string text)
            {
                _builder.Append(text).Append('\n');
            }
        }

        // Whip's TextHelper Class v2
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

            public static string WrapText(string text, float fontSize = 16, float pixelWidth = adjustedPixelWidth)
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
    }
}