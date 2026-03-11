using Fennec.App.Validators;
using Xunit;

namespace Fennec.App.Tests.Validators;

public class UsernameFormatAttributeTests
{
    private readonly UsernameFormatAttribute _validator = new();

    [Theory]
    [InlineData("user@fennec.chat")]
    [InlineData("user@localhost:5176")]
    [InlineData("alice@server.com")]
    public void IsValid_ReturnsTrueForValidFormatWithoutScheme(string value)
    {
        // Act
        var result = _validator.IsValid(value);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("user@http://fennec.chat")]
    [InlineData("user@https://localhost:5176")]
    [InlineData("user")]
    [InlineData("user@")]
    [InlineData("@fennec.chat")]
    [InlineData("user@@fennec.chat")]
    public void IsValid_ReturnsFalseForInvalidFormatOrScheme(string value)
    {
        // Act
        var result = _validator.IsValid(value);

        // Assert
        Assert.False(result);
    }
}
