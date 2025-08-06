using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lua.AST;

namespace Lua
{
    public static class Program
    {
        
        public static int Main(string[] args)
        {
            ProcessAllPloopClass();
            return default;
            var testluafile = @"f:/lua2ts/infile.lua";
            var tokenizer = new Tokenizer(File.ReadAllText(testluafile));
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            expr.ToString();
            var context = new CheckContext();
            expr.CheckNode(context,null);
            expr.ExportableVariables(context);
            Console.WriteLine($"{context}");
            
            var outpath = "f:/lua2ts/outputfile.lua";
            File.WriteAllText(outpath, expr.ToString());
            Console.WriteLine(outpath);
            return default;
        }
        
        private static List<string> filterPaths = new List<string>()
        {
            "GamePlay/GameModule/Login/LoginModule",
            "GameModule/NetModule/NetStateModule",
            "GameModule/NetModule/ReconnectNetModul",
            "GameModule/ResendMsg/ResendMsgModule",
            "GameModule/Payment/PaymentModule",
            "GameModule/MainModule",
           
            
            "GameModule/Map/State",
            //枚举
            "GameData/EnumData",
            
            //内城
            "GameModule/ModuleDefine.lua",
            "GameModule/HomeScene/HomeSceneModule",
            "GameModule/HomeScene/CityFogModule",
            "GameModule/SelfCity",
            
            
            //外城
            "GameModule/WorldMapModule",
            "MapViewPortChangedHandler",
            "MapCityObjectComponent",
            "MapObjectComponent",
            "MapNpcObjectComponent",
            "MapArmyObjectComponent",
            "GameModule/Map/MapUnit",
            "GameModule/Map/Log/MapLevel0DisplayDataProvider",
            "GameModule/Map/MapUtil",
            "Lua/GameModule/Army",
            "GameModule/Map/CityUnit",
            
            "GameModule/NPC",
            "GameModule/Map/Lod/MapLevel0DisplayDataProvider",
            
            "GameModule/Battle",
            "GameModule/EntityMenu",
            
            
            //data
            "GameData/Login/LoginData",
            "GameData/UserData",
            "GameData/ConfEnumData",
            "GameData/EnumData",
            "DServerData/DServerData",
            "GameData/Map/WorldMapData",
            
            
            "GameNet/EnterGamePb",
            "GameNet/Common",
            "GameNet/Resource",
            "GameNet/Recharge",
            
            "Lua/GameData/Map/",
            "Lua/GameData/Battle",
            
            "GameData/EntityMenu",
            "GameData/EntityButtonData",
            
            "GameData/Item",
            
            
            //common
            "Lua/Common/LuaObjectN",
            "Lua/Common/UI/CommonContainerN",
            "GameView/ViewBase/ViewNodeN",
            "GameView/ViewBase/ViewBaseN",
            "Common/Util/StateMachineN",
            
            
            //condition 
            "Common/Logic/Condition/core/",
            "Common/Logic/Condition/type/",
            "CommonExt/Logic/Condition/mod",
            
            //cfg condition
            "Common/Logic/CfgCondition/core/CfgCondition",
            "CommonExt/Logic/CfgCondition/CfgConditionBind",
            "Common/Logic/CfgCondition/core/ConditionBase",
            "CommonExt/Logic/CfgCondition/mod",
            
            //check 
            "Common/Logic/Check",
            "CommonExt/Logic/Check",
            
            //action 
            "Common/Logic/Action/",
            "CommonExt/Logic/Action/mod",
            
            //trigger / watcher
            "Common/Logic/Trigger/core/Watcher",
            "CommonExt/Logic/Trigger",
            "Common/Logic/Trigger/TriggerSystem_Bind",
            "Common/Logic/Trigger/core/Trigger",
            "Common/Logic/Trigger/core/TriggerSystem",
            
            
            //ui-----------
            "GameView/Login/login_panel",
            "GameView/Login/serverlist_panel",
            "GameView/Login/server_item",
        };

        private static List<string> fullcopyfiles = new List<string>()
        {
            "Procedure.lua",
            // "GameModule.lua",
            // "GameData.lua",
            // "Core.lua",
            "Game.lua",
            // "GameView.lua",
            
        };
        
