using FluentAssertions;
using Multiboxer.Native;
using Xunit;

namespace Multiboxer.Tests.Native;

/// <summary>
/// Tests for native enum definitions to ensure correct values for Windows API compatibility
/// </summary>
public class EnumTests
{
    #region SetWindowPosFlags Tests

    [Theory]
    [InlineData(SetWindowPosFlags.SWP_NOSIZE, 0x0001)]
    [InlineData(SetWindowPosFlags.SWP_NOMOVE, 0x0002)]
    [InlineData(SetWindowPosFlags.SWP_NOZORDER, 0x0004)]
    [InlineData(SetWindowPosFlags.SWP_NOREDRAW, 0x0008)]
    [InlineData(SetWindowPosFlags.SWP_NOACTIVATE, 0x0010)]
    [InlineData(SetWindowPosFlags.SWP_FRAMECHANGED, 0x0020)]
    [InlineData(SetWindowPosFlags.SWP_SHOWWINDOW, 0x0040)]
    [InlineData(SetWindowPosFlags.SWP_HIDEWINDOW, 0x0080)]
    [InlineData(SetWindowPosFlags.SWP_ASYNCWINDOWPOS, 0x4000)]
    public void SetWindowPosFlags_ShouldHaveCorrectValues(SetWindowPosFlags flag, uint expected)
    {
        ((uint)flag).Should().Be(expected);
    }

    [Fact]
    public void SetWindowPosFlags_ShouldCombineCorrectly()
    {
        var combined = SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER;
        ((uint)combined).Should().Be(0x0007);
    }

    #endregion

    #region ShowWindowCommand Tests

    [Theory]
    [InlineData(ShowWindowCommand.SW_HIDE, 0)]
    [InlineData(ShowWindowCommand.SW_SHOWNORMAL, 1)]
    [InlineData(ShowWindowCommand.SW_SHOWMINIMIZED, 2)]
    [InlineData(ShowWindowCommand.SW_SHOWMAXIMIZED, 3)]
    [InlineData(ShowWindowCommand.SW_SHOWNOACTIVATE, 4)]
    [InlineData(ShowWindowCommand.SW_SHOW, 5)]
    [InlineData(ShowWindowCommand.SW_MINIMIZE, 6)]
    [InlineData(ShowWindowCommand.SW_RESTORE, 9)]
    public void ShowWindowCommand_ShouldHaveCorrectValues(ShowWindowCommand cmd, int expected)
    {
        ((int)cmd).Should().Be(expected);
    }

    #endregion

    #region ModifierKeys Tests

    [Theory]
    [InlineData(ModifierKeys.None, 0x0000)]
    [InlineData(ModifierKeys.Alt, 0x0001)]
    [InlineData(ModifierKeys.Control, 0x0002)]
    [InlineData(ModifierKeys.Shift, 0x0004)]
    [InlineData(ModifierKeys.Win, 0x0008)]
    [InlineData(ModifierKeys.NoRepeat, 0x4000)]
    public void ModifierKeys_ShouldHaveCorrectValues(ModifierKeys key, uint expected)
    {
        ((uint)key).Should().Be(expected);
    }

    [Fact]
    public void ModifierKeys_ShouldCombineCorrectly()
    {
        var combined = ModifierKeys.Control | ModifierKeys.Alt;
        ((uint)combined).Should().Be(0x0003);

        var withShift = ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt;
        ((uint)withShift).Should().Be(0x0007);
    }

    #endregion

    #region WindowStyles Tests

    [Theory]
    [InlineData(WindowStyles.WS_OVERLAPPED, 0x00000000)]
    [InlineData(WindowStyles.WS_POPUP, 0x80000000)]
    [InlineData(WindowStyles.WS_VISIBLE, 0x10000000)]
    [InlineData(WindowStyles.WS_CAPTION, 0x00C00000)]
    [InlineData(WindowStyles.WS_BORDER, 0x00800000)]
    [InlineData(WindowStyles.WS_THICKFRAME, 0x00040000)]
    [InlineData(WindowStyles.WS_SYSMENU, 0x00080000)]
    public void WindowStyles_ShouldHaveCorrectValues(WindowStyles style, uint expected)
    {
        ((uint)style).Should().Be(expected);
    }

