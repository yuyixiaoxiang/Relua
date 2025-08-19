-----------------------------------------------
-- [FILE] LoginData.lua
-- [DATE] 2020-09-12
-- [CODE] BY zengqingfeng
-- [MARK] 登录相关数据
-----------------------------------------------
---@class Game
---@field Module Game.Module

---@class Game.Module
---@field GameModule fun():GameModule
Module "Game.Module" (function(_ENV)
	namespace "Game.Module"
	import "Game.Data"
	-- 玩法逻辑模块只能访问数据模块不能直接访问界面（界面需要高度解耦独立）


	---@class GameModule
	class "GameModule" (function(_ENV)
	--	property "System" { field = "__System", type = ClassType, set = false }
	--	property "Message" { field = "__Message", type = ClassType, set = false }
		local _hasEnterGame = false;
		-- 自动生成各玩法模块
		for i, name in ipairs(ModuleNames) do 
			property (name) { field = table.concat({"__", name}), type = ClassType, set = false,
							  get = function(self)
								local __name = table.concat({"__", name}) 
								if self[__name] == nil then
									require(_LAZY_REQUIRE[table.concat({name, "Module"})])
									self[__name] = _ENV[table.concat({name, "Module"})]()
									if _hasEnterGame then
										safe.callFunc(self[__name], "OnEnterGame");
									end
								end	
								return self[__name]
							  end 
			}
		end 
		
		function __ctor(self)
		end
		
		function Init(self)
			
			-- 手动生成
			--		self.__System = SystemModule();
			--		self.__Message = MessageModule();
			-- 自动初始化各模块
			for i, name in ipairs(ModuleNames) do
				self[table.concat({"__", name})] = _ENV[table.concat({name, "Module"})](); -- 实例化各玩法模块并保存 
			end
			EVENT:AddListener(self, EventDefine.OnEnterGame, self.OnEnterGame)
			return self;
		end

		-- 登录游戏 收到第一天初始化数据前必走;
		function OnEnterGame(self)
			_hasEnterGame = true
			for i, name in ipairs(ModuleNames) do
				if self[table.concat({"__", name})] then
					safe.callFunc(self[table.concat({"__", name})], "OnEnterGame");	
				end 
			end 
		end

		-- 切账号、返回登录走;
		function OnClearAll(self)
			for i, name in ipairs(ModuleNames) do
				if self[table.concat({"__", name})] then
					safe.callFunc(self[table.concat({"__", name})], "OnClearAll");
				end
			end
		end
	end)
end)
