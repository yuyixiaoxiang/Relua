
ConditionType = {
    CITY_COLLECTION_COUNT = "1001",
    RESOURCE_POINT_COLLECTION_COUNT = "1002",
    RESOURCE_POINT_COLLECTION_TIMES = "1003",
    CITY_COLLECTION_TIMES = "1004",
    BUILDING_GROUP_LEVEL = "2001",
    HAVE_BUILDING_COUNT = "2002",
    CREATE_BUILDING = "2003",
    BUILDING_UPGRADE_TIMES = "2004",
    ARMY_BUILDING_LEVEL = "2005",
    RESOURCE_BUILDING_LEVEL = "2006",
    RESTORE_BUILDING_TYPE = "2007",
    RESTORE_BUILDING_ANY = "2008",
    SCIENCE_GROUP_LEVEL = "3001",
    SCIENCE_UPGRADE_COUNT = "3002",
    KILL_MONSTER_LEVEL_MORE_THAN = "4001",
    PERSONAL_KILL_MONSTER = "4002",
    ALLIANCE_PERSONAL_KILL_MONSTER = "4003",
    CREATE_ARMY_COUNT = "5001",
    HAVE_ARMY_COUNT = "5002",
    HAVE_HERO_COUNT = "5003",
    HERO_SKILL_UPGRADE_TIMES = "5004",
    HERO_STAR_UPGRADE_TIMES = "5005",
    HERO_STAR_MORE_THAN = "5006",
    CURE_ARMY_COUNT = "5007",
    CURE_ARMY_TIMES = "5008",
    LOTTERY_TIMES = "5009",
    HERO_SKILL_LEVEL = "5010",
    HERO_QUALITY = "5011",
    WOUNDED_ARMY = "5012",
    AREA_UNLOCK_ID = "6001",
    AREA_UNLOCK_COUNT = "6002",
    AREA_UNLOCK_TIMES = "6003",
    DAILY_BOX_TIMES = "6004",
    ALLIANCE_HELP_TIMES = "6006",
    AREA_UNLOCK_EVENT_ID = "6007",
    WORLD_TROOP_COUNT = "7001",
    USE_ITEM_ID = "8001",
    LOGIN_DAY = "8002",
    SPEED_TIME = "8003",
    COMMANDER_ACTIVITY_POINT = "8004",
    PAYMENT_COUNT = "8006",
    WEEKLY_CARD = "8007",
    MONTHLY_CARD = "8008",
    PLAYER_FIGHT_POWER = "9001",
    PLAYER_VIP_MIN_LEVEL = "9002",
    PLAYER_VIP_MAX_LEVEL = "9003",
    PLAYER_MIN_LEVEL = "9004",
    PLAYER_MAX_LEVEL = "9005",
    REGISTER_AFTER_DAY = "9006",
    REGISTER_BEFORE_DAY = "9007",
    SERVER_OPEN_DAY = "9008",
    TASK_COMPLETE = "9009",
    MAIN_BUILDING_MIN_LEVEL = "9010",
    MAIN_BUILDING_MAX_LEVEL = "9011",
    PVE_LEVEL_STAR = "9012",
    PVE_LEVEL_PASS = "9013",
    FUNCTION_ID_UNLOCK = "9014",
    MILESTONE_NODE_END = "9015",
    SERVER_OPEN_TYPE = "9016",
    KVK_MATCH_SCENE = "9017",
    KVK_DATA_SOCKET = "9018",
    All_PERSONAL_KILL_MONSTER = "20001",
    ALLIANCE_KILL_MONSTER = "20002",
    All_PERSONAL_KILL_RALLY_MONSTER = "20003",
    ALLIANCE_KILL_RALLY_MONSTER = "20004",
    All_ALLIANCE_BUILDING_COUNT = "20010",
    ALLIANCE_BUILDING_COUNT = "20011",
    ALLIANCE_HAVE_BUILDING_COUNT = "20012",
    SERVER_HAVE_BUILDING_COUNT = "20013",
    All_PERSONAL_CITY_BUILDING_COUNT = "20020",
    All_ALLIANCE_MEMBER_COUNT = "20030",
    ALLIANCE_MEMBER_COUNT = "20031",
    ALLIANCE_FIGHT_POWER_RANK = "20032",
    PVE_CHAPTER_COMPLETE = "13002"
}

setmetatable(ConditionType, {
    __call = function(self, value)
        for key, val in pairs(self) do
            if val == value then
                return key
            end
        end
        return ""
    end
})
            

