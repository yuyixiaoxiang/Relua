using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Lua.Tests {
    [TestFixture]
    public class PloopProperty {
        [Test]
        public void PloopPropertyGet1() {
            string multiLineString = """
                                     property "Name" {
                                     	get = "GetName",
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.ReadPloopProperty();
            Assert.Pass(expr.ToString());
        }
        
        [Test]
        public void PloopPropertyGet2() {
            string multiLineString = """
                                     property "Name" {
                                     	get = function(self) return self.__name end,
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.ReadPloopProperty();
            Assert.Pass(expr.ToString());
        }
        
        [Test]
        public void PloopPropertySet1() {
            string multiLineString = """
                                     property "Name" {
                                     	set = false,
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.ReadPloopProperty();
            Assert.Pass(expr.ToString());
        }

        [Test]
        public void PloopPropertySet2() {
            string multiLineString = """
                                     property "Name" {
                                     	set = function(self, name)  self.__name = name end,
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.ReadPloopProperty();
            Assert.Pass(expr.ToString());
        }
        
        [Test]
        public void PloopPropertyField() {
            string multiLineString = """
                                     property "Name" {
                                     	field = "__name",
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.ReadPloopProperty();
            Assert.Pass("");
        }
        
        [Test]
        public void PloopPropertyDefaultValue() {
            string multiLineString = """
                                     property "Name" {
                                     	default = "anonymous",
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.ReadPloopProperty();
            Assert.Pass(expr.ToString());
        }
        
        [Test]
        public void PloopPropertyDefaultValue2() {
            string multiLineString = """
                                     property "Name" {
                                     	default = function(self) return math.random(100) end 
                                     }
                                     """;
            var tokenizer = new Tokenizer(multiLineString);
            var parser = new Parser(tokenizer);
            var expr = parser.ReadPloopProperty();
            Assert.True(true);
        }
        
    }
}
