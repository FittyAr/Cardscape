using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("alice@example.com")]
    [InlineData("  ALICE@Example.COM  ")]   // whitespace + case are canonicalised
    [InlineData("a.b+tag@sub.example.co")]
    public void Create_WithValidEmail_ReturnsCanonicalisedSuccess(string input)
    {
        var result = EmailAddress.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingEmail_ReturnsRequiredValidationError(string? input)
    {
        var result = EmailAddress.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("members.email.required");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("two@@signs.com")]
    [InlineData("spaces in@email.com")]
    public void Create_WithMalformedEmail_ReturnsInvalidFormatError(string input)
    {
        var result = EmailAddress.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.email.invalid");
    }

    [Fact]
    public void Create_WithExactlyMaxLengthEmail_Succeeds()
    {
        // 64-char local + "@example.com" = 76 chars, well under MaxLength (254).
        var local = new string('a', 64);
        var input = $"{local}@example.com";

        var result = EmailAddress.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Length.Should().BeLessThanOrEqualTo(EmailAddress.MaxLength);
    }

    [Fact]
    public void Create_WithEmailExceedingMaxLength_ReturnsTooLongError()
    {
        var local = new string('a', 250);  // forces total > MaxLength
        var input = $"{local}@example.com";

        var result = EmailAddress.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.email.too_long");
    }

    [Fact]
    public void ToString_ReturnsRawValue()
    {
        var email = EmailAddress.Create("Alice@Example.com").Value;

        email.ToString().Should().Be("alice@example.com");
    }
}
