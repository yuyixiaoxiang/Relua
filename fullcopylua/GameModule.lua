-----------------------------------------------
-- [FILE] LoginData.lua
-- [DATE] 2020-09-12
-- [CODE] BY zengqingfeng
-- [MARK] 登录相关数据
-----------------------------------------------
Module "Game.Module" (function(_ENV)
	--namespace "Game.Module"
	--import "Game.Data"
	-- 玩法逻辑模块只能访问数据模块不能直接访问界面（界面需要高度解耦独立）

	---@class GameModule _auto_annotation_
	class "GameModule" (function(_ENV)
		-- property "System" { field = "__System", type = ClassType, set = false }
		-- property "Message" { field = "__Message", type = ClassType, set = false }
		-- 自动生成各玩法模块
		--for i, name in ipairs(ModuleNames) do 
		-- ---@class GameModule _auto_annotation_
		-- ---@field public __ nil _auto_annotation_
		-- property (name) { field = table.concat({"__", name}), type = ClassType, set = false }
		--end 

		---@class GameModule _auto_annotation_
		---@field public __ctor fun(self:GameModule) _auto_annotation_
		---@param self GameModule _auto_annotation_
		function __ctor(self)
		end

		---@class GameModule _auto_annotation_
		---@field public Init fun(self:GameModule) _auto_annotation_
		---@param self GameModule _auto_annotation_
		function Init(self)
			self.Login = require("Common/GamePlay/GameModule/Login/LoginModule")()
			self.ResendMsg = require("Common/GamePlay/GameModule/ResendMsg/ResendMsgModule")()
			self.NetState =  require("Common/GamePlay/GameModule/NetModule/NetStateModule")()
			self.Payment = require("CommonExt/GamePlay/GameModule/Payment/PaymentModule")()
			self.ReconnectNet = require("Common/GamePlay/GameModule/NetModule/ReconnectNetModule")()
			self.Main = require("GameModule/MainModule")()
			-- self.WorldMap = require("GameModule/WorldMapModule")()
			self.HomeScene = require("GameModule/HomeScene/HomeSceneModule")()
			self.WorldMap = require("GameModule/WorldMapModule")()
			self.Army = require("GameModule/Army/ArmyModule")()
			self.NPC = require("GameModule/NPC/NPCModule")()
			self.CreateArmy = require("GameModule/Army/CreateArmyModule")()
			self.CityFog = require("GameModule/HomeScene/CityFogModule")()
			self.SelfCity = require("GameModule/SelfCity/SelfCityModule")()
			self.Battle = require("GameModule/Battle/BattleModule")()

			
			self.EntityMenu = require("Common/GamePlay/GameModule/EntityMenu/EntityMenuModule")()
			-- 手动生成
			--    self.__System = SystemModule();
			--    self.__Message = MessageModule();
			-- 自动初始化各模块
			for i, name in ipairs(ModuleNames) do
				if self[name] == nil then
					print('gamemodule: ' .. name)
					self[name] = _ENV[table.concat({name, "Module"})](); -- 实例化各玩法模块并保存
				end
			end
			EVENT:AddListener(self, EventDefine.OnEnterGame, self.OnEnterGame)
			return self;
		end

		-- 登录游戏或者断线重连触发构造
		---@class GameModule _auto_annotation_
		---@field public OnEnterGame fun(self:GameModule) _auto_annotation_
		---@param self GameModule _auto_annotation_
		function OnEnterGame(self)
			for i, name in ipairs(ModuleNames) do
				safe.callFunc(self[name], "OnEnterGame");
			end
		end
	end)
end)