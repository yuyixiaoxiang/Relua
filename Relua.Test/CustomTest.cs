namespace Relua.Test;

public class Tests
{
    [Test]
    public void FancyTableFunctionCall() {
        var tokenizer = new Tokenizer("self.__rimEntity = EntityUtilLua.CreateDisplayEntity(\"troop_circle_alliance\");");
        var parser = new Parser(tokenizer);
        var expr = parser.ReadExpression() as AST.Node;
        Assert.AreEqual("print({ 1, 2 })", expr.ToString(one_line: true));
    }
}