    #endregion

    #region WindowStylesEx Tests

    [Theory]
    [InlineData(WindowStylesEx.WS_EX_TOPMOST, 0x00000008)]
    [InlineData(WindowStylesEx.WS_EX_TRANSPARENT, 0x00000020)]
    [InlineData(WindowStylesEx.WS_EX_TOOLWINDOW, 0x00000080)]
    [InlineData(WindowStylesEx.WS_EX_APPWINDOW, 0x00040000)]
    [InlineData(WindowStylesEx.WS_EX_LAYERED, 0x00080000)]
    [InlineData(WindowStylesEx.WS_EX_NOACTIVATE, 0x08000000)]
    public void WindowStylesEx_ShouldHaveCorrectValues(WindowStylesEx style, uint expected)
    {
        ((uint)style).Should().Be(expected);
    }

    #endregion

    #region MonitorDefaultTo Tests

    [Theory]
    [InlineData(MonitorDefaultTo.MONITOR_DEFAULTTONULL, 0)]
    [InlineData(MonitorDefaultTo.MONITOR_DEFAULTTOPRIMARY, 1)]
    [InlineData(MonitorDefaultTo.MONITOR_DEFAULTTONEAREST, 2)]
    public void MonitorDefaultTo_ShouldHaveCorrectValues(MonitorDefaultTo flag, uint expected)
    {
        ((uint)flag).Should().Be(expected);
    }

    #endregion

    #region InputType Tests

    [Theory]
    [InlineData(InputType.INPUT_MOUSE, 0)]
    [InlineData(InputType.INPUT_KEYBOARD, 1)]
    [InlineData(InputType.INPUT_HARDWARE, 2)]
    public void InputType_ShouldHaveCorrectValues(InputType type, uint expected)
    {
        ((uint)type).Should().Be(expected);
    }

    #endregion

    #region ProcessPriorityClass Tests

    [Theory]
    [InlineData(ProcessPriorityClass.Idle, 0x00000040)]
    [InlineData(ProcessPriorityClass.BelowNormal, 0x00004000)]
    [InlineData(ProcessPriorityClass.Normal, 0x00000020)]
    [InlineData(ProcessPriorityClass.AboveNormal, 0x00008000)]
    [InlineData(ProcessPriorityClass.High, 0x00000080)]
    [InlineData(ProcessPriorityClass.RealTime, 0x00000100)]
    public void ProcessPriorityClass_ShouldHaveCorrectValues(ProcessPriorityClass priority, uint expected)
    {
        ((uint)priority).Should().Be(expected);
    }

    #endregion

    #region ProcessAccessFlags Tests

    [Theory]
    [InlineData(ProcessAccessFlags.Terminate, 0x0001)]
    [InlineData(ProcessAccessFlags.CreateThread, 0x0002)]
    [InlineData(ProcessAccessFlags.QueryInformation, 0x0400)]
    [InlineData(ProcessAccessFlags.QueryLimitedInformation, 0x1000)]
    [InlineData(ProcessAccessFlags.SetInformation, 0x0200)]
    [InlineData(ProcessAccessFlags.SetQuota, 0x0100)]
    public void ProcessAccessFlags_ShouldHaveCorrectValues(ProcessAccessFlags flag, uint expected)
    {
        ((uint)flag).Should().Be(expected);
    }

    #endregion

    #region WindowStyleConstants Tests

    [Fact]
    public void WindowStyleConstants_GWL_STYLE_ShouldBeCorrect()
    {
        WindowStyleConstants.GWL_STYLE.Should().Be(-16);
    }

    [Fact]
    public void WindowStyleConstants_GWL_EXSTYLE_ShouldBeCorrect()
    {
        WindowStyleConstants.GWL_EXSTYLE.Should().Be(-20);
    }

    [Fact]
    public void WindowStyleConstants_MONITORINFOF_PRIMARY_ShouldBeCorrect()
    {
        WindowStyleConstants.MONITORINFOF_PRIMARY.Should().Be(1u);
    }

    #endregion
}