local __activeConditionClass = {
    CityCollectionCount,
    ResourcePointCollectionCount,
    ResourcePointCollectionTimes,
    CityCollectionTime,
    BuildingGroupLevel,
    HaveBuildingCount,
    CreateBuilding,
    BuildingUpgradeTimes,
    ArmyBuildingLevel,
    ResourceBuildingLevel,
    RestoreBuildingType,
    RestoreBuildingAny,
    ScienceGroupLevel,
    ScienceUpgradeCount,
    KillMonsterLevelMoreThan,
    PersonalKillMonster,
    AlliancePersonalKillMonster,
    CreateArmyCount,
    HaveArmyCount,
    HaveHeroCount,
    HeroSkillUpgradeTimes,
    HeroStarUpgradeTimes,
    HeroStarMoreThan,
    CureArmyCount,
    CureArmyTimes,
    LotteryTimes,
    HeroSkillLevel,
    HeroQuality,
    WoundedArmy,
    AreaUnlockId,
    AreaUnlockCount,
    AreaUnlockTimes,
    DailyBoxTimes,
    AllianceHelpTimes,
    AreaUnlockEventId,
    WorldTroopCount,
    UseItemId,
    LoginDay,
    SpeedItem,
    CommanderActivityPoint,
    PaymentCount,
    WeeklyCard,
    MonthlyCard,
    PlayerFightPower,
    PlayerVipMinLevel,
    PlayerVipMaxLevel,
    PlayerMinLevel,
    PlayerMaxLevel,
    RegisterAfterDay,
    RegisterBeforeDay,
    ServerOpenDay,
    TaskComplete,
    MainBuildingMinLevel,
    MainBuildingMaxLevel,
    PveLevelPass,
    PveLevelStar,
    FunctionIdUnlock,
    MilestoneNodeEnd,
    ServerOpenType,
    KVKMatchScene,
    KVKDataSocket,
    AllPersonalKillMonster,
    AllianceKillMonster,
    AllPersonalKillRallyMonster,
    AllianceKillRallyMonster,
    AllAllianceBuildingCount,
    AllianceBuildingCount,
    AllianceHaveBuildingCount,
    ServerHaveBuildingCount,
    AllPersonalCityBuildingCount,
    AllAllianceMemberCount,
    AllianceMemberCount,
    AllianceFightPowerRank,
    PveChapterComplete
}
--local CfgCondition = require("CommonExt/Logic/CfgCondition/CfgConditionBind")
local LuaObjectN = require("Common/LuaObjectN")
---@class CfgCondition : LuaObjectN
local CfgCondition = middleclass('CfgCondition', LuaObjectN) 


function CfgCondition:__index(key)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2) 
    local existField = true --rawget(self,fieldName) ~= nil
    local getFunc = rawget(self.class.__instanceDict,'__Get'..key)
    if existField and getFunc ~= nil then
        return getFunc(self)
    end
    return nil
end
            

function CfgCondition:__newindex(key,value)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2)
    local existField = true-- rawget(self,fieldName) ~= nil
    local setFunc = rawget(self.class.__instanceDict,'__Set'..key)
    if existField and setFunc ~= nil then
        setFunc(self,value)
    else
        rawset(self,key,value)
    end
end
            

function CfgCondition:__ctor()
    self.__Condition_type_class = {}
    local value
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionArmyHero")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionBuilding")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionCity")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionCollection")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionLimit")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionMonster")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionNonPersonal")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionOther")) do
        table.insert(__activeConditionClass, value)
    end
    for key, value in pairs(require("CommonExt/Logic/CfgCondition/mod/ConditionScience")) do
        table.insert(__activeConditionClass, value)
    end
    table.insert(__activeConditionClass, require("CommonExt/Logic/CfgCondition/mod/ConditionPve"))
    table.insert(__activeConditionClass, require("CommonExt/Logic/CfgCondition/mod/ConditionWorld"))
    for index, classValue in pairs(__activeConditionClass) do
        value = classValue()
        if (value or (value.ConditionType ~= "BaseType")) then
            self.__Condition_type_class[value.ConditionType] = value
        else
            logError(("create conditionClass error!! name:" .. classValue))
        end
    end
end

---------auto include sub partial class--------------------
package.loaded["CommonExt/Logic/CfgCondition/CfgConditionBind"] = CfgCondition
if(not package.loaded["Common/Logic/CfgCondition/core/CfgCondition"]) then
	require("Common/Logic/CfgCondition/core/CfgCondition")  
end
---------auto include sub partial class--------------------



return CfgCondition