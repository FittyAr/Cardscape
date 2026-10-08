using Cardscape.Web.Shared;

namespace Cardscape.UnitTests.Web;

public sealed class AuditCsvTests
{
    [Theory]
    [InlineData("Ada", "Ada")]
    [InlineData("Lovelace, Ada", "\"Lovelace, Ada\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData(null, "")]
    public void Escape_QuotesSeparatorsAndNeutralisesFormulas(string? value, string expected) =>
        AuditCsv.Escape(value).Should().Be(expected);
}
