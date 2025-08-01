
--local EntityMenuModule = require("Common/GamePlay/GameModule/EntityMenu/EntityMenuModule")
---@class EntityMenuModule
local EntityMenuModule = middleclass('EntityMenuModule')
local EntityMenuDetail = require("Common/GamePlay/GameModule/EntityMenu/EntityMenuDetail")

function EntityMenuModule:__index(key)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2) 
    local existField = true --rawget(self,fieldName) ~= nil
    local getFunc = rawget(self.class.__instanceDict,'__Get'..key)
    if existField and getFunc ~= nil then
        return getFunc(self)
    end
    return nil
end
            

function EntityMenuModule:__newindex(key,value)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2)
    local existField = true-- rawget(self,fieldName) ~= nil
    local setFunc = rawget(self.class.__instanceDict,'__Set'..key)
    if existField and setFunc ~= nil then
        setFunc(self,value)
    else
        rawset(self,key,value)
    end
end
            
local __entityMenuCfgMap
local __entityMenuCfgListMap
local __hudConfigMap

function EntityMenuModule:__GetHudConfigId()
    if (self.__hudConfigId == nil) then
        self.__hudConfigId = "defaultHudConfig"
    end
    return self.__hudConfigId
end

function EntityMenuModule:__SetMenuUIFactory(value)
    self.__menuUIFactory = value
end

function EntityMenuModule:__GetMenuUIFactory()
    return self.__menuUIFactory
end

function EntityMenuModule:__GetButtonsCache()
    return self.__buttonsCache
end

function EntityMenuModule:__GetMenuCache()
    return self.__menuCache
end

function EntityMenuModule:__GetMenuDict()
    return self.__menuDict
end

function EntityMenuModule:__SetMenuElement(value)
    self.__menuElement = value
end

function EntityMenuModule:__GetMenuElement()
    if (self.__menuElement == nil) then
        self.__menuElement = nil
    end
    return self.__menuElement
end
--PLoop Property:MenuElement
--PLoop Property:MenuDict
--PLoop Property:MenuCache
--PLoop Property:ButtonsCache
--PLoop Property:MenuUIFactory
--PLoop Property:HudConfigId

function EntityMenuModule:__ctor()
    self.__menuDict = {}
    self.__menuCache = {}
    self.__buttonsCache = {}
    self:__InitPriority()
    self:__InitAsync()
end

function EntityMenuModule:SetActive(uId, isActive)
    local menusData = self.__menuDict[uId]
    if menusData then
        menusData:SetActive(isActive)
    end
end

function EntityMenuModule:TrySelect(id, data)
    if (self.__menuElement and (self:GetID(self.__menuElement) == id)) then
        return
    end
    self:HideEntityMenu()
    self:__DealAsyncDataAtOnce(id)
    local menusData = self.__menuDict[id]
    if ((not menusData) and data) then
        self:AddEntityMenu(data, true)
        menusData = self.__menuDict[id]
    end
    if menusData then
        self.__menuElement = menusData.Data
        menusData:DoClick(true)
    end
    EVENT:BrocastNow(EventDefine.SHOW_ENTITY_MENU)
end

function EntityMenuModule:HideEntityMenu()
    if (not self.__menuElement) then
        EVENT:BrocastNow(EventDefine.HIDE_ENTITY_MENU)
        return
    end
    local id = self:GetID(self.__menuElement)
    self.__menuElement = nil
    local menusData = self.__menuDict[id]
    if menusData then
        menusData:HideMenu()
    end
    EVENT:BrocastNow(EventDefine.HIDE_ENTITY_MENU)
    EVENT:BrocastNow(EventDefine.InnerCityDeSelect)
end

function EntityMenuModule:IsSelectTarget(id)
    if (self.__menuElement and (self.__menuElement.Id == id)) then
        return true
    end
    return false
end

function EntityMenuModule:RemoveAllMenus()
    for _, v in pairs(self.__menuDict) do
        v:Recycle()
        table.insert(self.__menuCache, v)
    end
    self.__menuDict = {}
    self.__priorityList = {}
    self.__menuElement = nil
    self.__enabledMenuCnt = 0
    self.__menuCnt = 0
    self:__RemoveAllAsyncData()
