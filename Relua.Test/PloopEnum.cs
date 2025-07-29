using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Lua.Tests
{
    [TestFixture]
    public class PloopEnum
    {
        [Test]
        public void PloopPloopEnum()
        {
            string multiLineString = """
                                     enum "PBEMapNode1" {
                                     	"CITY",
                                     	"SELF_CITY",
                                     	"NPC",
                                     	"RES",
                                     	"ARMY",
                                     	"WORLD_PLACE",
                                     	"BLOCK",
                                     	"VILLAGE",
                                     	"LOW_HOLE", "MIDDLE_HOLE", "TOP_HOLE",
                                     	"TEMPLE", "ALTAR", "PASS","INSTANCE_FORTRESS","INSTANCE_ONE_WAY_DOOR",
                                     	"ALLIANCE_FLAG",
                                     	"ALLIANCE_CASTLE",
                                     	"ALLIANCE_MINE", -- 联盟建筑 资源矿
                                     	"ALLIANCE_RES", -- 联盟资源田
                                     	"RALLY_NPC",
                                     	"TROOP",
                                     	-- 以下都是客户端的
                                     	"ALLIANCE_BUILDING", -- 联盟建筑;
                                     	--"EXPLORE_ITEM", -- 探索任务;
                                     	--"EXPLORE_TROOP", -- 探索任务行军;
                                     	"NEUTRAL_BUILDING", -- 中立联盟建筑;
                                     	"TREE",
                                     	"STATIC_OBJ",
                                     	"BOOKMARK",
                                     	"BOOKMARK_ALLIANCE"
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            var str = expr.ToString();
            Assert.Pass(str);
        }
        
        [Test]
        public void PloopPloopEnum2()
        {
	        string multiLineString = """
	                                 enum "ExplorerTeamStatus" {
	                                     ETS_IDLE	= 0,		-- 休闲
	                                     ETS_EXPLORING = 1,	-- 工作中
	                                     ETS_IN_CD	= 2,		-- CD中
	                                     ETS_LOCK = 3,			-- 锁定中 客户端使用，服务器不使用
	                                 }
	                                 """;
	        var tokenizer = new Tokenizer(multiLineString);
	        var parser = new Parser(tokenizer);
	        var expr = parser.Read();
	        var str = expr.ToString();
	        Assert.Pass(str);
        }
    }
}