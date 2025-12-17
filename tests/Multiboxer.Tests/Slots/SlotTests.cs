using FluentAssertions;
using Multiboxer.Core.Config;
using Multiboxer.Core.Slots;
using Xunit;

namespace Multiboxer.Tests.Slots;

/// <summary>
/// Tests for Slot class
/// </summary>
public class SlotTests : IDisposable
{
    private readonly List<Slot> _createdSlots = new();

    public void Dispose()
    {
        foreach (var slot in _createdSlots)
        {
            slot.Dispose();
        }
    }

    private Slot CreateSlot(int id = 1)
    {
        var slot = new Slot(id);
        _createdSlots.Add(slot);
        return slot;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldSetId()
    {
        // Arrange & Act
        var slot = CreateSlot(5);

        // Assert
        slot.Id.Should().Be(5);
    }

    [Fact]
    public void Constructor_ShouldStartInEmptyState()
    {
        // Arrange & Act
        var slot = CreateSlot();

        // Assert
        slot.State.Should().Be(SlotState.Empty);
    }

    [Fact]
    public void Constructor_ShouldHaveNoProcess()
    {
        // Arrange & Act
        var slot = CreateSlot();

        // Assert
        slot.Process.Should().BeNull();
        slot.HasProcess.Should().BeFalse();
        slot.ProcessId.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldHaveZeroWindowHandle()
    {
        // Arrange & Act
        var slot = CreateSlot();

        // Assert
        slot.MainWindowHandle.Should().Be(IntPtr.Zero);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void DisplayName_WithNoProcess_ShouldShowSlotId()
    {
        // Arrange
        var slot = CreateSlot(3);

        // Act
        var displayName = slot.DisplayName;

        // Assert
        displayName.Should().Contain("3");
    }

    [Fact]
    public void WindowTitle_WithNoWindow_ShouldBeEmpty()
    {
        // Arrange
        var slot = CreateSlot();

        // Act
        var title = slot.WindowTitle;

        // Assert
        title.Should().BeEmpty();
    }

    [Fact]
    public void WindowPosition_WithNoWindow_ShouldBeZero()
    {
        // Arrange
        var slot = CreateSlot();

        // Assert
        slot.WindowX.Should().Be(0);
        slot.WindowY.Should().Be(0);
        slot.WindowWidth.Should().Be(0);
        slot.WindowHeight.Should().Be(0);
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public void DetachProcess_WithNoProcess_ShouldRemainEmpty()
    {
        // Arrange
        var slot = CreateSlot();

        // Act
        slot.DetachProcess();

        // Assert
        slot.State.Should().Be(SlotState.Empty);
        slot.HasProcess.Should().BeFalse();
    }

    [Fact]
    public void Close_WithNoProcess_ShouldNotThrow()
    {
        // Arrange
        var slot = CreateSlot();

        // Act
        var action = () => slot.Close();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Focus_WithNoWindow_ShouldReturnFalse()
    {
        // Arrange
        var slot = CreateSlot();

        // Act
        var result = slot.Focus();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsForeground_WithNoWindow_ShouldReturnFalse()
    {
        // Arrange
        var slot = CreateSlot();

        // Act
        var result = slot.IsForeground();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Event Tests

    [Fact]
    public void StateChanged_ShouldBeSubscribable()
    {
        // Arrange
        var slot = CreateSlot();
        var eventRaised = false;

        // Act - subscribe to the event
        slot.StateChanged += (s, e) => eventRaised = true;

        // Assert - verify we can subscribe without error
        // The event hasn't been raised yet
        eventRaised.Should().BeFalse();
    }

    #endregion

    #region ID Validation Tests

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(40)]
    public void Constructor_WithValidId_ShouldNotThrow(int id)
    {
        // Act
        var action = () => CreateSlot(id);

        // Assert
        action.Should().NotThrow();
    }

    #endregion
}
