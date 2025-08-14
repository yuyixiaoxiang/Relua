-----------------------------------------------
-- [FILE] Core.lua
-- [DATE] 2020-09-12
-- [CODE] BY zengqingfeng
-- [MARK] 简易数据中心基本管理类
-----------------------------------------------
---@class Game
---@field Data Game.Data

---@class Game.Data
---@field GameData fun():GameData
Module "Game.Data" (function(_ENV)
	namespace "Game.Data"
	
	---@class GameData
	class "GameData" (function(_ENV)
		local _hasEnterGame = false;
		for i, name in ipairs(DataNames) do
			property (name) { field = table.concat({"__", name}), type = ClassType, set = false,
							  get = function(self)
								  local __name = table.concat({"__", name})
								  if self[__name] == nil then
									  self[__name] = _ENV[table.concat({name, "Data"})]()
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
			--for i, name in ipairs(DataNames) do
			--	self[table.concat({"__", name})] = _ENV[table.concat({name, "Data"})](); -- 实例化各玩法模块并保存
			--end
			EVENT:AddListener(self, EventDefine.OnEnterGame, self.OnEnterGame)
			return self;
		end
		
		-- 登录游戏 收到第一天初始化数据前必走;
		function OnEnterGame(self)
			_hasEnterGame = true
			for i, name in ipairs(DataNames) do 
				--VhLog("#ConnectGame# OnEnterGame() " , table.concat({"__", name}))
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