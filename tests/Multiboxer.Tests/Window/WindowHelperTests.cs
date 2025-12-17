using FluentAssertions;
using Multiboxer.Core.Window;
using Xunit;

namespace Multiboxer.Tests.Window;

/// <summary>
/// Tests for WindowHelper static methods
/// These tests require a Windows environment to run properly
/// </summary>
public class WindowHelperTests
{
    #region Window Query Tests

    [Fact]
    public void FindVisibleWindows_ShouldReturnAtLeastOne()
    {
        // Act
        var windows = WindowHelper.FindVisibleWindows();

        // Assert - there should always be at least one visible window in Windows
        windows.Should().NotBeEmpty();
    }

    [Fact]
    public void GetWindowTitle_WithValidWindow_ShouldReturnTitle()
    {
        // Arrange
        var windows = WindowHelper.FindVisibleWindows();
        var firstWindow = windows.First();

        // Act
        var title = WindowHelper.GetWindowTitle(firstWindow);

        // Assert - visible windows should have titles
        title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetWindowTitle_WithZeroHandle_ShouldReturnEmpty()
    {
        // Act
        var title = WindowHelper.GetWindowTitle(IntPtr.Zero);

        // Assert
        title.Should().BeEmpty();
    }

    [Fact]
    public void GetWindowClassName_WithValidWindow_ShouldReturnClassName()
    {
        // Arrange
        var windows = WindowHelper.FindVisibleWindows();
        var firstWindow = windows.First();

        // Act
        var className = WindowHelper.GetWindowClassName(firstWindow);

        // Assert
        className.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetWindowClassName_WithZeroHandle_ShouldReturnEmpty()
    {
        // Act
        var className = WindowHelper.GetWindowClassName(IntPtr.Zero);

        // Assert
        className.Should().BeEmpty();
    }

    #endregion

    #region Window State Tests

    [Fact]
    public void IsWindowVisible_WithVisibleWindow_ShouldReturnTrue()
    {
        // Arrange
        var windows = WindowHelper.FindVisibleWindows();
        var firstWindow = windows.First();

        // Act
        var isVisible = WindowHelper.IsWindowVisible(firstWindow);

        // Assert
        isVisible.Should().BeTrue();
    }

    [Fact]
    public void IsWindowVisible_WithZeroHandle_ShouldReturnFalse()
    {
        // Act
        var isVisible = WindowHelper.IsWindowVisible(IntPtr.Zero);

        // Assert
        isVisible.Should().BeFalse();
    }

    [Fact]
    public void IsWindowMinimized_WithZeroHandle_ShouldReturnFalse()
    {
        // Act
        var isMinimized = WindowHelper.IsWindowMinimized(IntPtr.Zero);

        // Assert
        isMinimized.Should().BeFalse();
    }

    [Fact]
    public void IsWindowMaximized_WithZeroHandle_ShouldReturnFalse()
    {
        // Act
        var isMaximized = WindowHelper.IsWindowMaximized(IntPtr.Zero);

        // Assert
        isMaximized.Should().BeFalse();
    }

    #endregion

    #region GetWindowPosition Tests

    [Fact]
    public void GetWindowPosition_WithValidWindow_ShouldReturnValidDimensions()
    {
        // Arrange
        var windows = WindowHelper.FindVisibleWindows();
        var firstWindow = windows.First();

        // Act
        var (x, y, width, height) = WindowHelper.GetWindowPosition(firstWindow);

        // Assert - visible windows should have positive dimensions
        width.Should().BeGreaterThan(0);
        height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetWindowPosition_WithZeroHandle_ShouldReturnZeros()
    {
        // Act
        var (x, y, width, height) = WindowHelper.GetWindowPosition(IntPtr.Zero);

        // Assert
        x.Should().Be(0);
        y.Should().Be(0);
        width.Should().Be(0);
        height.Should().Be(0);
    }

    #endregion

    #region FindWindows Tests

    [Fact]
    public void FindWindows_WithNullPredicate_ShouldReturnAllTopLevelWindows()
    {
        // Act
        var windows = WindowHelper.FindWindows(null);

        // Assert
        windows.Should().NotBeEmpty();
    }

    [Fact]
    public void FindWindows_WithAlwaysFalsePredicate_ShouldReturnEmpty()
    {
        // Act
        var windows = WindowHelper.FindWindows(_ => false);

        // Assert
        windows.Should().BeEmpty();
    }

    [Fact]
    public void FindWindows_WithAlwaysTruePredicate_ShouldReturnWindows()
    {
        // Act
        var windows = WindowHelper.FindWindows(_ => true);

        // Assert
        windows.Should().NotBeEmpty();
    }

    [Fact]
    public void FindWindowsByTitle_WithExistingTitle_ShouldFindWindows()
    {
        // Arrange - "Windows" is likely in some window title
        // Or use a more specific term that's likely to exist
        var windows = WindowHelper.FindVisibleWindows();
        var firstWindow = windows.First();
        var title = WindowHelper.GetWindowTitle(firstWindow);
        var partialTitle = title.Length > 3 ? title.Substring(0, 3) : title;

        // Act
        var foundWindows = WindowHelper.FindWindowsByTitle(partialTitle);

        // Assert
        foundWindows.Should().NotBeEmpty();
    }

    [Fact]
    public void FindWindowsByTitle_WithNonexistentTitle_ShouldReturnEmpty()
    {
        // Act
        var windows = WindowHelper.FindWindowsByTitle("XYZNONEXISTENTTITLE123456789");

        // Assert
        windows.Should().BeEmpty();
    }

    #endregion

    #region Safety Tests

    [Fact]
    public void MakeBorderless_WithZeroHandle_ShouldNotThrow()
    {
        // Act
        var action = () => WindowHelper.MakeBorderless(IntPtr.Zero);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RestoreBorders_WithZeroHandle_ShouldNotThrow()
    {
        // Act
        var action = () => WindowHelper.RestoreBorders(IntPtr.Zero);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void SetWindowPosition_WithZeroHandle_ShouldReturnFalse()
    {
        // Act
        var result = WindowHelper.SetWindowPosition(IntPtr.Zero, 0, 0, 100, 100);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ForceForegroundWindow_WithZeroHandle_ShouldReturnFalse()
    {
        // Act
        var result = WindowHelper.ForceForegroundWindow(IntPtr.Zero);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MinimizeWindow_WithZeroHandle_ShouldNotThrow()
    {
        // Act
        var action = () => WindowHelper.MinimizeWindow(IntPtr.Zero);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void MaximizeWindow_WithZeroHandle_ShouldNotThrow()
    {
        // Act
        var action = () => WindowHelper.MaximizeWindow(IntPtr.Zero);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RestoreWindow_WithZeroHandle_ShouldNotThrow()
    {
        // Act
        var action = () => WindowHelper.RestoreWindow(IntPtr.Zero);

        // Assert
        action.Should().NotThrow();
    }

    #endregion
}
