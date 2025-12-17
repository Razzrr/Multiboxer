using FluentAssertions;
using Multiboxer.Core.Layout;
using Multiboxer.Core.Window;
using Xunit;

namespace Multiboxer.Tests.Layout;

/// <summary>
/// Tests for layout strategies (Horizontal, Vertical, Custom)
/// </summary>
public class LayoutStrategyTests
{
    private static MonitorInfo CreateMockMonitor(int width = 1920, int height = 1080)
    {
        return new MonitorInfo
        {
            Handle = IntPtr.Zero,
            DeviceName = "TEST",
            IsPrimary = true,
            Bounds = new Rectangle(0, 0, width, height),
            WorkingArea = new Rectangle(0, 0, width, height),
            Index = 0
        };
    }

    private static LayoutOptions DefaultOptions => new LayoutOptions();

    #region HorizontalLayout Tests

    [Fact]
    public void HorizontalLayout_Name_ShouldBeHorizontal()
    {
        // Arrange
        var layout = new HorizontalLayout();

        // Assert
        layout.Name.Should().Be("Horizontal");
    }

    [Fact]
    public void HorizontalLayout_Description_ShouldNotBeEmpty()
    {
        // Arrange
        var layout = new HorizontalLayout();

        // Assert
        layout.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HorizontalLayout_CalculateRegions_WithOneSlot_ShouldReturnFullScreen()
    {
        // Arrange
        var layout = new HorizontalLayout();
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(1, monitor, options);

        // Assert
        regions.Should().HaveCount(1);
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var (x, y, w, h) = regions[0].GetAbsoluteValues(bounds);
        w.Should().Be(1920);
        h.Should().Be(1080);
    }

    [Fact]
    public void HorizontalLayout_CalculateRegions_WithTwoSlots_ShouldHaveMainAndSecondary()
    {
        // Arrange
        var layout = new HorizontalLayout();
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(2, monitor, options);

        // Assert
        regions.Should().HaveCount(2);

        // Main window should be larger
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var (mx, my, mw, mh) = regions[0].GetAbsoluteValues(bounds);
        var (sx, sy, sw, sh) = regions[1].GetAbsoluteValues(bounds);

        mh.Should().BeGreaterThan(sh); // Main is taller
        (mw * mh).Should().BeGreaterThan(sw * sh); // Main has more area
    }

    [Fact]
    public void HorizontalLayout_CalculateRegions_SecondarySlots_ShouldBeHorizontallyArranged()
    {
        // Arrange
        var layout = new HorizontalLayout();
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(4, monitor, options);

        // Assert
        regions.Should().HaveCount(4);

        // Secondary windows (1, 2, 3) should have same Y but different X
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var (_, y1, _, _) = regions[1].GetAbsoluteValues(bounds);
        var (_, y2, _, _) = regions[2].GetAbsoluteValues(bounds);
        var (_, y3, _, _) = regions[3].GetAbsoluteValues(bounds);

        y1.Should().Be(y2);
        y2.Should().Be(y3);
    }

    #endregion

    #region VerticalLayout Tests

    [Fact]
    public void VerticalLayout_Name_ShouldBeVertical()
    {
        // Arrange
        var layout = new VerticalLayout();

        // Assert
        layout.Name.Should().Be("Vertical");
    }

    [Fact]
    public void VerticalLayout_Description_ShouldNotBeEmpty()
    {
        // Arrange
        var layout = new VerticalLayout();

        // Assert
        layout.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VerticalLayout_CalculateRegions_WithOneSlot_ShouldReturnFullScreen()
    {
        // Arrange
        var layout = new VerticalLayout();
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(1, monitor, options);

        // Assert
        regions.Should().HaveCount(1);
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var (x, y, w, h) = regions[0].GetAbsoluteValues(bounds);
        w.Should().Be(1920);
        h.Should().Be(1080);
    }

    [Fact]
    public void VerticalLayout_CalculateRegions_SecondarySlots_ShouldBeVerticallyArranged()
    {
        // Arrange
        var layout = new VerticalLayout();
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(4, monitor, options);

        // Assert
        regions.Should().HaveCount(4);

        // Secondary windows (1, 2, 3) should have same X but different Y
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var (x1, _, _, _) = regions[1].GetAbsoluteValues(bounds);
        var (x2, _, _, _) = regions[2].GetAbsoluteValues(bounds);
        var (x3, _, _, _) = regions[3].GetAbsoluteValues(bounds);

        x1.Should().Be(x2);
        x2.Should().Be(x3);
    }

    #endregion

    #region CustomLayout Tests

    [Fact]
    public void CustomLayout_CreatePiPLayout_ShouldHaveMainAndPipRegions()
    {
        // Arrange & Act
        var layout = CustomLayout.CreatePiPLayout("TestPiP", 3);

        // Assert
        layout.Should().NotBeNull();
        layout.MainRegion.Should().NotBeNull();
        layout.Regions.Should().HaveCount(3); // 3 PiP slots
    }

    [Fact]
    public void CustomLayout_CreateGridLayout_ShouldHaveCorrectNumberOfRegions()
    {
        // Arrange & Act
        var layout = CustomLayout.CreateGridLayout("TestGrid", 3, 2); // 3 columns, 2 rows = 6 cells

        // Assert
        layout.Should().NotBeNull();
        layout.Regions.Should().HaveCount(5); // 5 background + 1 main = 6 total
    }

    [Fact]
    public void CustomLayout_Name_ShouldBeSet()
    {
        // Arrange
        var layout = new CustomLayout { Name = "MyLayout" };

        // Assert
        layout.Name.Should().Be("MyLayout");
    }

    [Fact]
    public void CustomLayout_CalculateRegions_ShouldReturnAllRegions()
    {
        // Arrange
        var layout = new CustomLayout
        {
            Name = "Test",
            MainRegion = WindowRegion.FullScreen,
            Regions = new List<WindowRegion>
            {
                new() { X = 0, Y = 0, Width = 50, Height = 50, UsePercentage = true },
                new() { X = 50, Y = 0, Width = 50, Height = 50, UsePercentage = true }
            }
        };
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(3, monitor, options);

        // Assert
        regions.Should().HaveCount(3); // Main + 2 background
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void HorizontalLayout_CalculateRegions_WithZeroSlots_ShouldReturnEmpty()
    {
        // Arrange
        var layout = new HorizontalLayout();
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(0, monitor, options);

        // Assert
        regions.Should().BeEmpty();
    }

    [Fact]
    public void VerticalLayout_CalculateRegions_WithManySlots_ShouldNotOverflow()
    {
        // Arrange
        var layout = new VerticalLayout();
        var monitor = CreateMockMonitor();
        var options = DefaultOptions;

        // Act
        var regions = layout.CalculateRegions(10, monitor, options);

        // Assert
        regions.Should().HaveCount(10);

        // All regions should have positive dimensions
        var bounds = new Rectangle(0, 0, 1920, 1080);
        foreach (var region in regions)
        {
            var (x, y, w, h) = region.GetAbsoluteValues(bounds);
            x.Should().BeGreaterOrEqualTo(0);
            y.Should().BeGreaterOrEqualTo(0);
            w.Should().BeGreaterThan(0);
            h.Should().BeGreaterThan(0);
        }
    }

    #endregion
}
