using FluentAssertions;
using Multiboxer.Core.Window;
using Xunit;

namespace Multiboxer.Tests.Window;

/// <summary>
/// Tests for MonitorManager - requires running on Windows with at least one monitor
/// </summary>
public class MonitorManagerTests
{
    [Fact]
    public void GetAllMonitors_ShouldReturnAtLeastOneMonitor()
    {
        // Act
        var monitors = MonitorManager.GetAllMonitors();

        // Assert
        monitors.Should().NotBeNull();
        monitors.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void GetAllMonitors_FirstMonitor_ShouldHaveValidProperties()
    {
        // Act
        var monitors = MonitorManager.GetAllMonitors();
        var firstMonitor = monitors.First();

        // Assert
        firstMonitor.Handle.Should().NotBe(IntPtr.Zero);
        firstMonitor.DeviceName.Should().NotBeNullOrEmpty();
        firstMonitor.Bounds.Width.Should().BeGreaterThan(0);
        firstMonitor.Bounds.Height.Should().BeGreaterThan(0);
        firstMonitor.WorkingArea.Width.Should().BeGreaterThan(0);
        firstMonitor.WorkingArea.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetPrimaryMonitor_ShouldReturnPrimary()
    {
        // Act
        var primary = MonitorManager.GetPrimaryMonitor();

        // Assert
        primary.Should().NotBeNull();
        primary!.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void GetAllMonitors_ShouldHaveExactlyOnePrimary()
    {
        // Act
        var monitors = MonitorManager.GetAllMonitors();
        var primaryCount = monitors.Count(m => m.IsPrimary);

        // Assert
        primaryCount.Should().Be(1);
    }

    [Fact]
    public void GetAllMonitors_IndicesShouldBeSequential()
    {
        // Act
        var monitors = MonitorManager.GetAllMonitors();

        // Assert
        for (int i = 0; i < monitors.Count; i++)
        {
            monitors[i].Index.Should().Be(i);
        }
    }

    [Fact]
    public void GetVirtualScreenBounds_ShouldContainAllMonitors()
    {
        // Act
        var virtualBounds = MonitorManager.GetVirtualScreenBounds();
        var monitors = MonitorManager.GetAllMonitors();

        // Assert
        virtualBounds.Width.Should().BeGreaterThan(0);
        virtualBounds.Height.Should().BeGreaterThan(0);

        foreach (var monitor in monitors)
        {
            // Each monitor's bounds should be within the virtual screen
            virtualBounds.Left.Should().BeLessThanOrEqualTo(monitor.Bounds.Left);
            virtualBounds.Top.Should().BeLessThanOrEqualTo(monitor.Bounds.Top);
            virtualBounds.Right.Should().BeGreaterThanOrEqualTo(monitor.Bounds.Right);
            virtualBounds.Bottom.Should().BeGreaterThanOrEqualTo(monitor.Bounds.Bottom);
        }
    }

    [Fact]
    public void MonitorInfo_Accessors_ShouldMatchBounds()
    {
        // Arrange
        var monitor = MonitorManager.GetPrimaryMonitor()!;

        // Assert
        monitor.X.Should().Be(monitor.Bounds.X);
        monitor.Y.Should().Be(monitor.Bounds.Y);
        monitor.Width.Should().Be(monitor.Bounds.Width);
        monitor.Height.Should().Be(monitor.Bounds.Height);
    }

    [Fact]
    public void MonitorInfo_WorkingArea_ShouldBeSmallerOrEqualToBounds()
    {
        // Arrange
        var monitors = MonitorManager.GetAllMonitors();

        // Assert
        foreach (var monitor in monitors)
        {
            // Working area excludes taskbar so should be <= bounds
            monitor.WorkingArea.Width.Should().BeLessThanOrEqualTo(monitor.Bounds.Width);
            monitor.WorkingArea.Height.Should().BeLessThanOrEqualTo(monitor.Bounds.Height);
        }
    }

    [Fact]
    public void MonitorInfo_ToString_ShouldContainDeviceNameAndResolution()
    {
        // Arrange
        var monitor = MonitorManager.GetPrimaryMonitor()!;

        // Act
        var str = monitor.ToString();

        // Assert
        str.Should().Contain(monitor.DeviceName);
        str.Should().Contain(monitor.Width.ToString());
        str.Should().Contain(monitor.Height.ToString());
        str.Should().Contain("[Primary]");
    }

    [Fact]
    public void GetMonitorForPoint_WithValidPoint_ShouldReturnMonitor()
    {
        // Arrange
        var primary = MonitorManager.GetPrimaryMonitor()!;
        var centerX = primary.X + primary.Width / 2;
        var centerY = primary.Y + primary.Height / 2;

        // Act
        var monitor = MonitorManager.GetMonitorForPoint(centerX, centerY);

        // Assert
        monitor.Should().NotBeNull();
        monitor!.Bounds.Contains(centerX, centerY).Should().BeTrue();
    }
}
