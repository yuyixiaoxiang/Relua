
local function default_table(name)
    return function(self)
        self[name] = {}
        return self[name]
    end
end
--local LuaObjectN = require("Common/LuaObjectN")
---@class LuaObjectN
local LuaObjectN = middleclass('LuaObjectN') 


function LuaObjectN:__index(key)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2) 
    local existField = true --rawget(self,fieldName) ~= nil
    local getFunc = rawget(self.class.__instanceDict,'__Get'..key)
    if existField and getFunc ~= nil then
        return getFunc(self)
    end
    return nil
end
            

function LuaObjectN:__newindex(key,value)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2)
    local existField = true-- rawget(self,fieldName) ~= nil
    local setFunc = rawget(self.class.__instanceDict,'__Set'..key)
    if existField and setFunc ~= nil then
        setFunc(self,value)
    else
        rawset(self,key,value)
    end
end
            

function LuaObjectN:__SetComp(value)
    self.__comp = value
end

function LuaObjectN:__GetComp()
    if (self.__comp == nil) then
        self.__comp = default_table("__comp")(self)
    end
    return self.__comp
end

function LuaObjectN:__SetInfo(value)
    if value then
        table.clear(self.__info)
        self.__info = table.clone(value, self.__info)
    end
end

function LuaObjectN:__GetInfo()
    if (self.__info == nil) then
        self.__info = default_table("__info")
    end
    return self.__info
end

function LuaObjectN:__SetId(value)
    self.__id = value
end

function LuaObjectN:__GetId()
    if (self.__id == nil) then
        self.__id = -1
    end
    return self.__id
end

function LuaObjectN:__GetName()
    if (self.__name == nil) then
        self.__name = "unknown"
    end
    return self.__name
end
--PLoop Property:Name
--PLoop Property:Id
--PLoop Property:Info
--PLoop Property:Comp

function LuaObjectN:__ctor(id, name)
    self.__id = (id or (-1))
    self.__name = (name or "unknown")
    self.Comp = self:__GetComp()
end

function LuaObjectN:__dtor()
    EVENT:ClearClassListener(self)
    EVENT:ClearObjAllEvent(self)
    CHECK:ClearUINode(self)
    if self.__comp then
        for _, v in pairs(self.__comp) do
            safe.callFunc(v, "Dispose")
        end
    end
end

function LuaObjectN:SetInfoDefault(default_value)
    if (not self.__info) then
        self.__info = {}
    end
    setmetatable(self.__info, { __index = default_value })
end



return LuaObjectN