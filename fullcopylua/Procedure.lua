-----------------------------------------------
-- [FILE] procedure.lua
-- [DATE] 2021-03-15
-- [CODE] BY zqf
-- [MARK] NONE
-----------------------------------------------

Module "Game.Procedure"(function(_ENV)
    --import "Common.StateMachine"
    --import "Game.Module"

    -- 启动状态初始化Lua
    ---@class LaunchState _auto_annotation_
    class "LaunchState"(function(_ENV)
        inherit "IState"
        ---@class LaunchState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "LaunchState", set = false }

        ---@class LaunchState _auto_annotation_
        ---@field public OnEnter fun(self:LaunchState, context) _auto_annotation_
        ---@param self LaunchState _auto_annotation_
        function OnEnter(self, context)
            VhLog(" #LaunchFlow#  LaunchState:OnEnter() ")
            -- 初始化bin文件;
            NetPBCManager:Init(); -- 初始化协议交互模块
            ---@type GameData
            _G["DATA"] = GameData(); -- 初始化玩法数据缓存模块
            DATA:Init();
            ---@type GameModule
            _G["MODULE"] = GameModule(); -- 初始化玩法逻辑
            MODULE:Init();
            ---@type GameView
            _G["VIEW"] = GameView(); -- 初始化界面模块
            ---@type GameNet  目前NET.xxx不生效
            _G["NET"] = GameNet(); -- 初始化网络模块
            NetPBCManager:InitEnd();
            ---@type ActionSystem
            _G["ACTION"] = ActionSystem(); -- 客户端独立事件处理系统
            ---@type ConditionSystem
            _G["CONDITION"] = ConditionSystem(); -- 客户端独立条件判断系统
            ---@type WatcherSystem
            _G["WATCHER"] = WatcherSystem(); -- 客户端监听器系统
            ---@type TriggerSystem
            _G["TRIGGER"] = TriggerSystem(); -- 客户端触发器系统
            ---@type CfgCondition
            _G["CFG_CONDITION"] = CfgCondition(); -- 基于服务器规则的条件判断系统
            ---@type CheckSystem
            _G["CHECK"] = CheckSystem(); -- 客户端红点系统
            if UNITY_EDITOR then
                --print(table2string(_G, "全局", 1));
            end

            -- 初始化语言设置
            local lang = PlayerPrefsLua.GetString("LanguageSetting", "empty");
            if lang == "empty" then
                Localization:SetLanguage();
            else
                Localization:SetLanguage(lang);
            end

            MODULE.Login:Test();
            MODULE.Login:ReinitialLoginModule()
            VIEW:OpenUI("msg_panel");
            VIEW:OpenUI("fly_effect_panel");
            UIMisc:PlayBgSound("Initializer")
            --context:SetState(ProcedureState.LoginState);

            EVENT:AddListener(self, EventDefine.OnInitEnd, function()
                self:__OnInitEnd(context);
            end); -- 等待c#异步初始化Lua完成
        end

        ---@class LaunchState _auto_annotation_
        ---@field public OnLeave fun(self:LaunchState, context) _auto_annotation_
        ---@param self LaunchState _auto_annotation_
        function OnLeave(self, context)
            EVENT:RemoveListener(self, EventDefine.OnInitEnd); -- EVENT:ClearObjAllEvent(self); -- 可以清理玩所有事件
        end

        ---@class LaunchState _auto_annotation_
        ---@field public OnEvent fun(self:LaunchState, context, event, params) _auto_annotation_
        ---@param self LaunchState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.GotoCity then
                context:SetState(ProcedureState.CityState, params);
            elseif event == ProcedureEvent.GotoWorld then
                context:SetState(ProcedureState.MapState);
            elseif event == ProcedureEvent.GotoBattle then
                context:SetState(ProcedureState.BattleState, params);
            end
        end

        -- 第一次进入登录界面（返回登录不会再进来了）
        ---@class LaunchState _auto_annotation_
        ---@field public __OnInitEnd fun(self:LaunchState, context) _auto_annotation_
        ---@param self LaunchState _auto_annotation_
        function __OnInitEnd(self, context)
            VhLog(" #LaunchFlow#  __OnInitEnd() ")
            -- 播放背景音乐
            UIMisc:PlayBgSound('Control_gameStart')
            -- 打开常驻界面
            VIEW:OpenUI("msg_panel"); -- 消息提示界面
            VIEW:OpenUI("fly_effect_panel"); -- 特效图标飞行表现界面
            VIEW:OpenUI('entity_menu_panel') -- 实体菜单界面
            VIEW:OpenUI('guide_view_panel') -- 引导界面
            VIEW:OpenUI('gauss_blur_panel') -- 高斯模糊界面
            VIEW:OpenUI("top_menu_panel"); -- 最高常驻UI杂项界面

            LaunchState.BeforeAllEnterGameInfoReceived()
            self:LaunchLogin()
            --context:SetState(ProcedureState.LoginState); -- 登录状态不需要切换了，只有返回登录才切换到登录状态
        end

        function LaunchLogin(self)
            EVENT:AddListener(self, EventDefine.EventAllInfoReceive, self.__OnInitEnd2);
            EVENT:AddListener(self, EventDefine.AccountMsgFailForLogin, self.OnAccountMsgFailForLogin);
            EVENT:AddListener(self, EventDefine.EnterGameMsgFailForLogin, self.OnEnterGameMsgFailForLogin);
            EVENT:AddListener(self, EventDefine.ConnectGameServerFailForLogin, self.OnConnectGameServerFail)

            if ELEX_SDK and not UNITY_EDITOR then
                MODULE.Login:Login()
            else
                MODULE.Notice:RequestNotice();--请求公告 请求到了就会显示
                VIEW:OpenUI("login_panel")
            end
        end

        function ClearLoginFlowState(self)
            EVENT:RemoveListener(self, EventDefine.EventAllInfoReceive);
            EVENT:RemoveListener(self, EventDefine.AccountMsgFailForLogin);
            EVENT:RemoveListener(self, EventDefine.EnterGameMsgFailForLogin);
            EVENT:RemoveListener(self, EventDefine.ConnectGameServerFailForLogin)
        end

        ---EnterGame 消息之后执行的初始化
        function __OnInitEnd2(self, eventId, data)
            self:ClearLoginFlowState()
            VhLog("#ConnectGame#  Procedure.LaunchState状态.OnInitEnd2() , 参数data ==>", data)
            LaunchState.AfterAllEnterGameInfoReceived(data)
        end

        function OnConnectGameServerFail(self, eventId)
            self:ClearLoginFlowState()
            VhLog("#状态机# socket 连接失败 ")
            MODULE.Login:SetAccountLoginState(MODULE.Login.ACCOUNT_LOGIN_STATE.Disconnected)
            VIEW:OpenUIEx("confirm_panel", 6, {
                title = 'tips',
                content = string.get("launch_key1"), --多语言: Launch状态机,登录中,socket连接失败
                cancel = false,
                confirm = string.get('launch_key2'), --重试
                cbConfirm = function()
                    self:LaunchLogin()
                end,
                UI_BTN_CLOSE = false,
                UI_BG_CLOSE = false,
            })
        end

        ---AccountMsg 发送失败
        function OnAccountMsgFailForLogin(self)
            self:ClearLoginFlowState()
            VhLog("#状态机# 处理 登录消息失败")
            VIEW:OpenUIEx("confirm_panel", 6, {
                title = 'tips',
                content = string.get("launch_key3"), --多语言: Launch状态机,登录中,处理 AccLogin消息失败
                cancel = false,
                confirm = string.get('launch_key4'), --返回登录
                cbConfirm = function()
                    self:LaunchLogin()
                end,
                UI_BTN_CLOSE = false,
                UI_BG_CLOSE = false,
            })
        end

        function OnEnterGameMsgFailForLogin(self)
            self:ClearLoginFlowState()
            VhLog("#状态机# 处理 EnterGame消息失败")
            VIEW:OpenUIEx("confirm_panel", 6, {
                title = 'tips',
                content = string.get("launch_key5"), --多语言: Launch状态机,登录中,处理 EnterGame消息失败
                cancel = false,
                confirm = string.get('launch_key2'), --重试
                cbConfirm = function()
                    self:LaunchLogin()
                end,
                UI_BTN_CLOSE = false,
                UI_BG_CLOSE = false,
            })
        end

        -- 进入游戏 procedure 状态机 功能 ===========================================================================================================================


        -- 全量消息处理之前
        __Static__()
        function BeforeAllEnterGameInfoReceived()

            DATA.DServer:ClearAllDSrvData() -- 不能放入OnEnterGame，会修改DATA.变量引用
            GAME.OnEnterGame();
        end

        -- 全量消息处理之后
        __Static__()
        function AfterAllEnterGameInfoReceived(data)

            --isShowSubTab是否显示二级页面1：显示，0：不显示 
            --isHorizontalLayout1：横版，0：竖版 
            --isBackButtonLeftBottom返回按钮位置，0顶部，1 底部
            local gameConfig = "{\"isShowSubTab\": 0,\"isHorizontalLayout\": 1,\"isBackButtonLeftBottom\": 0}"
            MODULE.Chat:InitSDKV2(gameConfig)

            local _mailCityLv = MODULE.SelfCity:GetMainCityLevel()
            DATA.Mail:InitMail(data.__serverId, ServiceManagerLua.MailServerType, data.player_guid, data.userData.allianceId, data.userData.allianceRank, _mailCityLv, data.userData.playerLevel);

            MODULE.Login:SDK_EnterGame(data, _mailCityLv)
            log("开始调用参数为空请求列表的方法")
            if ELEX_SDK then
                CommonInterface4Lua.LocalizedProducts(nil, nil)--确保第一次能取到
            end
            log("结束调用参数为空请求列表的方法")
            log("进行循环请求")
            MODULE.Payment:LocalizedProducts(data.rechargeInfo)


            --切场景之前初始化管理器
            CommonInterface4Lua.InitMapScene();
            --- 只有断线重连状态才会起效;
            if PROCEDURE.State == ProcedureState.ReconnectState then
                PROCEDURE:EmitStateEvent(ProcedureEvent.ReconnectState)
            elseif PROCEDURE.State ~= ProcedureState.BattleState then
                local cityData = DATA.WorldMap.SelfCityData;
                --PROCEDURE:EmitStateEvent(ProcedureEvent.GotoCity, {cityId = cityData.Id, fromState = ProcedureEvent.InitMapScene});
                PROCEDURE:EmitStateEvent(ProcedureEvent.GotoWorld, { cityId = cityData.Id, fromState = ProcedureEvent.InitMapScene });
            end

        end
    end)

    -- 返回登录状态(第二次以及以上进入登录状态)
    ---@class LoginState _auto_annotation_
    class "LoginState"(function(_ENV)
        inherit "IState"
        ---@class LoginState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "LoginState", set = false }

        ---@class LoginState _auto_annotation_
        ---@field public OnEnter fun(self:LoginState, context) _auto_annotation_
        ---@param self LoginState _auto_annotation_
        function OnEnter(self, context)
            VhLog(" LoginState状态机 OnEnter()  1")
            VIEW:CloseUI("main_panel_new");
            UIMisc:PlayBgSound('Control_gameStart');
            MODULE.Login:AccountLogout()
            MailtoLua.DestroyMailSdk();
            MODULE.Chat:DestroySDKV2()
            CommonInterface4Lua.ReturnLogin()

            VhLog(" LoginState状态机 OnEnter()  2")
            LaunchState.BeforeAllEnterGameInfoReceived()
            self:LoginForLoginState()
        end

        ---@class LoginState _auto_annotation_
        ---@field public OnLeave fun(self:LoginState, context) _auto_annotation_
        ---@param self LoginState _auto_annotation_
        function OnLeave(self, context)
            self:ClearLoginFlowState()
            if not ELEX_SDK then
                VIEW:CloseUI("login_panel");
            end
        end

        ---@class LoginState _auto_annotation_
        ---@field public OnEvent fun(self:LoginState, context, event, params) _auto_annotation_
        ---@param self LoginState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.GotoCity then
                context:SetState(ProcedureState.CityState, params);
            elseif event == ProcedureEvent.GotoBattle then
                context:SetState(ProcedureState.BattleState, params);
            end
        end

        function LoginForLoginState(self)
            VhLog(" 登录状态机 , LoginForLoginState()")
            EVENT:AddListener(self, EventDefine.EventAllInfoReceive, self.__OnInitEnd2);
            EVENT:AddListener(self, EventDefine.ConnectGameServerFailForLogin, self.OnConnectGameServerFailForLoginState)
            EVENT:AddListener(self, EventDefine.AccountMsgFailForLogin, self.OnAccountMsgFailForLoginState);
            EVENT:AddListener(self, EventDefine.EnterGameMsgFailForLogin, self.OnEnterGameMsgFailForLoginState);
            if ELEX_SDK then
                MODULE.Login:__GetGateWayCfg();
                MODULE.Login:ConnectGameServer()
            else
                VIEW:OpenUI("login_panel");
                VhLog("登录状态 确实应当 只是打开页面")
            end
        end

        function ClearLoginFlowState(self)
            EVENT:RemoveListener(self, EventDefine.EventAllInfoReceive);
            EVENT:RemoveListener(self, EventDefine.AccountMsgFailForLogin);
            EVENT:RemoveListener(self, EventDefine.EnterGameMsgFailForLogin);
            EVENT:RemoveListener(self, EventDefine.ConnectGameServerFailForLogin)
        end

        ---EnterGame 消息之后执行的初始化
        function __OnInitEnd2(self, eventId, data)
            self:ClearLoginFlowState()
            VhLog("#ConnectGame#  Procedure.LaunchState状态.OnInitEnd2() , 参数data ==>", data)
            LaunchState.AfterAllEnterGameInfoReceived(data)
        end

        function OnConnectGameServerFailForLoginState(self, eventId)
            self:ClearLoginFlowState()
            VhLog("#状态机# socket 连接失败 ")
            MODULE.Login:SetAccountLoginState(MODULE.Login.ACCOUNT_LOGIN_STATE.Disconnected)
            VIEW:OpenUIEx("confirm_panel", 6, {
                title = 'tips',
                content = string.get("launch_key1"), --多语言: Login状态机,登录中,socket连接失败
                cancel = false,
                confirm = string.get('launch_key2'), --重试
                cbConfirm = function()
                    self:LoginForLoginState()
                end,
                UI_BTN_CLOSE = false,
                UI_BG_CLOSE = false,
            })
        end

        ---AccountMsg 发送失败
        function OnAccountMsgFailForLoginState(self)
            self:ClearLoginFlowState()
            VhLog("#状态机# 处理 登录消息失败")
            VIEW:OpenUIEx("confirm_panel", 6, {
                title = 'tips',
                content = string.get("launch_key11"), --多语言: Login状态机,登录中,处理 AccLogin消息失败
                cancel = false,
                confirm = string.get('launch_key2'), --返回登录
                cbConfirm = function()
                    self:LoginForLoginState()
                end,
                UI_BTN_CLOSE = false,
                UI_BG_CLOSE = false,
            })
        end

        function OnEnterGameMsgFailForLoginState(self)
            self:ClearLoginFlowState()
            VhLog("#状态机# 处理 EnterGame消息失败")
            VIEW:OpenUIEx("confirm_panel", 6, {
                title = 'tips',
                content = string.get("launch_key12"), --多语言: Login状态机,登录中,处理 EnterGame消息失败
                cancel = false,
                confirm = string.get('launch_key4'), --返回登录
                cbConfirm = function()
                    self:LoginForLoginState()
                end,
                UI_BTN_CLOSE = false,
                UI_BG_CLOSE = false,
            })
        end

    end)

    -- 内城状态
    ---@class CityState _auto_annotation_
    class "CityState"(function(_ENV)
        inherit "IState"
        ---@class CityState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "CityState", set = false }

        ---@class CityState _auto_annotation_
        ---@field public OnEnter fun(self:CityState, context,params) _auto_annotation_
        ---@param self CityState _auto_annotation_
        function OnEnter(self, context, params)

            local singleAdd = false
            if ProcedureState.SceneDirty then
                singleAdd = true
                ProcedureState.SceneDirty = false
            end

            local enterCallBack = function()
                MODULE.EntityMenu:SetHudConfigId("cityHudConfig");
                g_panelMgr.CloseAllFunctionUI4Lua();
                MODULE.WorldMap:SetCityMode(true);
                MODULE.HomeScene:EnterHomeScene(params);
                LoadingControllerLua.Close(true)
                -- 进入内城后，输出当前时间
                logWarn("EnterCityState Time-------" .. Time.realtimeSinceStartup)
                if table.isTable(params) and params.callBack then
                    params.callBack()
                end ;
            end
            -- 内城写死当前状态
            MODULE.SceneMgr:SwitchScene(1001, true, true, enterCallBack, singleAdd)


        end

        ---@class CityState _auto_annotation_
        ---@field public OnLeave fun(self:CityState, context) _auto_annotation_
        ---@param self CityState _auto_annotation_
        function OnLeave(self, context)
            MODULE.HomeScene:LeaveHomeScene();
            Quality4Lua.ClearBaseCache()
            MODULE.WorldMap:SetCityMode(false);
        end

        ---@class CityState _auto_annotation_
        ---@field public OnEvent fun(self:CityState, context, event, params) _auto_annotation_
        ---@param self CityState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.ReturnLogin then
                -- 返回登录
                context:SetState(ProcedureState.LoginState);
            elseif event == ProcedureEvent.GotoCity then
                --自己切换自己 直接回调
                if table.isTable(params) and params.callBack then
                    params.callBack()
                end ;
            elseif event == ProcedureEvent.GotoWorld then
                context:SetState(ProcedureState.MapState, params);
            elseif event == ProcedureEvent.GotoBattle then
                --切换战斗场景销毁 A*缓存标记清除  在次回home时才会重建a*数据
                MODULE.CityPathFind:ResetFindGraph()
                context:SetState(ProcedureState.BattleState, params);
            elseif event == ProcedureEvent.ReconnectState then
                context:SetState(ProcedureState.ReconnectState);
                --如果在其他玩家内城断线 重连后切回自己城市 覆盖缓存数据
                if not MODULE.HomeScene:IsInSelfCity() then
                    context.__lastState = ProcedureState.CityState;
                    context.__lastParams = { cityId = DATA.WorldMap.SelfCityData.Id }
                end
            elseif event == ProcedureEvent.GotoDungeon then
                context:SetState(ProcedureState.DungeonState, params);
            elseif event == ProcedureEvent.GotoBagLike then
                context:SetState(ProcedureState.BagLikeState, { sceneId = params.sceneId, fromState = ProcedureEvent.GotoCity });
            elseif event == ProcedureEvent.ReconnectSuccessProcedureEvent then
                --断线重连 ,静默重连方式成功
                --VhLog("#ConnectGame# 重连成功, 内城状态 暂时不需要做什么")                
            end
        end
    end)

    -- 外城状态
    ---@class MapState _auto_annotation_
    class "MapState"(function(_ENV)
        inherit "IState"
        ---@class MapState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "MapState", set = false }

        ---@class MapState _auto_annotation_
        ---@field public OnEnter fun(self:MapState, context,params) _auto_annotation_
        ---@param self MapState _auto_annotation_
        function OnEnter(self, context, params)
            local singleAdd = false
            if ProcedureState.SceneDirty then
                singleAdd = true
                ProcedureState.SceneDirty = false
            end
            MODULE.BI:SendBIEvent(MODULE.BI.EventName.EnterMapBegin)
            local enterCallBack = function()
                --VhLog("#mapTime# lua unity场景切换完成回调, " ,CommonInterface4Lua.GetCurrentTime())
                MODULE.EntityMenu:SetHudConfigId("worldHudConfig");
                MODULE.WorldMap:SetCityMode(false);
                g_panelMgr.CloseAllFunctionUI4Lua();
                LoadingControllerLua.Close(false)
                
                local isReconnect = context.LastState == ProcedureState.ReconnectState
                MODULE.WorldMap:EnterWorldMap(isReconnect);
                VIEW:OpenUI("main_panel_new");  -- 不要挪位置，有时序要求;
                if table.isTable(params) and params.callBack then
                    params.callBack()
                end ;

                MODULE.BI:SendBIEvent(MODULE.BI.EventName.EnterMapEnd)
            end

            --VhLog("#mapTime# lua procedure 开始切换场景,加载lod 资源, " ,CommonInterface4Lua.GetCurrentTime())
            self:StartAsync(enterCallBack, singleAdd)
        end

        ---@class MapState _auto_annotation_
        ---@field public StartAsync fun(self:MapState, callbackOnAllCompleted ,singleAdd) _auto_annotation_
        ---@param self MapState _auto_annotation_
        function StartAsync(self, callbackOnAllCompleted, singleAdd)
            self.__asyncCount = 2
            local OnOneAsyncComplete = function(resultStr)
                self.__asyncCount = self.__asyncCount - 1
                --VhLog("异步又完成一个, __asyncCount = "..self.__asyncCount, " , resultStr = ",resultStr)
                if self.__asyncCount == 0 then
                    --VhLog("异步全部完成, 执行最终回调")
                    callbackOnAllCompleted()
                end
            end

            --VhLog(" 开始加载场景 ")
            -- 开始所有 异步
            --加载场景
            MODULE.SceneMgr:SwitchScene(DATA.SceneMgr.WorldMapSceneId, true, true, OnOneAsyncComplete, singleAdd)
            --加载 lod 配置
            CommonInterface4Lua.AsyncLoadCameraLod("lod/" .. MODULE.SceneMgr:GetSceneLodId(DATA.SceneMgr.WorldMapSceneId), OnOneAsyncComplete);
        end

        ---@class MapState _auto_annotation_
        ---@field public OnLeave fun(self:MapState, context) _auto_annotation_
        ---@param self MapState _auto_annotation_
        function OnLeave(self, context)
            CommonInterface4Lua.LeaveWorldScene();
            Quality4Lua.ClearBaseCache()
            MODULE.PlaceBuilding:EndPlaceBuilding()
        end

        ---@class MapState _auto_annotation_
        ---@field public OnEvent fun(self:MapState, context, event,params) _auto_annotation_
        ---@param self MapState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.ReturnLogin then
                -- 返回登录
                context:SetState(ProcedureState.LoginState)
            elseif event == ProcedureEvent.GotoCity then
                context:SetState(ProcedureState.CityState, params);
            elseif event == ProcedureEvent.GotoWorld then
                --自己切换自己 直接回调
                if table.isTable(params) and params.callBack then
                    params.callBack()
                end ;
            elseif event == ProcedureEvent.GotoBattle then
                --切换战斗场景销毁 A*缓存标记清除  在次回home时才会重建a*数据
                MODULE.CityPathFind:ResetFindGraph()
                context:SetState(ProcedureState.BattleState, params);
            elseif event == ProcedureEvent.ReconnectState then
                context:SetState(ProcedureState.ReconnectState);
                if DATA.Kvk.IsToKvkServer then
                    DATA.Kvk:SetIsToKvkServer(false)
                    context.__lastState = ProcedureState.CityState;
                    context.__lastParams = { cityId = DATA.WorldMap.SelfCityData.Id }
                end
            elseif event == ProcedureEvent.ReconnectSuccessProcedureEvent then
                --断线重连 ,静默重连方式成功
                VhLog("#ConnectGame# 重连成功, 重新请求视野")
                MODULE.WorldMap.AoiDirty = true;
                MODULE.WorldMap:SyncAOIInfo(true);
            elseif event == ProcedureEvent.GotoDungeon then
                context:SetState(ProcedureState.DungeonState, params);
            elseif event == ProcedureEvent.GotoBagLike then
                context:SetState(ProcedureState.BagLikeState, { sceneId = params.sceneId, fromState = ProcedureEvent.GotoWorld });
            end
        end
    end)

    -- pve战斗状态
    ---@class BattleState _auto_annotation_
    class "BattleState"(function(_ENV)
        inherit "IState"
        ---@class BattleState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "BattleState", set = false }

        ---@class BattleState _auto_annotation_
        ---@field public OnEnter fun(self:BattleState, context, params) _auto_annotation_
        ---@param self BattleState _auto_annotation_
        function OnEnter(self, context, params)
            ProcedureState.SceneDirty = true
            SceneMgrLua.ChangeScene(params and params.sceneName or "zhanchang01", true, function()
                VIEW:CloseUI('main_panel_new')
                g_panelMgr.CloseAllFunctionUI4Lua();
                UIMisc:PlayBgSound('Set_State_Battle');
                if params and params.callBack then
                    params.callBack()
                end ;
                LoadingControllerLua.Close(true)
            end)
        end

        ---@class BattleState _auto_annotation_
        ---@field public OnEvent fun(self:BattleState, context, event, params) _auto_annotation_
        ---@param self BattleState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.ReturnLogin then
                -- 返回登录
                context:SetState(ProcedureState.LoginState);
            elseif event == ProcedureEvent.GotoCity then
                context:SetState(ProcedureState.CityState, params);
            elseif event == ProcedureEvent.GotoWorld then
                context:SetState(ProcedureState.MapState, params);
            end
        end

        ---@class BattleState _auto_annotation_
        ---@field public OnLeave fun(self:BattleState, context) _auto_annotation_
        ---@param self BattleState _auto_annotation_
        function OnLeave(self, context)
            UIMisc:PlayBgSound('Set_State_Camp');
            --LoadingControllerLua.CaptureScreen()
        end
    end)

    -- 断线重连过度状态;
    ---@class ReconnectState _auto_annotation_
    class "ReconnectState"(function(_ENV)
        inherit "IState"
        ---@class ReconnectState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "ReconnectState", set = false }

        ---@class ReconnectState _auto_annotation_
        ---@field public OnEnter fun(self:ReconnectState, context) _auto_annotation_
        ---@param self ReconnectState _auto_annotation_
        function OnEnter(self, context)
            VIEW:CloseUI('main_panel_new')
            MailtoLua.DestroyMailSdk();
            MODULE.Chat:DestroySDKV2();
            g_panelMgr.CloseAllFunctionUI4Lua();
        end

        ---@class ReconnectState _auto_annotation_
        ---@field public OnLeave fun(self:ReconnectState, context) _auto_annotation_
        ---@param self ReconnectState _auto_annotation_
        function OnLeave(self, context)
        end

        ---@class ReconnectState _auto_annotation_
        ---@field public OnEvent fun(self:ReconnectState, context, event, params) _auto_annotation_
        ---@param self ReconnectState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.ReconnectState then
                context:SetState(context.LastState, context.LastParams);
            end
        end
    end)

    -- 副本状态
    ---@class DungeonState _auto_annotation_
    class "DungeonState"(function(_ENV)
        inherit "IState"
        ---@class DungeonState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "DungeonState", set = false }

        ---@class DungeonState _auto_annotation_
        ---@field public OnEnter fun(self:DungeonState, context, params) _auto_annotation_
        ---@param self DungeonState _auto_annotation_
        function OnEnter(self, context, params)
            -- 跳转场景后 开启心跳
            MODULE.Dungeon:StartHeartBeat();

            ProcedureState.SceneDirty = true

            local enterCallBack = function()
                VIEW:CloseUI('main_panel_new')
                g_panelMgr.CloseAllFunctionUI4Lua();
                UIMisc:PlayBgSound('Set_State_Battle');
                if params and params.callBack then
                    params.callBack()
                end ;
                LoadingControllerLua.Close(true);
            end
            self:StartAsync(enterCallBack, params.sceneId)
        end
        ---@class DungeonState _auto_annotation_
        ---@field public StartAsync fun(self:DungeonState, callbackOnAllCompleted,sceneId) _auto_annotation_
        ---@param self DungeonState _auto_annotation_
        function StartAsync(self, callbackOnAllCompleted, sceneId)
            self.__asyncCount = 2
            local OnOneAsyncComplete = function(resultStr)
                self.__asyncCount = self.__asyncCount - 1
                --VhLog("异步又完成一个, __asyncCount = "..self.__asyncCount, " , resultStr = ",resultStr)
                if self.__asyncCount == 0 then
                    --VhLog("异步全部完成, 执行最终回调")
                    callbackOnAllCompleted()
                end
            end
            -- 开始所有 异步
            --加载场景
            MODULE.SceneMgr:SwitchScene(sceneId, false, true, OnOneAsyncComplete, false)
            --加载 lod 配置
            CommonInterface4Lua.AsyncLoadCameraLod_Dungeon("lod/" .. MODULE.SceneMgr:GetSceneLodId(sceneId), OnOneAsyncComplete);
        end

        ---@class DungeonState _auto_annotation_
        ---@field public OnEvent fun(self:DungeonState, context, event, params) _auto_annotation_
        ---@param self DungeonState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.GotoCity then
                context:SetState(ProcedureState.CityState, params);
            elseif event == ProcedureEvent.GotoWorld then
                context:SetState(ProcedureState.MapState, params);
            elseif event == ProcedureEvent.ReconnectState then
                context:SetState(ProcedureState.ReconnectState);
                context.__lastState = ProcedureState.CityState;
                context.__lastParams = { cityId = DATA.WorldMap.SelfCityData.Id }
            elseif event == ProcedureEvent.ReturnLogin then
                -- 返回登录
                context:SetState(ProcedureState.LoginState);
            end
        end

        ---@class DungeonState _auto_annotation_
        ---@field public OnLeave fun(self:DungeonState, context) _auto_annotation_
        ---@param self DungeonState _auto_annotation_
        function OnLeave(self, context)
            CommonInterface4Lua.LeaveDungeonScene();
            Quality4Lua.ClearBaseCache();
            NET.Common.SendGetPlayerFullInfo({});
        end
    end)

    -- BagLike状态
    ---@class BagLikeState _auto_annotation_
    class "BagLikeState"(function(_ENV)
        inherit "IState"
        ---@class BagLikeState _auto_annotation_
        ---@field public StateName nil _auto_annotation_
        property "StateName" { field = "__stateName", type = System.String, default = "BagLikeState", set = false }

        ---@class BagLikeState _auto_annotation_
        ---@field public OnEnter fun(self:BagLikeState, context, params) _auto_annotation_
        ---@param self BagLikeState _auto_annotation_
        function OnEnter(self, context, params)
            local singleAdd = false
            if ProcedureState.SceneDirty then
                singleAdd = true
                ProcedureState.SceneDirty = false
            end
            local enterCallBack = function()
                VIEW:CloseUI('main_panel_new')
                g_panelMgr.CloseAllFunctionUI4Lua();
                UIMisc:PlayBgSound('Set_State_Battle');
                LoadingControllerLua.Close(false);
                MODULE.BagLike:OnEnter(params)
            end
            self:StartAsync(enterCallBack, params.sceneId, singleAdd)
        end
        ---@class BagLikeState _auto_annotation_
        ---@field public StartAsync fun(self:BagLikeState, callbackOnAllCompleted,sceneId) _auto_annotation_
        ---@param self BagLikeState _auto_annotation_
        function StartAsync(self, callbackOnAllCompleted, sceneId, singleAdd)
            self.__asyncCount = 1
            local OnOneAsyncComplete = function(resultStr)
                self.__asyncCount = self.__asyncCount - 1
                --VhLog("异步又完成一个, __asyncCount = "..self.__asyncCount, " , resultStr = ",resultStr)
                if self.__asyncCount == 0 then
                    --VhLog("异步全部完成, 执行最终回调")
                    callbackOnAllCompleted()
                end
            end
            
            --加载场景
            MODULE.SceneMgr:SwitchScene(sceneId, true, true, OnOneAsyncComplete, singleAdd)
            --加载 lod 配置
            --CommonInterface4Lua.AsyncLoadCameraLod_Dungeon("lod/" .. MODULE.SceneMgr:GetSceneLodId(sceneId), OnOneAsyncComplete);
        end

        ---@class BagLikeState _auto_annotation_
        ---@field public OnEvent fun(self:BagLikeState, context, event, params) _auto_annotation_
        ---@param self BagLikeState _auto_annotation_
        function OnEvent(self, context, event, params)
            if event == ProcedureEvent.GotoCity then
                context:SetState(ProcedureState.CityState, params);
            elseif event == ProcedureEvent.GotoWorld then
                context:SetState(ProcedureState.MapState, params);
            elseif event == ProcedureEvent.ReconnectState then
                context:SetState(ProcedureState.ReconnectState);
                context.__lastState = ProcedureState.CityState;
                context.__lastParams = { cityId = DATA.WorldMap.SelfCityData.Id }
            elseif event == ProcedureEvent.ReturnLogin then
                -- 返回登录
                context:SetState(ProcedureState.LoginState);
            end
        end

        ---@class BagLikeState _auto_annotation_
        ---@field public OnLeave fun(self:BagLikeState, context) _auto_annotation_
        ---@param self BagLikeState _auto_annotation_
        function OnLeave(self, context)
            --CommonInterface4Lua.LeaveDungeonScene();
            Quality4Lua.ClearBaseCache();
            --NET.Common.SendGetPlayerFullInfo({});
        end
    end)

    -- 所有游戏流程
    ---@class ProcedureState _auto_annotation_
    class "ProcedureState"(function(_ENV)
        local __LaunchState = LaunchState()
        local __LoginState = LoginState()
        local __CityState = CityState()
        local __MapState = MapState()
        local __BattleState = BattleState()
        local __ReconnectState = ReconnectState()
        local __DungeonState = DungeonState()
        local __BagLikeState = BagLikeState()

        __Static__()
        property "SceneDirty" { type = System.Boolean, default = false }

        __Static__()
        property "LaunchState" { type = System.Table, set = false, get = function()
            return __LaunchState
        end }
        __Static__()
        property "LoginState" { type = System.Table, set = false, get = function()
            return __LoginState
        end }
        __Static__()
        property "CityState" { type = System.Table, set = false, get = function()
            return __CityState
        end }
        __Static__()
        property "MapState" { type = System.Table, set = false, get = function()
            return __MapState
        end }
        __Static__()
        property "BattleState" { type = System.Table, set = false, get = function()
            return __BattleState
        end }
        __Static__()
        property "ReconnectState" { type = System.Table, set = false, get = function()
            return __ReconnectState
        end }
        __Static__()
        property "DungeonState" { type = System.Table, set = false, get = function()
            return __DungeonState
        end }
        __Static__()
        property "BagLikeState" { type = System.Table, set = false, get = function()
            return __BagLikeState
        end }

    end)

    ---@class ProcedureEvent _auto_annotation_
    ---@field public ReturnLogin number _auto_annotation_
    ---@field public InitMapScene number _auto_annotation_
    ---@field public GotoCity number _auto_annotation_
    ---@field public GotoWorld number _auto_annotation_
    ---@field public GotoBattle number _auto_annotation_
    ---@field public ReconnectState number _auto_annotation_
    ---@field public GotoDungeon number _auto_annotation_
    ---@field public GotoBagLike number _auto_annotation_
    enum "ProcedureEvent" {
        ReturnLogin = 1, -- 返回登录
        InitMapScene = 2, -- 初始化主场景
        GotoCity = 3, -- 进入主城
        GotoWorld = 4, -- 进入世界地图
        GotoBattle = 5, -- 进入pve战斗
        ReconnectState = 7, -- 断线重连过度状态【清理显示，重新显示】;
        GotoDungeon = 8, -- 进入副本场景
        GotoBagLike = 9, -- 进入背包Like场景
        ReconnectSuccessProcedureEvent = 10, -- 断线重连 -> 静默重连方式 成功
    }

    class "Procedure"(function(_ENV)
        inherit "IContext"

        ---@class Procedure _auto_annotation_
        ---@field public __ctor fun(self:Procedure) _auto_annotation_
        ---@param self Procedure _auto_annotation_
        function __ctor(self)
            _G.ProcedureState = ProcedureState
            self:SetState(ProcedureState.LaunchState);
            -- 暂时不需要update逻辑，屏蔽提高性能
            --if not self.handle then
            --    self.handle = CoUpdateBeat:CreateListener(self.Update, self);
            --end
            --CoUpdateBeat:AddListener(self.handle);
        end

        ---@class Procedure _auto_annotation_
        ---@field public __dtor fun(self:Procedure) _auto_annotation_
        ---@param self Procedure _auto_annotation_
        function __dtor(self)
            --if self.handle then
            --    CoUpdateBeat:RemoveListener(self.handle)
            --end
        end

        --function Update(self)
        --    self:OnUpdateState();
        --end
        function GetProcedureState(self)
            return ProcedureState
        end
    end)
end)