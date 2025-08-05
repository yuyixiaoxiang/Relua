-----------------------------------------------
-- [FILE] GameView.lua
-- [DATE] 2020-09-12
-- [CODE] BY zengqingfeng
-- [MARK] 界面核心类
-----------------------------------------------
Module "Game.View" (function(_ENV)
--namespace "Game.View"
	-- 界面模块可以访问玩法数据模块和逻辑模块的类型
	--import "Game.Data"
	--import "Game.Module"

    -- c#枚举界面层级Layer 
	---@class EmUILayer _auto_annotation_
	---@field public TouchLayer number _auto_annotation_
	---@field public HeadLayer number _auto_annotation_
	---@field public HudLayer number _auto_annotation_
	---@field public FunctionLayer number _auto_annotation_
	---@field public FunctionExLayer number _auto_annotation_
	---@field public CameraLayer number _auto_annotation_
	---@field public MessageLayer number _auto_annotation_
	---@field public AboveLoadingLayer number _auto_annotation_
	---@field public UILayerCnt number _auto_annotation_
    enum "EmUILayer" {
		TouchLayer = 0,    -- Touch层
        HeadLayer = 1,      -- 头顶信息;
        HudLayer = 2,       -- Hud界面;
        FunctionLayer = 3,  -- 功能界面;
		FunctionExLayer = 4,
		CameraLayer = 5,
        MessageLayer = 6,   -- 消息提示;
        AboveLoadingLayer = 7,  -- loading界面之上;
        UILayerCnt = 8,
    }
    
	local __viewSerialMax = 100
	---@class GameView _auto_annotation_
	class "GameView" (function(_ENV)
		---@class GameView _auto_annotation_
		---@field public ViewParams table _auto_annotation_
		property "ViewParams" { field = "__viewParams", type = Table, default = {}, set = false}
		---@class GameView _auto_annotation_
		---@field public ViewSerial number _auto_annotation_
		property "ViewSerial" { type = Integer, default = 0}
		-- 激活的已存在的界面（包括动态UI）
		__Indexer__()
		---@class GameView _auto_annotation_
		---@field public Views table _auto_annotation_
		property "Views" { field = "__views", type = Table, default = {},
			get = function(self, id) return self.__views[id]; end,
			set = false, -- 外部不允许修改
		}
        -- 每一个UI层级最高的界面id
        ---@class GameView _auto_annotation_
        ---@field public ViewTops table _auto_annotation_
        property "ViewTops" { field = "__viewTops", type = Table, default = {}, set = false}

		-- 关闭主界面的引用计数
		---@class GameView _auto_annotation_
		---@field public CloseMainUIMap number _auto_annotation_
		property "CloseMainUIMap" { field = "__closeMainUIMap", type = Integer, set = false}
		
		---@class GameView _auto_annotation_
		---@field public __ctor fun(self:GameView) _auto_annotation_
		---@param self GameView _auto_annotation_
		function __ctor(self)
			self.__views = {}
			self.__viewsN = {}
			self.__closeTwCache = {}

            self.__viewTops = {}
            for name, value in Enum.GetEnumValues(EmUILayer) do
				self.__viewTops[value] = -1; -- 赋予初始值
            end
			
			local cfg = GameConfigManager:GetConfPanelConfigList();
			if cfg then
				self.__cfgTable = table.toHash(cfg.list, 'name')
			end
			self.__closeMainUIMap = {}
		end 
		
		-- 打开界面（需要配置）
		__Arguments__{ String, Variable.Rest() }
		---@class GameView _auto_annotation_
		---@field public OpenUI fun(self:GameView, name, params) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OpenUI(self, name, params)
			self:OpenUIRaw(name, -1, false, params);
			--g_panelMgr.OpenPanel4Lua(name, -1, false, self:SaveParams(params));
		end

		--- 指定显示层级方式打开UI
		---@class GameView _auto_annotation_
		---@field public OpenUIEx fun(self:GameView, name, changeLayer, params) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OpenUIEx(self, name, changeLayer, params)
			self:OpenUIRaw(name, changeLayer, false, params);
			--g_panelMgr.OpenPanel4Lua(name, changeLayer, false, self:SaveParams(params));
		end

		-- 原生打开界面
		-- changeLayer:指定打开界面所在界面组，-1为读取配置
		-- forceCover:打开界面是否隐藏同级其他界面，false为读取配置
		---@class GameView _auto_annotation_
		---@field public OpenUIRaw fun(self:GameView, name, changeLayer, forceCover, params) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OpenUIRaw(self, name, changeLayer, forceCover, params)
			local tw = self.__closeTwCache[name]
			if tw then
				tw:Kill(true)
				self.__closeTwCache[name] = nil
			end
			
			g_panelMgr.OpenPanel4Lua(name, changeLayer, forceCover, self:SaveParams(params));

			MODULE.BI:SendBIEvent(MODULE.BI.EventName.PopupShow, {
				ed_pupupType = changeLayer,
				ed_popupExplain = table2string(params or {}, "", 5),
				ed_popupName = name,
			})
		end
		
		--__Arguments__{ String }
		---@class GameView _auto_annotation_
		---@field public CloseUI fun(self:GameView, name, closeAnimType) _auto_annotation_
		---@param self GameView _auto_annotation_
		function CloseUI(self, name, closeAnimType)
			self:__CloseUIInternal(name, closeAnimType)
		end

		-- 关闭正在显示的界面
		---@class GameView _auto_annotation_
		---@field public CloseVisibleUI fun(self:GameView, name) _auto_annotation_
		---@param self GameView _auto_annotation_
		function CloseVisibleUI(self, name)
			if not name then return end
			local panel = VIEW:IsPanelVisible(name)
			if panel then
				VIEW:CloseUI(name)
			end
		end

		-- 关闭所有功能界面
		---@class GameView
		---@field CloseAllFunctionUI fun(self:GameView) 关闭所有功能界面
		---@param self GameView
		function CloseAllFunctionUI(self)
			g_panelMgr.CloseAllFunctionUI4Lua();
		end
		
		--- 判断界面是否打开
		---	realShow：是否真正显示【指有资源且真正显示出来】
		---@class GameView _auto_annotation_
		---@field public IsPanelVisible fun(self:GameView, panelName, realShow) _auto_annotation_
		---@param self GameView _auto_annotation_
		function IsPanelVisible(self, panelName, realShow)
			realShow = realShow or false;
			return g_panelMgr.IsVisible(panelName, realShow)
		end
		
		-- 同步加载并附加UI到目标节点（嵌套）
		-- 使用事件监听加载成功回调
		---@class GameView _auto_annotation_
		---@field public AttachUI fun(self:GameView, view, path, name, params) _auto_annotation_
		---@param self GameView _auto_annotation_
		function AttachUI(self, view, path, name, params)
			local parentUid = g_panelMgr.GetPanelComponent(view.Id, UIControlType.UI_DynamicLoad, path);
			g_panelMgr.CreateDynamicUI(parentUid, name, self:SaveParentParams(params,view.Id));
			return parentUid; -- 动态界面
		end

		-- 基于容器附加UI [GameObjectContainer];
		---@class GameView _auto_annotation_
		---@field public AttachUIByContainerID fun(self:GameView, panel_id, id, name, params) _auto_annotation_
		---@param self GameView _auto_annotation_
		function AttachUIByContainerID(self, panel_id, id, name, params)
			local dynamicUID = g_panelMgr.GetCompFromContainer(panel_id, id, UIControlType.UI_DynamicLoad);
			g_panelMgr.CreateDynamicUI(dynamicUID, name, self:SaveParentParams(params, panel_id));
			return dynamicUID; -- 动态界面
		end

		-- 基于容器附加UI [GameAutoObjectContainer];
		---@class GameView _auto_annotation_
		---@field public AttachUIByContainerKey fun(self:GameView, panel_id, key, name, params) _auto_annotation_
		---@param self GameView _auto_annotation_
		function AttachUIByContainerKey(self, panel_id, key, name, params)
			local dynamicUID = g_panelMgr.GetCompFromContainerKey(panel_id, key, UIControlType.UI_DynamicLoad);
			g_panelMgr.CreateDynamicUI(dynamicUID, name, self:SaveParentParams(params, panel_id));
			return dynamicUID; -- 动态界面
		end

		---@class GameView _auto_annotation_
		---@field public DeattachUI fun(self:GameView, parentUid) _auto_annotation_
		---@param self GameView _auto_annotation_
		function DeattachUI(self, parentUid)
			g_panelMgr.DestoryDynamicUI(parentUid);
		end
		
		-- 界面是否打开
		--function CheckViewOpen(self, uiName)
		--	for id, view in pairs(self.__views) do
		--		if view.Name == uiName and view.IsShow then
		--			return true
		--		end
		--	end
		--	return false;
		--end
		
		-- 获取已经打开的界面pid
		---@class GameView _auto_annotation_
		---@field public GetViewId fun(self:GameView, uiName) _auto_annotation_
		---@param self GameView _auto_annotation_
		function GetViewId(self, uiName)
			local view = self.__viewsN[uiName]
			return view and view.Id or -1
		end
		
		---@class GameView _auto_annotation_
		---@field public SaveParams fun(self:GameView, params) _auto_annotation_
		---@param self GameView _auto_annotation_
		function SaveParams(self, params)
			if (not params or table.isTable(params) and table.isNullOrEmpty(params)) then
				return -1
			end 
			self.ViewSerial = self.ViewSerial + 1;
			if (self.ViewSerial > __viewSerialMax) then -- todo 循环队列
				self.ViewSerial = 0 -- 最大存10个界面参数缓存，多了复用老的位置
			end
			self.ViewParams[self.ViewSerial] = params -- 包装参数，记录流水号
			return self.ViewSerial;
		end 
		
		-- 动态界面 参数保存到父物体中
		---@class GameView _auto_annotation_
		---@field public SaveParentParams fun(self:GameView, params, parentId) _auto_annotation_
		---@param self GameView _auto_annotation_
		function SaveParentParams(self, params, parentId)
			if (not params or table.isTable(params) and table.isNullOrEmpty(params)) then
				return -1
			end
			local __parent_view = self.Views[parentId];
			if __parent_view.__parentViewParams == nil then
				__parent_view.__parentViewParams = {}
				__parent_view.__parentViewSerial = __viewSerialMax
			end
			local __view_serial = __parent_view.__parentViewSerial
			__view_serial = __view_serial + 1
			__parent_view.__parentViewSerial = __view_serial
			__parent_view.__parentViewParams[__view_serial] = params
			return __view_serial;
		end

        --__Arguments__ { Variable.Optional(Integer, -1) }
		---@class GameView _auto_annotation_
		---@field public GetParams fun(self:GameView, viewSerial, view) _auto_annotation_
		---@param self GameView _auto_annotation_
		function GetParams(self, viewSerial, view)
			if viewSerial > __viewSerialMax then
				if view.Parent.__parentViewParams then
					return view.Parent.__parentViewParams[viewSerial]
				end
			end
			return self.ViewParams[viewSerial]
		end

        -- 获取界面深度;越大的越靠近屏幕
        ---@class GameView _auto_annotation_
        ---@field public GetViewDepth fun(self:GameView, id) _auto_annotation_
        ---@param self GameView _auto_annotation_
        function GetViewDepth(self, id)
            return self.Views[id] and self.Views[id].Depth or -1;
        end
		
		---@class GameView _auto_annotation_
		---@field public GetPanelConfig fun(self:GameView, panelName) _auto_annotation_
		---@param self GameView _auto_annotation_
		function GetPanelConfig(self, panelName)
			return self.__cfgTable[panelName] or self.__cfgTable['default']
		end
		
		----------------------------- 反射接受c#事件 ---------------------
		---@class GameView _auto_annotation_
		---@field public OnAwake fun(self:GameView, id, name) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnAwake(self, id, name)
			local view = self.Views[id];
			if view then logError("view OnAwake repeated!!") end -- 一个界面实例不可能两次调用awake
			local viewClass
			if name == "login_panel" then
				viewClass = require("Common/GamePlay/GameView/Login/login_panel")
			elseif name == "serverlist_panel" then
				viewClass = require("Common/GamePlay/GameView/Login/serverlist_panel")
			elseif name == "fly_effect_panel" then
				viewClass =  require("GameView/FlyEffect/fly_effect_panel")
			elseif name == "entity_menu_panel" then
				viewClass =  require("GameView/EntityMenu/core/entity_menu_panel")
			elseif name == "guide_view_panel" then
				viewClass =  require("GameView/GuideView/guide_view_panel")
			elseif name == "gauss_blur_panel" then
				viewClass =  require("GameView/Common/util_panel/gauss_blur_panel")
			elseif name == "top_menu_panel" then
				viewClass =  require("GameView/Common/util_panel/top_menu_panel")
			elseif name == "hud_info_panel" then
				viewClass =  require("GameView/Main/hud_info_panel")
			elseif name == "resource_group" then
				viewClass =  require("GameView/Main/resource_group")
			elseif name == "dialog_bg_group" then
				viewClass =  require("GameView/Common/util_group/dialog_bg_group")
			elseif name == "main_panel_new" then
				viewClass =  require("GameView/Main_panel_new/main_panel_new")

			elseif name == "map_hub_panel" then
				viewClass =  require("GameView/MapHub/map_hub_panel")
				
				
			elseif name == "msg_panel" then
				viewClass = require("GameView/Msg/msg_panel")
			end
			



			if viewClass == nil then
				viewClass = _ENV[name]; -- 资源名字和脚本名字同名 -- 需要加保护	
			end
			if not viewClass then logError("view script missing!! name:"..name) return; end
			view = viewClass(id, name); -- 资源实例和脚本实例生命周期对应
			self.__views[id] = view;
			view.__name = name	-- 防止未命名;
			self.__viewsN[name] = view
			view:OnAwake(id, name);
		end
		
		---@class GameView _auto_annotation_
		---@field public OnDestroy fun(self:GameView, id) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnDestroy(self, id)
			local view = self.Views[id];
			if not view then logError("OnDestroy:view is null!! id:".. id) return end

			if not string.isNullOrEmpty(view.Name) then
				self.__viewsN[view.Name] = nil
			end

			view:OnDestroy(); -- 资源销毁
			view:Dispose();
			view = nil;
			self.__views[id] = nil;
		end
		
		-- 是否需要关闭主界面
		---@class GameView _auto_annotation_
		---@field public CanShowMainUI fun(self:GameView) _auto_annotation_
		---@param self GameView _auto_annotation_
		function CanShowMainUI(self)
			return table.isNullOrEmpty(self.CloseMainUIMap)
		end
		
		---@class GameView _auto_annotation_
		---@field public OnEnable fun(self:GameView, id) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnEnable(self, id)
			 local view = self.Views[id];
			if not view then logError("OnEnable:view is null!! id:".. id) return end

			if self.__IsCloseMainUI(view) then
				local canFlag = self:CanShowMainUI()
				self.__closeMainUIMap[view.Name] = view
				if canFlag then
					EVENT:Brocast(EventDefine.CloseMainUIFlagChanged);
				end
			end

			view:OnEnable();
		end
		
		---@class GameView _auto_annotation_
		---@field public OnDisable fun(self:GameView, id) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnDisable(self, id)
			local  view = self.Views[id];
			if not view then logError("OnDisable:view is null!! id:".. id) return end

			EVENT:Brocast(EventDefine.OnDisableUI, view.Name, id);
			if self.__IsCloseMainUI(view) then
				self.__closeMainUIMap[view.Name] = nil
				local canFlag = self:CanShowMainUI()
				if canFlag then
					EVENT:Brocast(EventDefine.CloseMainUIFlagChanged);
				end
			end

			view:OnDisable();
		end
		
		---@class GameView _auto_annotation_
		---@field public __IsCloseMainUI fun(view) _auto_annotation_
		---@param self GameView _auto_annotation_
		function __IsCloseMainUI(view)
			local cfg = view.panelCfg
			if not cfg or not cfg.close_main_ui or view.Parent ~= nil then
				return false
			end

			return cfg.layer == EmUILayer.FunctionLayer
		end

		---@class GameView _auto_annotation_
		---@field public OnClose fun(self:GameView, id) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnClose(self, id)
			local view = self.Views[id];
			if not view then logError("OnDisable:view is null!! id:".. id) return end
			EVENT:BrocastNow(EventDefine.OnCloseUI, view.Name, id);
			if view.IsShow then -- 因为可能播放动画前提前调用过了所有这里不要重复调用
				view:OnClose();
			end
		end
		
		---@class GameView _auto_annotation_
		---@field public OnShow fun(self:GameView, id, viewSerial, openAnimType) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnShow(self, id, viewSerial, openAnimType)
          local  view = self.Views[id];
			if not view then logError("OnShow:view is null!! id:".. id) return end

			view.panelCfg = self:GetPanelConfig(view.Name)
			EVENT:BrocastNow(EventDefine.OnShowUI, view.Name, id);
			local params=self:GetParams(viewSerial, view)
			view:OnShow(params);

			view.__openAnimType = openAnimType	-- 缓存一下，用于关闭时反向播放关闭动效;
			self:__PlayOpenUIAnim(id, openAnimType)
		end
		
		-- 层级变化
		---@class GameView _auto_annotation_
		---@field public OnDepthChanged fun(self:GameView, id, depth) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnDepthChanged(self, id, depth)
			local view = self.__views[id];
			if not view then logError("OnDepthChanged:view is null!! id:".. id) return end
			view:OnDepthChanged(depth);
		end 
		
		-- UI附加成功
		-- parentUid:父节点组件id（UI_DynamicLoad）
		---@class GameView _auto_annotation_
		---@field public OnAttach fun(self:GameView, id, parentUid) _auto_annotation_
		---@param self GameView _auto_annotation_
		function OnAttach(self, id, parentUid)
            local viewChild = self.__views[id];
			local uid = UIMisc:GetPidByUid(parentUid);
            local view = self.__views[uid];
			if not viewChild then logError("OnAttach:viewChild is null!! id:".. id) return end
			if not view then logError("OnAttach:view is null!! parentUid:".. parentUid) return end
			log(string.format("parentUid%s, %s , %s", parentUid, uid,view.Name))
			view:OnAttached(viewChild, parentUid); -- 通知父界面
			viewChild:OnAttach(view, parentUid); -- 通知子界面
		end
 
        -- UI层级最高的界面id更新
        ---@class GameView _auto_annotation_
        ---@field public OnUISortLayerChanged fun(self:GameView, layerId, topPid) _auto_annotation_
        ---@param self GameView _auto_annotation_
        function OnUISortLayerChanged(self, layerId, topPid)
            self.__viewTops[layerId] = topPid;
            --EVENT:BrocastNow(EventDefine.OnUISortLayerChangedNow, layerId, topPid) -- 暂时没用到
			EVENT:Brocast(EventDefine.OnUISortLayerChanged)
        end
		----------------------------/ 反射接受c#事件 /--------------------
		
		---@class GameView _auto_annotation_
		---@field public __PlayOpenUIAnim fun(self:GameView, id, openAnimType) _auto_annotation_
		---@param self GameView _auto_annotation_
		function __PlayOpenUIAnim(self, id, openAnimType)
			openAnimType = openAnimType or 0
			if openAnimType == 0 then
				return
			end

			--- 缩放动画;
			if openAnimType == 1 then
				self:__ScaleAnim(id)
			end
		end
		
		---@class GameView _auto_annotation_
		---@field public __ScaleAnim fun(self:GameView, id) _auto_annotation_
		---@param self GameView _auto_annotation_
		function __ScaleAnim(self, id)
			local tran = g_panelMgr.GetGameTran(id)
			if not tran then return end

			local tw = TWEEN:CreateScale(tran, 0.5, 1, 0.2, true, true, 'OutBack')
			tw:SetUpdate(true);	-- 不受timeScale影响
		end
		
		---@class GameView _auto_annotation_
		---@field public __CloseUIInternal fun(self:GameView, name, closeAnimType) _auto_annotation_
		---@param self GameView _auto_annotation_
		function __CloseUIInternal(self, name, closeAnimType)
            local view = self.__viewsN[name]
			
			if not view then
				g_panelMgr.ClosePanel4Lua(name);
				return
			end

			--- 播放UI关闭音效;
			if view.panelCfg then
				UIMisc:PlayUISound(view.panelCfg.close_audio)
			end

			-- 如果不指定关闭动画，就用打开动画;
			if closeAnimType == nil then
				closeAnimType = view.__openAnimType
			end
			closeAnimType = closeAnimType or 0
			
			if not self:__PlayCloseUIAnim(view.Id, closeAnimType, name) then
				g_panelMgr.ClosePanel4Lua(name);
			end
		end

		---@class GameView _auto_annotation_
		---@field public __PlayCloseUIAnim fun(self:GameView, id, closeAnimType, uiName) _auto_annotation_
		---@param self GameView _auto_annotation_
		function __PlayCloseUIAnim(self, id, closeAnimType, uiName)
			if closeAnimType == 0 then
				return false
			end

			--- 缩放动画;
			if closeAnimType == 1 then
				local tran = g_panelMgr.GetGameTran(id)
				if not tran then return false end

				-- OnClose提前调用，在播放关闭动画之前，因为在动画关闭前就要处理相关事件的销毁
				local view = self.__views[id];
				view:OnClose();
				
				local sq = TWEEN:CreateSequence()
				self.__closeTwCache[uiName] = sq
				TWEEN:AppendTween(sq, TWEEN:CreateScale(tran, 1, 0.5, 0.2, true, true, 'InBack'))
				TWEEN:AppendCallback(sq, function()
					self.__closeTwCache[uiName] = nil
					g_panelMgr.ClosePanel4Lua(uiName)
				end)
				return true
			end
			
			return false
		end
	end)
end)