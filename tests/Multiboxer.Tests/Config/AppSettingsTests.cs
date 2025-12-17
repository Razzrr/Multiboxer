using FluentAssertions;
using Multiboxer.Core.Config;
using Xunit;

namespace Multiboxer.Tests.Config;

/// <summary>
/// Tests for AppSettings and related configuration classes
/// </summary>
public class AppSettingsTests
{
    #region AppSettings Tests

    [Fact]
    public void CreateDefault_ShouldReturnValidSettings()
    {
        // Act
        var settings = AppSettings.CreateDefault();

        // Assert
        settings.Should().NotBeNull();
        settings.Version.Should().NotBeNullOrEmpty();
        settings.Profiles.Should().NotBeNull();
        settings.Hotkeys.Should().NotBeNull();
        settings.Layout.Should().NotBeNull();
        settings.Highlighter.Should().NotBeNull();
        settings.Performance.Should().NotBeNull();
        settings.Window.Should().NotBeNull();
    }

    [Fact]
    public void CreateDefault_ShouldHaveEmptyProfiles()
    {
        // Act
        var settings = AppSettings.CreateDefault();

        // Assert
        settings.Profiles.Should().BeEmpty();
    }

    #endregion

    #region HotkeySettings Tests

    [Fact]
    public void HotkeySettings_CreateDefault_ShouldReturnValidSettings()
    {
        // Act
        var hotkeys = HotkeySettings.CreateDefault();

        // Assert
        hotkeys.Should().NotBeNull();
        hotkeys.SlotHotkeys.Should().NotBeNull();
        hotkeys.GlobalHotkeysEnabled.Should().BeTrue();
    }

    [Fact]
    public void HotkeySettings_CreateDefault_ShouldHaveF1ThroughF12PlusEnd()
    {
        // Act
        var hotkeys = HotkeySettings.CreateDefault();

        // Assert - F1-F12 (12 keys) + End key for slot 13 = 13 total
        hotkeys.SlotHotkeys.Should().HaveCount(13);
        hotkeys.SlotHotkeys.Should().Contain("F1");
        hotkeys.SlotHotkeys.Should().Contain("F12");
        hotkeys.SlotHotkeys.Should().Contain("End");
    }

    [Fact]
    public void HotkeySettings_CreateDefault_ShouldHaveNavigationHotkeys()
    {
        // Act
        var hotkeys = HotkeySettings.CreateDefault();

        // Assert
        hotkeys.PreviousWindow.Should().NotBeNullOrEmpty();
        hotkeys.NextWindow.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region LayoutSettings Tests

    [Fact]
    public void LayoutSettings_CreateDefault_ShouldReturnValidSettings()
    {
        // Act
        var layout = LayoutSettings.CreateDefault();

        // Assert
        layout.Should().NotBeNull();
        layout.ActiveLayout.Should().NotBeNullOrEmpty();
        layout.Options.Should().NotBeNull();
        layout.CustomLayouts.Should().NotBeNull();
    }

    [Fact]
    public void LayoutSettings_CreateDefault_ShouldHaveHorizontalAsDefault()
    {
        // Act
        var layout = LayoutSettings.CreateDefault();

        // Assert
        layout.ActiveLayout.Should().Be("Horizontal");
    }

    #endregion

    #region HighlighterSettings Tests

    [Fact]
    public void HighlighterSettings_CreateDefault_ShouldReturnValidSettings()
    {
        // Act
        var highlighter = HighlighterSettings.CreateDefault();

        // Assert
        highlighter.Should().NotBeNull();
        highlighter.ShowBorder.Should().BeTrue();
        highlighter.ShowNumber.Should().BeTrue();
        highlighter.BorderColor.Should().NotBeNullOrEmpty();
        highlighter.BorderThickness.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HighlighterSettings_CreateDefault_BorderColor_ShouldBeValidHex()
    {
        // Act
        var highlighter = HighlighterSettings.CreateDefault();

        // Assert - should be a valid hex color
        highlighter.BorderColor.Should().MatchRegex(@"^#[0-9A-Fa-f]{6}$");
    }

    #endregion

    #region PerformanceSettings Tests

    [Fact]
    public void PerformanceSettings_CreateDefault_ShouldReturnValidSettings()
    {
        // Act
        var performance = PerformanceSettings.CreateDefault();

        // Assert
        performance.Should().NotBeNull();
        performance.BackgroundMaxFps.Should().BeGreaterThan(0);
        // ForegroundMaxFps = 0 means unlimited (no limit)
        performance.ForegroundMaxFps.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void PerformanceSettings_CreateDefault_ShouldHaveReasonableBackgroundFps()
    {
        // Act
        var performance = PerformanceSettings.CreateDefault();

        // Assert - Background should be limited (30 fps default)
        performance.BackgroundMaxFps.Should().Be(30);
        // Foreground is 0 = unlimited
        performance.ForegroundMaxFps.Should().Be(0);
    }

    #endregion

    #region WindowSettings Tests

    [Fact]
    public void WindowSettings_ShouldHaveDefaultValues()
    {
        // Act
        var window = new WindowSettings();

        // Assert
        window.Left.Should().Be(0);
        window.Top.Should().Be(0);
        window.Width.Should().Be(900);  // Default width
        window.Height.Should().Be(600); // Default height
        window.Maximized.Should().BeFalse();
        window.MinimizeToTray.Should().BeTrue(); // Default is true
        window.StartMinimized.Should().BeFalse();
    }

    #endregion
}
