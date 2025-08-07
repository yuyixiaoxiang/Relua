
local __EventsCache = {}

function AddClassListener(_, hoster, target, callback, eventName_)
    do return end
    eventName_ = eventName_ or "OnDataChanged"
	local targetDic = __GetTargetList(hoster, target)
    if targetDic[eventName_] then
        logError("AddClassListener can't add twice!!")
        return
    end
    targetDic[eventName_] = function(...)
        callback(hoster, ...)
    end
    if target[eventName_] then
        target[eventName_] = (target[eventName_] + targetDic[eventName_])
    else
        target[eventName_] = targetDic[eventName_]
    end
end

function RemoveClassListener(_, hoster, target, eventName_)
    do return end
    eventName_ = eventName_ or "OnDataChanged"
	local targetDic = __GetTargetList(hoster, target)
    if targetDic[eventName_] then
        target[eventName_] = (target[eventName_] - targetDic[eventName_])
        targetDic[eventName_] = nil
    end
end
function ClearClassListener(_, hoster)
    do return end
    if (not __EventsCache[hoster]) then
        return
    end
    __EventsCache[hoster]:Each(__EachHosterDic, hoster)
    __EventsCache[hoster] = nil
end
function __EachHosterDic(target, targetDic, hoster)
    targetDic:Each(__EachTargetDic, hoster, target)
end
function __EachTargetDic(eventName, callback, hoster, target)
    RemoveClassListener(EVENT, hoster, target, eventName)
end
function __GetTargetList(hoster, target)
    local hosterDic = __EventsCache[hoster]
    if (not hosterDic) then
        hosterDic = {}
        __EventsCache[hoster] = hosterDic
    end
    local targetDic = hosterDic[target]
    if (not targetDic) then
        targetDic = {}
        hosterDic[target] = targetDic
    end
    return targetDic
end
EVENT.AddClassListener = AddClassListener
EVENT.RemoveClassListener = RemoveClassListener
EVENT.ClearClassListener = ClearClassListener
