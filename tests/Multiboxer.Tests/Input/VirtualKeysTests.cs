using FluentAssertions;
using Multiboxer.Core.Input;
using Xunit;

namespace Multiboxer.Tests.Input;

/// <summary>
/// Tests for VirtualKeys constants and methods
/// </summary>
public class VirtualKeysTests
{
    #region Key Code Tests

    [Theory]
    [InlineData(VirtualKeys.VK_F1, 0x70)]
    [InlineData(VirtualKeys.VK_F2, 0x71)]
    [InlineData(VirtualKeys.VK_F12, 0x7B)]
    public void FunctionKeys_ShouldHaveCorrectValues(uint key, uint expected)
    {
        key.Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKeys.VK_0, 0x30)]
    [InlineData(VirtualKeys.VK_1, 0x31)]
    [InlineData(VirtualKeys.VK_9, 0x39)]
    public void NumberKeys_ShouldHaveCorrectValues(uint key, uint expected)
    {
        key.Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKeys.VK_A, 0x41)]
    [InlineData(VirtualKeys.VK_Z, 0x5A)]
    public void LetterKeys_ShouldHaveCorrectValues(uint key, uint expected)
    {
        key.Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKeys.VK_ESCAPE, 0x1B)]
    [InlineData(VirtualKeys.VK_RETURN, 0x0D)]
    [InlineData(VirtualKeys.VK_SPACE, 0x20)]
    [InlineData(VirtualKeys.VK_TAB, 0x09)]
    public void SpecialKeys_ShouldHaveCorrectValues(uint key, uint expected)
    {
        key.Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKeys.VK_NUMPAD0, 0x60)]
    [InlineData(VirtualKeys.VK_NUMPAD9, 0x69)]
    public void NumpadKeys_ShouldHaveCorrectValues(uint key, uint expected)
    {
        key.Should().Be(expected);
    }

    #endregion

    #region GetKeyName Tests

    [Theory]
    [InlineData(VirtualKeys.VK_F1, "F1")]
    [InlineData(VirtualKeys.VK_F2, "F2")]
    [InlineData(VirtualKeys.VK_F12, "F12")]
    public void GetKeyName_FunctionKeys_ShouldReturnCorrectName(uint keyCode, string expected)
    {
        // Act
        var name = VirtualKeys.GetKeyName(keyCode);

        // Assert
        name.Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKeys.VK_ESCAPE, "Escape")]
    [InlineData(VirtualKeys.VK_RETURN, "Enter")]
    [InlineData(VirtualKeys.VK_SPACE, "Space")]
    [InlineData(VirtualKeys.VK_TAB, "Tab")]
    public void GetKeyName_SpecialKeys_ShouldReturnCorrectName(uint keyCode, string expected)
    {
        // Act
        var name = VirtualKeys.GetKeyName(keyCode);

        // Assert
        name.Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKeys.VK_END, "End")]
    [InlineData(VirtualKeys.VK_HOME, "Home")]
    [InlineData(VirtualKeys.VK_INSERT, "Insert")]
    [InlineData(VirtualKeys.VK_DELETE, "Delete")]
    public void GetKeyName_NavigationKeys_ShouldReturnCorrectName(uint keyCode, string expected)
    {
        // Act
        var name = VirtualKeys.GetKeyName(keyCode);

        // Assert
        name.Should().Be(expected);
    }

    [Fact]
    public void GetKeyName_UnknownKey_ShouldReturnKeyFormat()
    {
        // Arrange
        uint unknownKey = 0xFF;

        // Act
        var name = VirtualKeys.GetKeyName(unknownKey);

        // Assert - format is "Key{hex}" like "KeyFF"
        name.Should().StartWith("Key");
        name.Should().Be("KeyFF");
    }

    #endregion
}
