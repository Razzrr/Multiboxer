using FluentAssertions;
using Multiboxer.Core.Performance;
using System.Diagnostics;
using Xunit;

namespace Multiboxer.Tests.Performance;

/// <summary>
/// Tests for AffinityManager class
/// </summary>
public class AffinityManagerTests
{
    private readonly AffinityManager _affinityManager;

    public AffinityManagerTests()
    {
        _affinityManager = new AffinityManager();
    }

    #region ProcessorCount Tests

    [Fact]
    public void ProcessorCount_ShouldBeGreaterThanZero()
    {
        // Assert
        _affinityManager.ProcessorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProcessorCount_ShouldMatchEnvironment()
    {
        // Assert
        _affinityManager.ProcessorCount.Should().Be(Environment.ProcessorCount);
    }

    #endregion

    #region CreateAffinityMask Tests

    [Fact]
    public void CreateAffinityMask_SingleCore_ShouldReturnCorrectMask()
    {
        // Act
        var mask = AffinityManager.CreateAffinityMask(new[] { 0 });

        // Assert
        mask.Should().Be(new IntPtr(1)); // 0b0001
    }

    [Fact]
    public void CreateAffinityMask_Core1_ShouldReturnCorrectMask()
    {
        // Act
        var mask = AffinityManager.CreateAffinityMask(new[] { 1 });

        // Assert
        mask.Should().Be(new IntPtr(2)); // 0b0010
    }

    [Fact]
    public void CreateAffinityMask_MultipleCores_ShouldReturnCorrectMask()
    {
        // Act
        var mask = AffinityManager.CreateAffinityMask(new[] { 0, 2 });

        // Assert
        mask.Should().Be(new IntPtr(5)); // 0b0101
    }

    [Fact]
    public void CreateAffinityMask_AllFirstFourCores_ShouldReturnCorrectMask()
    {
        // Act
        var mask = AffinityManager.CreateAffinityMask(new[] { 0, 1, 2, 3 });

        // Assert
        mask.Should().Be(new IntPtr(15)); // 0b1111
    }

    [Fact]
    public void CreateAffinityMask_EmptyArray_ShouldReturnZero()
    {
        // Act
        var mask = AffinityManager.CreateAffinityMask(Array.Empty<int>());

        // Assert
        mask.Should().Be(IntPtr.Zero);
    }

    #endregion

    #region GetCoresFromMask Tests

    [Fact]
    public void GetCoresFromMask_Core0_ShouldReturnSingleCore()
    {
        // Arrange
        var mask = new IntPtr(1); // 0b0001

        // Act
        var cores = AffinityManager.GetCoresFromMask(mask);

        // Assert
        cores.Should().Equal(new[] { 0 });
    }

    [Fact]
    public void GetCoresFromMask_Cores0And2_ShouldReturnBothCores()
    {
        // Arrange
        var mask = new IntPtr(5); // 0b0101

        // Act
        var cores = AffinityManager.GetCoresFromMask(mask);

        // Assert
        cores.Should().Equal(new[] { 0, 2 });
    }

    [Fact]
    public void GetCoresFromMask_AllFirstFourCores_ShouldReturnFourCores()
    {
        // Arrange
        var mask = new IntPtr(15); // 0b1111

        // Act
        var cores = AffinityManager.GetCoresFromMask(mask);

        // Assert
        cores.Should().Equal(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void GetCoresFromMask_Zero_ShouldReturnEmpty()
    {
        // Arrange
        var mask = IntPtr.Zero;

        // Act
        var cores = AffinityManager.GetCoresFromMask(mask);

        // Assert
        cores.Should().BeEmpty();
    }

    #endregion

    #region Roundtrip Tests

    [Theory]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 0, 1 })]
    [InlineData(new[] { 0, 2, 4 })]
    [InlineData(new[] { 0, 1, 2, 3 })]
    public void CreateAffinityMask_GetCoresFromMask_ShouldRoundtrip(int[] inputCores)
    {
        // Skip cores that don't exist on this system
        var validCores = inputCores.Where(c => c < Environment.ProcessorCount).ToArray();
        if (validCores.Length == 0) return;

        // Act
        var mask = AffinityManager.CreateAffinityMask(validCores);
        var resultCores = AffinityManager.GetCoresFromMask(mask);

        // Assert
        resultCores.Should().BeEquivalentTo(validCores);
    }

    #endregion

    #region SystemAffinityMask Tests

    [Fact]
    public void SystemAffinityMask_ShouldNotBeZero()
    {
        // Assert
        _affinityManager.SystemAffinityMask.Should().NotBe(IntPtr.Zero);
    }

    #endregion

    #region GetProcessAffinity Tests

    [Fact]
    public void GetProcessAffinity_CurrentProcess_ShouldReturnValidMask()
    {
        // Arrange
        var process = Process.GetCurrentProcess();

        // Act
        var affinity = _affinityManager.GetProcessAffinity(process);

        // Assert
        affinity.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void GetProcessCores_CurrentProcess_ShouldReturnCores()
    {
        // Arrange
        var process = Process.GetCurrentProcess();

        // Act
        var cores = _affinityManager.GetProcessCores(process);

        // Assert
        cores.Should().NotBeEmpty();
    }

    #endregion

    #region SetProcessAffinity Tests

    [Fact]
    public void SetProcessAffinity_CurrentProcess_ShouldSucceed()
    {
        // Arrange
        var process = Process.GetCurrentProcess();
        var originalAffinity = _affinityManager.GetProcessAffinity(process);

        try
        {
            // Act - set to first core only
            var result = _affinityManager.SetProcessAffinity(process, new[] { 0 });

            // Assert
            result.Should().BeTrue();

            var newAffinity = _affinityManager.GetProcessAffinity(process);
            newAffinity.Should().Be(new IntPtr(1));
        }
        finally
        {
            // Restore original affinity
            _affinityManager.SetProcessAffinityMask(process, originalAffinity);
        }
    }

    [Fact]
    public void RestoreOriginalAffinity_ShouldRestorePreviousAffinity()
    {
        // Arrange
        var process = Process.GetCurrentProcess();
        var originalAffinity = _affinityManager.GetProcessAffinity(process);

        // Change affinity
        _affinityManager.SetProcessAffinity(process, new[] { 0 });
        var changedAffinity = _affinityManager.GetProcessAffinity(process);
        changedAffinity.Should().Be(new IntPtr(1));

        // Act
        _affinityManager.RestoreOriginalAffinity(process);

        // Assert
        var restoredAffinity = _affinityManager.GetProcessAffinity(process);
        restoredAffinity.Should().Be(originalAffinity);
    }

    #endregion

    #region DistributeAcrossCores Tests

    [Fact]
    public void DistributeAcrossCores_WithNoProcesses_ShouldNotThrow()
    {
        // Act
        var action = () => _affinityManager.DistributeAcrossCores(new List<Process>());

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region PinToCore Tests

    [Fact]
    public void PinToCore_ValidCore_ShouldSucceed()
    {
        // Arrange
        var process = Process.GetCurrentProcess();
        var originalAffinity = _affinityManager.GetProcessAffinity(process);

        try
        {
            // Act
            var result = _affinityManager.PinToCore(process, 0);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            // Restore
            _affinityManager.SetProcessAffinityMask(process, originalAffinity);
        }
    }

    [Fact]
    public void PinToCore_InvalidCore_ShouldReturnFalse()
    {
        // Arrange
        var process = Process.GetCurrentProcess();
        int invalidCore = Environment.ProcessorCount + 100; // Way beyond available cores

        // Act
        var result = _affinityManager.PinToCore(process, invalidCore);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
