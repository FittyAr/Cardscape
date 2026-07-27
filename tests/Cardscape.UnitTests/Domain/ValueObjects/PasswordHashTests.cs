using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class PasswordHashTests
{
    [Fact]
    public void FromHashed_WithNonEmptyString_ReturnsSuccess()
    {
        var result = PasswordHash.FromHashed("v1.salt123.hash456");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("v1.salt123.hash456");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromHashed_WithEmptyOrWhitespace_ReturnsEmptyError(string? input)
    {
        var result = PasswordHash.FromHashed(input!);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("members.password.empty");
    }

    [Fact]
    public void VersionPrefix_IsV1()
    {
        PasswordHash.VersionPrefix.Should().Be("v1");
    }

    [Fact]
    public void ToString_ReturnsRawValue()
    {
        var hash = PasswordHash.FromHashed("v1.abc.def").Value;

        hash.ToString().Should().Be("v1.abc.def");
    }
}
