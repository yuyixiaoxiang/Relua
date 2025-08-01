-----------------------------------------------
-- [FILE] Core.lua
-- [DATE] 2020-09-12
-- [CODE] BY zengqingfeng
-- [MARK] 简易数据中心基本管理类
-----------------------------------------------
Module "Game.Data" (function(_ENV)
	--namespace "Game.Data"
	
	---@class GameData _auto_annotation_
	class "GameData" (function(_ENV)
		--for i, name in ipairs(DataNames) do
		--	---@class GameData _auto_annotation_
		--	---@field public __ nil _auto_annotation_
		--	property (name) { field = table.concat({"__", name}), type = ClassType, set = false }
		--end
		
		---@class GameData _auto_annotation_
		---@field public __ctor fun(self:GameData) _auto_annotation_
		---@param self GameData _auto_annotation_
		function __ctor(self)
		end
		
		---@class GameData _auto_annotation_
		---@field public Init fun(self:GameData) _auto_annotation_
		---@param self GameData _auto_annotation_
		function Init(self)
			self.Login = require("Common/GamePlay/GameData/Login/LoginData")()
			self.User = require("GameData/UserData")()
			self.DServer = require("GameData/DServerData/DServerData")()

			self.WorldMap = require("GameData/Map/WorldMapData")()
			self.Battle = require("GameData/Battle/BattleData")()

			self.Item = require("GameData/Item/ItemData")()
			self.Goods = require("GameData/Item/GoodsData")()
			
			for i, name in ipairs(DataNames) do
				if self[name] == nil then
					print('gamedata: ' .. name)
					self[name] = _ENV[table.concat({name, "Data"})](); -- 实例化各玩法模块并保存
				end
			end
			EVENT:AddListener(self, EventDefine.OnEnterGame, self.OnEnterGame)
			return self;
		end
		
		-- 登录游戏或者断线重连触发事件，需要清理上一个的玩家数据
		---@class GameData _auto_annotation_
		---@field public OnEnterGame fun(self:GameData) _auto_annotation_
		---@param self GameData _auto_annotation_
		function OnEnterGame(self)
			for i, name in ipairs(DataNames) do 
				--VhLog("#ConnectGame# OnEnterGame() " , table.concat({"__", name}))
				safe.callFunc(self[name], "OnEnterGame");
			end 
		end
	end)
end)