end

function EntityMenuModule:RemoveEntityMenu(id)
    if self:__RemoveAsyncData(id) then
        return
    end
    if (not self.__menuDict[id]) then
        return
    end
    local entity_menu = self.__menuDict[id]
    entity_menu:Recycle()
    self.__menuDict[id] = nil
    self:__RemovePriorityList(entity_menu)
    table.insert(self.__menuCache, entity_menu)
end

function EntityMenuModule:GetID(data)
    return ((data and data.Id) or nil)
end

function EntityMenuModule:GetMenuCfg(menuKey)
    if string.isNullOrEmpty(menuKey) then
        return
    end
    if (not __entityMenuCfgMap) then
        local entityMenuCfgList = GameConfigManager:GetConfEntityMenuList()
        if (not entityMenuCfgList) then
            return
        end
        __entityMenuCfgMap = table.toHash(entityMenuCfgList.list, "key")
    end
    return __entityMenuCfgMap[menuKey]
end

function EntityMenuModule:GetMenuList(menuKey)
    local cfg = self:GetMenuCfg(menuKey)
    if (not cfg) then
        return
    end
    if (not __entityMenuCfgListMap) then
        __entityMenuCfgListMap = {}
    end
    local menuCfgList = __entityMenuCfgListMap[menuKey]
    if (not menuCfgList) then
        menuCfgList = {}
        __entityMenuCfgListMap[menuKey] = menuCfgList
        for _, v in ipairs(cfg.group) do
            local localCfg = GameConfigManager:GetConfEntityMenuItem(v)
            if localCfg then
                table.insert(menuCfgList, localCfg)
            end
        end
    end
    return menuCfgList, cfg
end

