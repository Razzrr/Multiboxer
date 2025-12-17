using FluentAssertions;
using Multiboxer.Core.Window;
using Multiboxer.Native;
using Xunit;

namespace Multiboxer.Tests.Window;

/// <summary>
/// Tests for Rectangle struct
/// </summary>
public class RectangleTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Act
        var rect = new Rectangle(10, 20, 100, 200);

        // Assert
        rect.X.Should().Be(10);
        rect.Y.Should().Be(20);
        rect.Width.Should().Be(100);
        rect.Height.Should().Be(200);
    }

    [Fact]
    public void Constructor_EdgeAccessors_ShouldBeCorrect()
    {
        // Arrange
        var rect = new Rectangle(10, 20, 100, 200);

        // Assert
        rect.Left.Should().Be(10);
        rect.Top.Should().Be(20);
        rect.Right.Should().Be(110);  // X + Width
        rect.Bottom.Should().Be(220); // Y + Height
    }

    #endregion

    #region FromRECT Tests

    [Fact]
    public void FromRECT_ShouldConvertCorrectly()
    {
        // Arrange
        var nativeRect = new RECT(100, 200, 500, 700);

        // Act
        var rect = Rectangle.FromRECT(nativeRect);

        // Assert
        rect.X.Should().Be(100);
        rect.Y.Should().Be(200);
        rect.Width.Should().Be(400);  // Right - Left
        rect.Height.Should().Be(500); // Bottom - Top
    }

    [Fact]
    public void FromRECT_WithZeroRect_ShouldReturnZero()
    {
        // Arrange
        var nativeRect = new RECT(0, 0, 0, 0);

        // Act
        var rect = Rectangle.FromRECT(nativeRect);

        // Assert
        rect.X.Should().Be(0);
        rect.Y.Should().Be(0);
        rect.Width.Should().Be(0);
        rect.Height.Should().Be(0);
    }

    #endregion

    #region Contains Tests

    [Fact]
    public void Contains_PointInside_ShouldReturnTrue()
    {
        // Arrange
        var rect = new Rectangle(0, 0, 100, 100);

        // Act & Assert
        rect.Contains(50, 50).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOnTopLeftCorner_ShouldReturnTrue()
    {
        // Arrange
        var rect = new Rectangle(0, 0, 100, 100);

        // Act & Assert
        rect.Contains(0, 0).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOnBottomRightCorner_ShouldReturnFalse()
    {
        // Right and Bottom are exclusive
        var rect = new Rectangle(0, 0, 100, 100);

        // Act & Assert
        rect.Contains(100, 100).Should().BeFalse();
    }

    [Fact]
    public void Contains_PointOutside_ShouldReturnFalse()
    {
        // Arrange
        var rect = new Rectangle(0, 0, 100, 100);

        // Act & Assert
        rect.Contains(150, 150).Should().BeFalse();
        rect.Contains(-10, 50).Should().BeFalse();
        rect.Contains(50, -10).Should().BeFalse();
    }

    [Fact]
    public void Contains_PointJustInsideEdge_ShouldReturnTrue()
    {
        // Arrange
        var rect = new Rectangle(10, 10, 100, 100);

        // Act & Assert
        rect.Contains(10, 10).Should().BeTrue();   // Top-left
        rect.Contains(109, 10).Should().BeTrue();  // Just inside right
        rect.Contains(10, 109).Should().BeTrue();  // Just inside bottom
    }

    #endregion

    #region Intersects Tests

    [Fact]
    public void Intersects_OverlappingRects_ShouldReturnTrue()
    {
        // Arrange
        var rect1 = new Rectangle(0, 0, 100, 100);
        var rect2 = new Rectangle(50, 50, 100, 100);

        // Act & Assert
        rect1.Intersects(rect2).Should().BeTrue();
        rect2.Intersects(rect1).Should().BeTrue();
    }

    [Fact]
    public void Intersects_NonOverlappingRects_ShouldReturnFalse()
    {
        // Arrange
        var rect1 = new Rectangle(0, 0, 100, 100);
        var rect2 = new Rectangle(200, 200, 100, 100);

        // Act & Assert
        rect1.Intersects(rect2).Should().BeFalse();
        rect2.Intersects(rect1).Should().BeFalse();
    }

    [Fact]
    public void Intersects_TouchingRects_ShouldReturnFalse()
    {
        // Arrange - rects that touch at edges but don't overlap
        var rect1 = new Rectangle(0, 0, 100, 100);
        var rect2 = new Rectangle(100, 0, 100, 100); // Starts exactly where rect1 ends

        // Act & Assert
        rect1.Intersects(rect2).Should().BeFalse();
    }

    [Fact]
    public void Intersects_ContainedRect_ShouldReturnTrue()
    {
        // Arrange
        var outer = new Rectangle(0, 0, 200, 200);
        var inner = new Rectangle(50, 50, 50, 50);

        // Act & Assert
        outer.Intersects(inner).Should().BeTrue();
        inner.Intersects(outer).Should().BeTrue();
    }

    [Fact]
    public void Intersects_SameRect_ShouldReturnTrue()
    {
        // Arrange
        var rect = new Rectangle(0, 0, 100, 100);

        // Act & Assert
        rect.Intersects(rect).Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var rect = new Rectangle(10, 20, 100, 200);

        // Act
        var str = rect.ToString();

        // Assert
        str.Should().Contain("10");
        str.Should().Contain("20");
        str.Should().Contain("100");
        str.Should().Contain("200");
    }

    #endregion
}
