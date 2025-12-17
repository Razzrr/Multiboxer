using FluentAssertions;
using Multiboxer.Core.Config;
using Xunit;

namespace Multiboxer.Tests.Config;

/// <summary>
/// Tests for LaunchProfile and VirtualFileMapping
/// </summary>
public class LaunchProfileTests
{
    #region LaunchProfile Validation Tests

    [Fact]
    public void IsValid_WithValidProfile_ShouldReturnTrue()
    {
        // Arrange - use a real path that exists (Windows directory)
        var profile = new LaunchProfile
        {
            Name = "Test Profile",
            Path = Environment.GetFolderPath(Environment.SpecialFolder.System),
            Executable = "notepad.exe" // This exists in System32
        };

        // Act - IsValid checks Name, Path, Executable AND that Path/Executable exist on disk
        var isValid = profile.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithEmptyName_ShouldReturnFalse()
    {
        // Arrange
        var profile = new LaunchProfile
        {
            Name = "",
            Path = @"C:\Games\TestGame",
            Executable = "game.exe"
        };

        // Act
        var isValid = profile.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithEmptyPath_ShouldReturnFalse()
    {
        // Arrange
        var profile = new LaunchProfile
        {
            Name = "Test Profile",
            Path = "",
            Executable = "game.exe"
        };

        // Act
        var isValid = profile.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithEmptyExecutable_ShouldReturnFalse()
    {
        // Arrange
        var profile = new LaunchProfile
        {
            Name = "Test Profile",
            Path = @"C:\Games\TestGame",
            Executable = ""
        };

        // Act
        var isValid = profile.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithNullName_ShouldReturnFalse()
    {
        // Arrange
        var profile = new LaunchProfile
        {
            Name = null!,
            Path = @"C:\Games\TestGame",
            Executable = "game.exe"
        };

        // Act
        var isValid = profile.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    #endregion

    #region FullExecutablePath Tests

    [Fact]
    public void FullExecutablePath_ShouldCombinePathAndExecutable()
    {
        // Arrange
        var profile = new LaunchProfile
        {
            Name = "Test",
            Path = @"C:\Games\TestGame",
            Executable = "game.exe"
        };

        // Act
        var fullPath = profile.FullExecutablePath;

        // Assert
        fullPath.Should().Be(@"C:\Games\TestGame\game.exe");
    }

    [Fact]
    public void FullExecutablePath_WithTrailingSlash_ShouldNotDuplicate()
    {
        // Arrange
        var profile = new LaunchProfile
        {
            Name = "Test",
            Path = @"C:\Games\TestGame\",
            Executable = "game.exe"
        };

        // Act
        var fullPath = profile.FullExecutablePath;

        // Assert
        fullPath.Should().NotContain(@"\\");
    }

    #endregion

    #region LaunchProfile Properties Tests

    [Fact]
    public void LaunchProfile_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var profile = new LaunchProfile();

        // Assert
        profile.Name.Should().BeEmpty();
        profile.Game.Should().BeEmpty();
        profile.Path.Should().BeEmpty();
        profile.Executable.Should().BeEmpty();
        profile.Arguments.Should().BeEmpty();
        profile.UseVirtualFiles.Should().BeFalse();
        profile.VirtualFiles.Should().NotBeNull().And.BeEmpty();
        profile.RunAsAdmin.Should().BeFalse();
        profile.LaunchDelay.Should().Be(0);
        profile.WindowClass.Should().BeNull();
        profile.WindowTitlePattern.Should().BeNull();
        profile.GameExecutable.Should().BeNull();
    }

    #endregion
}

/// <summary>
/// Tests for VirtualFileMapping
/// </summary>
public class VirtualFileMappingTests
{
    [Fact]
    public void GetReplacementForSlot_ShouldReplaceSlotToken()
    {
        // Arrange
        var mapping = new VirtualFileMapping
        {
            Pattern = "*/config.ini",
            Replacement = @"C:\Configs\slot{slot}\config.ini"
        };

        // Act
        var result = mapping.GetReplacementForSlot(3, @"C:\Game\config.ini");

        // Assert
        result.Should().Contain("slot3");
    }

    [Fact]
    public void GetReplacementForSlot_ShouldReplacePathToken()
    {
        // Arrange
        var mapping = new VirtualFileMapping
        {
            Pattern = "*/config.ini",
            Replacement = @"{path}\slot{slot}\config.ini"
        };

        // Act
        var result = mapping.GetReplacementForSlot(1, @"C:\Game\config.ini");

        // Assert
        result.Should().StartWith(@"C:\Game");
    }

    [Fact]
    public void VirtualFileMapping_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var mapping = new VirtualFileMapping();

        // Assert
        mapping.Pattern.Should().BeEmpty();
        mapping.Replacement.Should().BeEmpty();
        mapping.Exact.Should().BeFalse();
    }
}
