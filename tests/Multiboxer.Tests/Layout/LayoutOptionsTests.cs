using FluentAssertions;
using Multiboxer.Core.Layout;
using Xunit;

namespace Multiboxer.Tests.Layout;

/// <summary>
/// Tests for LayoutOptions
/// </summary>
public class LayoutOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new LayoutOptions();

        // Assert
        options.SwapOnActivate.Should().BeTrue();
        options.SwapOnHotkeyFocused.Should().BeTrue();
        options.LeaveHole.Should().BeFalse();
        options.AvoidTaskbar.Should().BeTrue();
        options.MakeBorderless.Should().BeTrue();
        options.RescaleWindows.Should().BeTrue();
        options.FocusFollowsMouse.Should().BeFalse();
        options.MonitorIndex.Should().Be(-1); // Auto-detect
    }

    [Fact]
    public void MonitorIndex_MinusOne_ShouldMeanAutoDetect()
    {
        // Arrange
        var options = new LayoutOptions { MonitorIndex = -1 };

        // Assert
        options.MonitorIndex.Should().Be(-1);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var options = new LayoutOptions();

        // Act
        options.SwapOnActivate = false;
        options.MakeBorderless = false;
        options.MonitorIndex = 2;

        // Assert
        options.SwapOnActivate.Should().BeFalse();
        options.MakeBorderless.Should().BeFalse();
        options.MonitorIndex.Should().Be(2);
    }
}