        private static List<string> postcopyfiles = new List<string>()
        {
             "EntityMenuModule.lua",
             "CfgConditionBind.lua",
             "LuaObjectN.lua"
        };
            
        
        private static void ProcessAllPloopClass()
        {
            var topLuaDir =Const.topLuaDir;
            Console.WriteLine(topLuaDir);
            var scanLuaFiles = PloopScanner.ScanLuaFiles(topLuaDir);
            var errCnt = 0;
            var processor = new Processor();
            foreach (var file in scanLuaFiles)
            { 
                var srcPath = file.FilePath;
                var requirePath =Path.GetRelativePath(topLuaDir,srcPath).Replace(Path.GetExtension(srcPath),"").Replace("\\","/");
                var outpath = srcPath.Insert(srcPath.LastIndexOf(".lua", StringComparison.Ordinal), "");
                if (fullcopyfiles.Exists(s => Path.GetFileName(outpath) == s))
                {
                    Console.WriteLine($"full copy file: {outpath}");
                    var copyfile =Path.Combine(GetProjectDirectory(),"fullcopylua",Path.GetFileName(outpath));
                    var content = File.ReadAllText(copyfile);
                    File.WriteAllText(outpath, content);
                    continue;
                }

                processor.AddFile(srcPath, requirePath,outpath);
            }

            var outfiles = processor.Process();
            // return;
            foreach (var outfile in outfiles)
            {
                // if (filterPaths.Exists((s => outfile.path.Replace("\\", "/").Contains(s))) == false)
                // {
                //     Console.WriteLine($"skip process file: {outfile.path}");
                //     continue;
                // }

                
                if (File.Exists(outfile.path))
                    File.Delete(outfile.path);
                
                if (postcopyfiles.Exists(s => Path.GetFileName(outfile.path) == s))
                {
                    Console.WriteLine($"post copy file: {Path.GetFileName(outfile.path)}");
                    var copyfile =Path.Combine(GetProjectDirectory(),"postcopylua",Path.GetFileName(outfile.path));
                    var content = File.ReadAllText(copyfile);
                    File.WriteAllText(outfile.path, content);
                }
                else
                {
                    //CUSTOM GameView
                    var content = outfile.content;
                    
                    if (outfile.path.EndsWith("GameView.lua"))
                    {
                        content = content.Replace("Enum.GetEnumValues", "pairs");
                        content = content.Replace("local viewClass = _ENV[name]", "local viewClass = _ENV[name]()");
                    }

                    if (outfile.path.EndsWith("msg_panel.lua"))
                    {
                        content = content.Insert(0,"""
                                                    local _ENV = {
                                                       msg_entity_buff_item = require("GameView/Msg/mod/msg_entity_buff_item"),
                                                       msg_entity_chat_item = require("GameView/Msg/mod/msg_entity_chat_item"),
                                                       msg_entity_damage_item = require("GameView/Msg/mod/msg_entity_damage_item"),
                                                       msg_entity_info_item = require("GameView/Msg/mod/msg_entity_info_item"),
                                                       msg_entity_pop_item = require("GameView/Msg/mod/msg_entity_pop_item"),
                                                       msg_entity_skill_damage_item = require("GameView/Msg/mod/msg_entity_skill_damage_item"),
                                                       msg_notice_item = require("GameView/Msg/mod/msg_notice_item"),
                                                       msg_pop_critical_item = require("GameView/Msg/mod/msg_pop_critical_item"),
                                                       msg_pop_item = require("GameView/Msg/mod/msg_pop_item"),
                                                       msg_reward_item = require("GameView/Msg/mod/msg_reward_item"),
                                                       msg_rolling_item = require("GameView/Msg/mod/msg_rolling_item"),
                                                       msg_voice_over_item = require("GameView/Msg/mod/msg_voice_over_item"),
                                                   }
                                                   
                                                   """);
                    }

                    if (outfile.path.EndsWith("main_panel_new_menu.lua"))
                    {
                        content = content.Insert(0,"""
                                                    local _ENV = {
                                                       main_left_menu_item = require("GameView/Main_panel_new/main_left_menu_item"),
                                                       main_normal_item = require("GameView/Main_panel_new/main_normal_item"),
                                                       msg_entity_damage_item = require("GameView/Main_panel_new/main_right_menu_item"),
                                                   }

                                                   """);
                    }

                    if (outfile.path.EndsWith("BuffAttributeModule.lua"))
                    {
                        content = content.Replace("GetStartIndex(sourceType[1])", "BuffAttributeModule.GetStartIndex(sourceType[1])");
                    }




                    Console.WriteLine($"rewriting {outfile.path}");
                    File.WriteAllText(outfile.path, content);    
                }
            }
            //CUSTOM 
            var luainit =Path.Combine(GetProjectDirectory(),"misc","luainit.lua");
            var luainitcontent = File.ReadAllText(luainit);
            File.WriteAllText(Path.Combine(Const.topLuaDir,"init.lua"), luainitcontent);
            
            luainit =Path.Combine(GetProjectDirectory(),"misc","shopinit.lua");
            luainitcontent = File.ReadAllText(luainit);
            File.WriteAllText(Path.Combine(Const.topLuaDir,"GameModule/Shop","init.lua"), luainitcontent);
        }
        
        private static bool HasProjectFile(string directory)
        {
            return Directory.GetFiles(directory, "*.sln").Length > 0;
        }
        
        public static string GetProjectDirectory([CallerFilePath]string callerFilePath = "")
        {
            // 从调用此方法的源文件路径推断项目目录
            string directory = Path.GetDirectoryName(callerFilePath);
        
            // 向上查找到项目根目录（包含 .csproj 文件的目录）
            while (directory != null && !HasProjectFile(directory))
            {
                directory = Directory.GetParent(directory)?.FullName;
            }
        
            return directory;
        }
    }
}