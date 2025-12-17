using FluentAssertions;
using Multiboxer.Core.Layout;
using Multiboxer.Core.Window;
using Xunit;

namespace Multiboxer.Tests.Layout;

/// <summary>
/// Tests for WindowRegion
/// </summary>
public class WindowRegionTests
{
    [Fact]
    public void FullScreen_ShouldReturnPercentageBased100Percent()
    {
        // Act
        var region = WindowRegion.FullScreen;

        // Assert
        region.X.Should().Be(0);
        region.Y.Should().Be(0);
        region.Width.Should().Be(100);
        region.Height.Should().Be(100);
        region.UsePercentage.Should().BeTrue();
    }

    [Fact]
    public void GetAbsoluteValues_WithPixelValues_ShouldReturnSame()
    {
        // Arrange
        var region = new WindowRegion
        {
            X = 100,
            Y = 200,
            Width = 800,
            Height = 600,
            UsePercentage = false
        };
        var bounds = new Rectangle(0, 0, 1920, 1080);

        // Act
        var (x, y, w, h) = region.GetAbsoluteValues(bounds);

        // Assert
        x.Should().Be(100);
        y.Should().Be(200);
        w.Should().Be(800);
        h.Should().Be(600);
    }

    [Fact]
    public void GetAbsoluteValues_WithPercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var region = new WindowRegion
        {
            X = 0,
            Y = 0,
            Width = 50,
            Height = 50,
            UsePercentage = true
        };
        var bounds = new Rectangle(0, 0, 1920, 1080);

        // Act
        var (x, y, w, h) = region.GetAbsoluteValues(bounds);

        // Assert
        x.Should().Be(0);
        y.Should().Be(0);
        w.Should().Be(960);  // 50% of 1920
        h.Should().Be(540);  // 50% of 1080
    }

    [Fact]
    public void GetAbsoluteValues_WithPercentageOffset_ShouldIncludeMonitorPosition()
    {
        // Arrange
        var region = new WindowRegion
        {
            X = 25,
            Y = 25,
            Width = 50,
            Height = 50,
            UsePercentage = true
        };
        // Monitor at position 1920, 0 (secondary monitor to the right)
        var bounds = new Rectangle(1920, 0, 1920, 1080);

        // Act
        var (x, y, w, h) = region.GetAbsoluteValues(bounds);

        // Assert
        x.Should().Be(1920 + 480);  // Monitor X + 25% of width
        y.Should().Be(0 + 270);      // Monitor Y + 25% of height
        w.Should().Be(960);          // 50% of 1920
        h.Should().Be(540);          // 50% of 1080
    }

    [Fact]
    public void GetAbsoluteValues_With100Percent_ShouldMatchMonitorSize()
    {
        // Arrange
        var region = WindowRegion.FullScreen;
        var bounds = new Rectangle(0, 0, 2560, 1440);

        // Act
        var (x, y, w, h) = region.GetAbsoluteValues(bounds);

        // Assert
        x.Should().Be(0);
        y.Should().Be(0);
        w.Should().Be(2560);
        h.Should().Be(1440);
    }

    [Fact]
    public void Constructor_DefaultValues_ShouldBeZero()
    {
        // Arrange & Act
        var region = new WindowRegion();

        // Assert
        region.X.Should().Be(0);
        region.Y.Should().Be(0);
        region.Width.Should().Be(0);
        region.Height.Should().Be(0);
        region.UsePercentage.Should().BeFalse();
        region.MonitorIndex.Should().Be(-1);
    }
}
