using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Lua.Tests
{
    [TestFixture]
    public class PloopAttribte
    {
        [Test]
        public void PloopAttribute()
        {
            string multiLineString = """
                                     __Static__()
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Assert.Pass(expr.ToString());
        }


        [Test]
        public void PloopAttribute2()
        {
            string multiLineString = """
                                     __Set__(PropertySet.Clone + PropertySet.Retain)
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Assert.Pass(expr.ToString());
        }


        [Test]
        public void PloopAttribute3()
        {
            string multiLineString = """
                                     __Static__()
                                        function GetKey(resId)
                                            return EventDefine.GetItemEventID(resId, E_ITEM_TYPE.ITEM_RESOURCE);
                                        end
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Assert.Pass(expr.ToString());
        }

        [Test]
        public void PloopAttribute4()
        {
            string multiLineString = """
                                     __Set__(PropertySet.Clone + PropertySet.Retain)
                                     __Get__(PropertySet.Clone)
                                     property "Data" { type = Data }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Assert.Pass(expr.ToString());
        }
    }
}