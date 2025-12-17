using FluentAssertions;
using Xunit;

namespace Multiboxer.Tests.Overlay;

/// <summary>
/// Tests for ThumbnailManager logic.
/// Note: Full integration tests require a WPF application context.
/// These tests verify the core logic without creating actual windows.
/// </summary>
public class ThumbnailManagerTests
{
    #region ShowLabels Property Tests

    [Fact]
    public void ShowLabels_DefaultsToTrue()
    {
        // Arrange & Act
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Assert
        manager.ShowLabels.Should().BeTrue();
    }

    [Fact]
    public void ShowLabels_CanBeSet()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        manager.ShowLabels = false;

        // Assert
        manager.ShowLabels.Should().BeFalse();
    }

    #endregion

    #region ShowBorders Property Tests

    [Fact]
    public void ShowBorders_DefaultsToFalse()
    {
        // Arrange & Act
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Assert
        manager.ShowBorders.Should().BeFalse();
    }

    [Fact]
    public void ShowBorders_CanBeSet()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        manager.ShowBorders = true;

        // Assert
        manager.ShowBorders.Should().BeTrue();
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_OnEmptyManager_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.Clear();

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region HideAll Tests

    [Fact]
    public void HideAll_OnEmptyManager_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.HideAll();

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region ShowAll Tests

    [Fact]
    public void ShowAll_OnEmptyManager_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.ShowAll();

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region SetForegroundSlot Tests

    [Fact]
    public void SetForegroundSlot_WithNoThumbnails_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.SetForegroundSlot(1);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void SetForegroundSlot_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () =>
        {
            manager.SetForegroundSlot(1);
            manager.SetForegroundSlot(2);
            manager.SetForegroundSlot(3);
            manager.SetForegroundSlot(1); // Back to 1
        };

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region HideThumbnail/ShowThumbnail Tests

    [Fact]
    public void HideThumbnail_WithNonexistentSlot_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.HideThumbnail(999);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void ShowThumbnail_WithNonexistentSlot_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.ShowThumbnail(999);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region RemoveThumbnail Tests

    [Fact]
    public void RemoveThumbnail_WithNonexistentSlot_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.RemoveThumbnail(999);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region RefreshAll Tests

    [Fact]
    public void RefreshAll_OnEmptyManager_ShouldNotThrow()
    {
        // Arrange
        var manager = new Multiboxer.Overlay.ThumbnailManager();

        // Act
        var action = () => manager.RefreshAll();

        // Assert
        action.Should().NotThrow();
    }

    #endregion
}
