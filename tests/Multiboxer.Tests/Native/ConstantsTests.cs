using FluentAssertions;
using Multiboxer.Native;
using Xunit;

namespace Multiboxer.Tests.Native;

/// <summary>
/// Tests for Windows API constants to ensure correct values
/// </summary>
public class ConstantsTests
{
    #region User32 Hook Constants

    [Fact]
    public void WH_KEYBOARD_LL_ShouldBeCorrect()
    {
        User32.WH_KEYBOARD_LL.Should().Be(13);
    }

    [Theory]
    [InlineData(0x0100)] // WM_KEYDOWN
    [InlineData(0x0101)] // WM_KEYUP
    [InlineData(0x0104)] // WM_SYSKEYDOWN
    [InlineData(0x0105)] // WM_SYSKEYUP
    public void KeyboardMessages_ShouldBeCorrect(int expected)
    {
        var messages = new[] { User32.WM_KEYDOWN, User32.WM_KEYUP, User32.WM_SYSKEYDOWN, User32.WM_SYSKEYUP };
        messages.Should().Contain(expected);
    }

    #endregion

    #region User32 Window Message Constants

    [Fact]
    public void WM_HOTKEY_ShouldBeCorrect()
    {
        User32.WM_HOTKEY.Should().Be(0x0312);
    }

    [Fact]
    public void WM_CLOSE_ShouldBeCorrect()
    {
        User32.WM_CLOSE.Should().Be(0x0010);
    }

    [Fact]
    public void WM_SYSCOMMAND_ShouldBeCorrect()
    {
        User32.WM_SYSCOMMAND.Should().Be(0x0112);
    }

    [Theory]
    [InlineData(0xF020)] // SC_MINIMIZE
    [InlineData(0xF030)] // SC_MAXIMIZE
    [InlineData(0xF120)] // SC_RESTORE
    public void SysCommands_ShouldBeCorrect(int expected)
    {
        var commands = new[] { User32.SC_MINIMIZE, User32.SC_MAXIMIZE, User32.SC_RESTORE };
        commands.Should().Contain(expected);
    }

    #endregion

    #region User32 System Metrics

    [Fact]
    public void SM_CXSCREEN_ShouldBeCorrect()
    {
        User32.SM_CXSCREEN.Should().Be(0);
    }

    [Fact]
    public void SM_CYSCREEN_ShouldBeCorrect()
    {
        User32.SM_CYSCREEN.Should().Be(1);
    }

    #endregion
}
