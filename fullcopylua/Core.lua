-----------------------------------------------
-- [FILE] Net.lua
-- [DATE] 2020-09-12
-- [CODE] BY zengqingfeng
-- [MARK] 网络协议
-----------------------------------------------
---
Module "Game.Net" (function(_ENV)
	--namespace "Game.Net"
	--import "Game.Data"
	--import "Game.Module"

	class "GameNet" (function(_ENV)
		-- 自动生成各玩法模块
		--for i, name in ipairs(NetNames) do
		--	---@class GameNet _auto_annotation_
		--	---@field public __ nil _auto_annotation_
		--	property (name) { field = table.concat({"__", name}), type = ClassType, set = false }
		--end

		---@class GameNet _auto_annotation_
		---@field public __ctor fun(self) _auto_annotation_
		---@param self GameNet _auto_annotation_
		function __ctor(self)
			self.EnterGamePb = require("GameNet/EnterGamePb")()
			self.Common = require("GameNet/Common")()
			self.Resource = require("GameNet/Resource")()
			self.Recharge = require("GameNet/Recharge")()

			
			-- 自动初始化各模块
			for i, name in ipairs(NetNames) do
				if self[name] == nil then
					self[name] = _ENV[name](); -- 实例化各玩法模块并保存	
				end 
			end
		end
	end)
end)