function EntityMenuModule:RentButton(logic_lua)
    local list = self.__buttonsCache[logic_lua]
    local button
    if ((not list) or ((#list) == 0)) then
        local __creator
        if logic_lua == 'EntityButtonData' then
            __creator = require("Common/GamePlay/GameData/EntityMenu/EntityButtonData")
        elseif logic_lua == 'AllianceHelpButtonData'  then
            __creator = require("GameData/EntityButtonData/AllianceHelpButtonData").AllianceHelpButtonData
        elseif  logic_lua == 'AllianceHelpOtherButtonData' then
            __creator = require("GameData/EntityButtonData/AllianceHelpButtonData").AllianceHelpOtherButtonData
        else
            __creator = require("GameData/EntityButtonData/"..logic_lua)
        end 
        
        --local __creator = _ENV[logic_lua]
        if __creator then
            print('RentButton '..logic_lua)
            button = __creator()
        else
            logError(string.concat("entity_menu.xlsx -> entity_menu_item -> logic_lua ", logic_lua, " is not existed!"))
        end
    else
        button = table.remove(list)
    end
    return button
end

function EntityMenuModule:RecycleButton(logic_lua, button)
    if (not button) then
        return
    end
    button:Recycle()
    local list = self.__buttonsCache[logic_lua]
    if (not list) then
        list = {}
        self.__buttonsCache[logic_lua] = list
    end
    table.insert(list, button)
end

function EntityMenuModule:SetHudConfigId(key)
    if ((not key) or (type(key) ~= "string")) then
        return
    end
    self.__hudConfigId = key
end

function EntityMenuModule:SetEntityMenuKey(data)
    if ((not data) or (not data.menuKey)) then
        return
    end
    if (not __hudConfigMap) then
        local hudCfg = GameConfigManager:GetConfEntityMenuConfigList()
        if (not hudCfg) then
            return
        end
        __hudConfigMap = table.toHash(hudCfg.list, "hudConfigKey")
    end
    if string.isNullOrEmpty(data.menuKey) then
        logErrorEx("menuKey is isNullOrEmpty")
    end
    if __hudConfigMap[data.menuKey] then
        local value = __hudConfigMap[data.menuKey][self.HudConfigId]
        if string.isNullOrEmpty(value) then
            value = __hudConfigMap[data.menuKey].defaultHudConfig
        end
        data.menuKey = value
    end
end

function EntityMenuModule:AddEntityMenu(data, autoRemove_, force)
    if (not data) then
        --return
    else
        self:SetEntityMenuKey(data)
    end
    local id = self:GetID(data)
    if (not id) then
        return
    end
    self:__SetPriority(data)
    if self.__menuDict[id] then
        self.__menuDict[id]:RefreshData(data)
        return
    end
    if (((not force) and (not autoRemove_)) and (self:__GetShowMenuCnt() >= self:__GetAsyncStartCnt())) then
        self:__AddAsyncData(data)
        return
    end
    local menu_list, cfg = self:GetMenuList(data.menuKey)
    if (not cfg) then
        return
    end
    local enabled = true
    if ((self:__GetShowMenuCnt() >= self.MaxShowMenuCnt) and (not self:__CancelMenuEnabled(data.Priority))) then
        enabled = false
    end
    local entity_menu = self:__RentEntityMenu()
    entity_menu:InitData(data, cfg, menu_list, (autoRemove_ or false), enabled)
    self.__menuDict[id] = entity_menu
    self:__AddPriorityList(entity_menu)
end

function EntityMenuModule:__RemoveEntityMenu(_, data)
    local id = self:GetID(data)
    if (not id) then
        return
    end
    self:RemoveEntityMenu(id)
end

function EntityMenuModule:__RentEntityMenu()
    local data
    local len = (#self.__menuCache)
    if (len > 0) then
        data = table.remove(self.__menuCache)
    else
        data = EntityMenuDetail()
    end
    return data
end

function EntityMenuModule:AddCityBuilding(data)
    if ((not data) or (data.IsDefence and data:IsDefence())) then
        return
    end
    data.menuKey = self:GetCityBMenuKey(data.GroupId)
    data.menuName = tostring(data.GroupId)
    self:AddEntityMenu(data)
end

function EntityMenuModule:GetCityBMenuKey(groupId)
    local menuKey = "city_building_common"
    local cfg = GameConfigManager:GetConfBuildingGroup(groupId)
    if (cfg and (not string.isNullOrEmpty(cfg.buildingHudConfigId))) then
        menuKey = cfg.buildingHudConfigId
    end
    return menuKey
end

function EntityMenuModule:AddUnlockEvent(data)
    if (not data) then
        return
    end
    local menuInfo = {}
    menuInfo.Id = data.UId
    menuInfo.menuKey = self:GetUnlockEventMenuKey(data)
    menuInfo.EntityId = data.EntityId
    menuInfo.ConfId = data.ConfId
    menuInfo.menuName = "unlock"
    self:AddEntityMenu(menuInfo)
end

function EntityMenuModule:GetUnlockEventMenuKey(data)
    local menuKeyCommon = "city_building_unlock"
    return menuKeyCommon
end

function EntityMenuModule:AddRepairedBMenu(data)
    local menuInfo = {}
    menuInfo.Id = data.UId
    menuInfo.EntityId = data.EntityId
    menuInfo.ConfId = data.ConfId
    menuInfo.menuName = tostring(CityNodeType.REPAIRED_BUILDING)
    menuInfo.menuKey = "city_building_repair"
    self:AddEntityMenu(menuInfo)
end

function EntityMenuModule:SetAssistFlag(id, flag, add)
    if (not id) then
        return
    end
    local menuDetail = self.__menuDict[id]
    if menuDetail then
        menuDetail:SetAssistFlag(flag, add)
    end
end

---------auto include sub partial class--------------------
package.loaded["Common/GamePlay/GameModule/EntityMenu/EntityMenuModule"] = EntityMenuModule
if(not package.loaded["Common/GamePlay/GameModule/EntityMenu/EntityMenuModule_Async"]) then
	require("Common/GamePlay/GameModule/EntityMenu/EntityMenuModule_Async")  
end
if(not package.loaded["Common/GamePlay/GameModule/EntityMenu/EntityMenuModule_Priority"]) then
	require("Common/GamePlay/GameModule/EntityMenu/EntityMenuModule_Priority")  
end
---------auto include sub partial class--------------------



return EntityMenuModule