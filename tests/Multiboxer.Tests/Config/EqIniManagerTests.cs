using FluentAssertions;
using Multiboxer.Core.Config;
using Xunit;

namespace Multiboxer.Tests.Config;

public class EqIniManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly EqIniManager _manager;

    public EqIniManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"EqIniManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _manager = new EqIniManager(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    #region BaseIniPath Tests

    [Fact]
    public void BaseIniPath_ShouldReturnCorrectPath()
    {
        // Assert
        _manager.BaseIniPath.Should().Be(Path.Combine(_testDir, "eqclient.ini"));
    }

    #endregion

    #region GetSlotIniPath Tests

    [Theory]
    [InlineData(1, "eqclient.slot1.ini")]
    [InlineData(5, "eqclient.slot5.ini")]
    [InlineData(12, "eqclient.slot12.ini")]
    public void GetSlotIniPath_ShouldReturnCorrectPath(int slotId, string expectedFilename)
    {
        // Act
        var path = _manager.GetSlotIniPath(slotId);

        // Assert
        path.Should().Be(Path.Combine(_testDir, expectedFilename));
    }

    #endregion

    #region CreateSlotIni Tests

    [Fact]
    public void CreateSlotIni_WithNoBaseIni_ShouldReturnFalse()
    {
        // Act
        var result = _manager.CreateSlotIni(1, 800, 600, 100, 50);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CreateSlotIni_WithValidBaseIni_ShouldCreateSlotIni()
    {
        // Arrange
        var baseContent = @"[Defaults]
WindowedWidth=1024
WindowedHeight=768
WindowedMode=FALSE
SomeOtherSetting=value";
        File.WriteAllText(_manager.BaseIniPath, baseContent);

        // Act
        var result = _manager.CreateSlotIni(1, 800, 600, 100, 50);

        // Assert
        result.Should().BeTrue();
        File.Exists(_manager.GetSlotIniPath(1)).Should().BeTrue();
    }

    [Fact]
    public void CreateSlotIni_ShouldUpdateWindowDimensions()
    {
        // Arrange
        var baseContent = @"[Defaults]
WindowedWidth=1024
WindowedHeight=768
WindowedModeXOffset=0
WindowedModeYOffset=0
WindowedMode=FALSE";
        File.WriteAllText(_manager.BaseIniPath, baseContent);

        // Act
        _manager.CreateSlotIni(1, 800, 600, 100, 50);

        // Assert
        var slotContent = File.ReadAllText(_manager.GetSlotIniPath(1));
        slotContent.Should().Contain("WindowedWidth=800");
        slotContent.Should().Contain("WindowedHeight=600");
        slotContent.Should().Contain("WindowedModeXOffset=100");
        slotContent.Should().Contain("WindowedModeYOffset=50");
        slotContent.Should().Contain("WindowedMode=TRUE");
    }

    [Fact]
    public void CreateSlotIni_WithMissingKeys_ShouldAddThem()
    {
        // Arrange - INI with no window settings
        var baseContent = @"[Defaults]
SomeOtherSetting=value";
        File.WriteAllText(_manager.BaseIniPath, baseContent);

        // Act
        _manager.CreateSlotIni(1, 800, 600, 100, 50);

        // Assert
        var slotContent = File.ReadAllText(_manager.GetSlotIniPath(1));
        slotContent.Should().Contain("WindowedWidth=800");
        slotContent.Should().Contain("WindowedHeight=600");
        slotContent.Should().Contain("WindowedModeXOffset=100");
        slotContent.Should().Contain("WindowedModeYOffset=50");
    }

    #endregion

    #region UpdateWindowDimensions Tests

    [Fact]
    public void UpdateWindowDimensions_WithNonexistentFile_ShouldReturnFalse()
    {
        // Act
        var result = _manager.UpdateWindowDimensions(
            Path.Combine(_testDir, "nonexistent.ini"), 800, 600, 0, 0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UpdateWindowDimensions_WithValidFile_ShouldUpdateValues()
    {
        // Arrange
        var iniPath = Path.Combine(_testDir, "test.ini");
        var content = @"[Defaults]
WindowedWidth=1024
WindowedHeight=768
WindowedModeXOffset=0
WindowedModeYOffset=0";
        File.WriteAllText(iniPath, content);

        // Act
        var result = _manager.UpdateWindowDimensions(iniPath, 640, 480, 200, 150);

        // Assert
        result.Should().BeTrue();
        var updatedContent = File.ReadAllText(iniPath);
        updatedContent.Should().Contain("WindowedWidth=640");
        updatedContent.Should().Contain("WindowedHeight=480");
        updatedContent.Should().Contain("WindowedModeXOffset=200");
        updatedContent.Should().Contain("WindowedModeYOffset=150");
    }

    #endregion

    #region GetWindowDimensions Tests

    [Fact]
    public void GetWindowDimensions_WithNonexistentFile_ShouldReturnNull()
    {
        // Act
        var result = _manager.GetWindowDimensions(Path.Combine(_testDir, "nonexistent.ini"));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetWindowDimensions_WithValidFile_ShouldReturnCorrectValues()
    {
        // Arrange
        var iniPath = Path.Combine(_testDir, "test.ini");
        var content = @"[Defaults]
WindowedWidth=1920
WindowedHeight=1080
WindowedModeXOffset=50
WindowedModeYOffset=100";
        File.WriteAllText(iniPath, content);

        // Act
        var result = _manager.GetWindowDimensions(iniPath);

        // Assert
        result.Should().NotBeNull();
        result!.Value.width.Should().Be(1920);
        result!.Value.height.Should().Be(1080);
        result!.Value.x.Should().Be(50);
        result!.Value.y.Should().Be(100);
    }

    [Fact]
    public void GetWindowDimensions_WithMissingKeys_ShouldReturnDefaults()
    {
        // Arrange
        var iniPath = Path.Combine(_testDir, "test.ini");
        var content = @"[Defaults]
SomeOtherSetting=value";
        File.WriteAllText(iniPath, content);

        // Act
        var result = _manager.GetWindowDimensions(iniPath);

        // Assert
        result.Should().NotBeNull();
        result!.Value.width.Should().Be(1024);  // default
        result!.Value.height.Should().Be(768);  // default
        result!.Value.x.Should().Be(0);         // default
        result!.Value.y.Should().Be(0);         // default
    }

    #endregion

    #region CleanupSlotInis Tests

    [Fact]
    public void CleanupSlotInis_ShouldDeleteAllSlotInis()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "eqclient.slot1.ini"), "content");
        File.WriteAllText(Path.Combine(_testDir, "eqclient.slot5.ini"), "content");
        File.WriteAllText(Path.Combine(_testDir, "eqclient.slot12.ini"), "content");
        File.WriteAllText(Path.Combine(_testDir, "eqclient.ini"), "base content"); // Should NOT be deleted

        // Act
        _manager.CleanupSlotInis();

        // Assert
        File.Exists(Path.Combine(_testDir, "eqclient.slot1.ini")).Should().BeFalse();
        File.Exists(Path.Combine(_testDir, "eqclient.slot5.ini")).Should().BeFalse();
        File.Exists(Path.Combine(_testDir, "eqclient.slot12.ini")).Should().BeFalse();
        File.Exists(Path.Combine(_testDir, "eqclient.ini")).Should().BeTrue();
    }

    #endregion

    #region PrepareSlotInis Tests

    [Fact]
    public void PrepareSlotInis_ShouldCreateMultipleSlotInis()
    {
        // Arrange
        var baseContent = @"[Defaults]
WindowedWidth=1024
WindowedHeight=768";
        File.WriteAllText(_manager.BaseIniPath, baseContent);

        var slotDimensions = new List<(int slotId, int width, int height, int x, int y)>
        {
            (1, 640, 480, 0, 0),
            (2, 640, 480, 640, 0),
            (3, 640, 480, 1280, 0)
        };

        // Act
        _manager.PrepareSlotInis(slotDimensions);

        // Assert
        File.Exists(_manager.GetSlotIniPath(1)).Should().BeTrue();
        File.Exists(_manager.GetSlotIniPath(2)).Should().BeTrue();
        File.Exists(_manager.GetSlotIniPath(3)).Should().BeTrue();

        var slot1Content = File.ReadAllText(_manager.GetSlotIniPath(1));
        slot1Content.Should().Contain("WindowedModeXOffset=0");

        var slot2Content = File.ReadAllText(_manager.GetSlotIniPath(2));
        slot2Content.Should().Contain("WindowedModeXOffset=640");

        var slot3Content = File.ReadAllText(_manager.GetSlotIniPath(3));
        slot3Content.Should().Contain("WindowedModeXOffset=1280");
    }

    #endregion
}
