using FluentAssertions;
using Multiboxer.Core.Slots;
using Xunit;

namespace Multiboxer.Tests.Slots;

/// <summary>
/// Tests for SlotManager class
/// </summary>
public class SlotManagerTests : IDisposable
{
    private readonly SlotManager _slotManager;

    public SlotManagerTests()
    {
        _slotManager = new SlotManager();
    }

    public void Dispose()
    {
        _slotManager.Dispose();
    }

    #region MaxSlots Tests

    [Fact]
    public void MaxSlots_ShouldBe40()
    {
        SlotManager.MaxSlots.Should().Be(72);
    }

    #endregion

    #region GetOrCreateSlot Tests

    [Fact]
    public void GetOrCreateSlot_WithValidId_ShouldReturnSlot()
    {
        // Act
        var slot = _slotManager.GetOrCreateSlot(1);

        // Assert
        slot.Should().NotBeNull();
        slot.Id.Should().Be(1);
    }

    [Fact]
    public void GetOrCreateSlot_CalledTwice_ShouldReturnSameSlot()
    {
        // Act
        var slot1 = _slotManager.GetOrCreateSlot(1);
        var slot2 = _slotManager.GetOrCreateSlot(1);

        // Assert
        slot1.Should().BeSameAs(slot2);
    }

    [Fact]
    public void GetOrCreateSlot_DifferentIds_ShouldReturnDifferentSlots()
    {
        // Act
        var slot1 = _slotManager.GetOrCreateSlot(1);
        var slot2 = _slotManager.GetOrCreateSlot(2);

        // Assert
        slot1.Should().NotBeSameAs(slot2);
        slot1.Id.Should().Be(1);
        slot2.Id.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(73)]
    [InlineData(100)]
    public void GetOrCreateSlot_WithInvalidId_ShouldThrow(int invalidId)
    {
        // Act
        var action = () => _slotManager.GetOrCreateSlot(invalidId);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region GetSlot Tests

    [Fact]
    public void GetSlot_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var slot = _slotManager.GetSlot(1);

        // Assert
        slot.Should().BeNull();
    }

    [Fact]
    public void GetSlot_AfterCreation_ShouldReturnSlot()
    {
        // Arrange
        _slotManager.GetOrCreateSlot(5);

        // Act
        var slot = _slotManager.GetSlot(5);

        // Assert
        slot.Should().NotBeNull();
        slot!.Id.Should().Be(5);
    }

    #endregion

    #region GetActiveSlots Tests

    [Fact]
    public void GetActiveSlots_WithNoSlots_ShouldReturnEmpty()
    {
        // Act
        var activeSlots = _slotManager.GetActiveSlots();

        // Assert
        activeSlots.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveSlots_WithEmptySlots_ShouldReturnEmpty()
    {
        // Arrange
        _slotManager.GetOrCreateSlot(1);
        _slotManager.GetOrCreateSlot(2);

        // Act - slots are empty (no process)
        var activeSlots = _slotManager.GetActiveSlots();

        // Assert - empty slots are not "active"
        activeSlots.Should().BeEmpty();
    }

    #endregion

    #region GetNextAvailableSlotId Tests

    [Fact]
    public void GetNextAvailableSlotId_WithNoSlots_ShouldReturn1()
    {
        // Act
        var nextId = _slotManager.GetNextAvailableSlotId();

        // Assert
        nextId.Should().Be(1);
    }

    [Fact]
    public void GetNextAvailableSlotId_WithSlot1NoProcess_ShouldReturn1()
    {
        // Arrange - slot exists but has no process
        _slotManager.GetOrCreateSlot(1);

        // Act - should return 1 since slot 1 has no process
        var nextId = _slotManager.GetNextAvailableSlotId();

        // Assert - GetNextAvailableSlotId returns first slot without a process
        nextId.Should().Be(1);
    }

    [Fact]
    public void GetNextAvailableSlotId_WithGapNoProcess_ShouldReturnFirst()
    {
        // Arrange - slots exist but have no process
        _slotManager.GetOrCreateSlot(1);
        _slotManager.GetOrCreateSlot(3); // Skip 2

        // Act - should return 1 since it has no process
        var nextId = _slotManager.GetNextAvailableSlotId();

        // Assert - returns first slot ID where slot doesn't exist OR has no process
        nextId.Should().Be(1);
    }

    #endregion

    #region RemoveSlot Tests

    [Fact]
    public void RemoveSlot_WithExistingSlot_ShouldRemove()
    {
        // Arrange
        _slotManager.GetOrCreateSlot(1);

        // Act
        _slotManager.RemoveSlot(1);

        // Assert
        _slotManager.GetSlot(1).Should().BeNull();
    }

    [Fact]
    public void RemoveSlot_WithNonExistentSlot_ShouldNotThrow()
    {
        // Act
        var action = () => _slotManager.RemoveSlot(99);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region ActiveSlots Collection Tests

    [Fact]
    public void ActiveSlots_ShouldBeObservable()
    {
        // Assert
        _slotManager.ActiveSlots.Should().NotBeNull();
    }

    [Fact]
    public void ActiveSlots_WhenSlotCreated_ShouldContainSlot()
    {
        // Arrange & Act
        var slot = _slotManager.GetOrCreateSlot(1);

        // Assert
        _slotManager.ActiveSlots.Should().Contain(slot);
    }

    [Fact]
    public void ActiveSlots_WhenSlotRemoved_ShouldNotContainSlot()
    {
        // Arrange
        var slot = _slotManager.GetOrCreateSlot(1);

        // Act
        _slotManager.RemoveSlot(1);

        // Assert
        _slotManager.ActiveSlots.Should().NotContain(slot);
    }

    #endregion

    #region ActiveSlotCount Tests

    [Fact]
    public void ActiveSlotCount_WithNoActiveSlots_ShouldBeZero()
    {
        // Assert
        _slotManager.ActiveSlotCount.Should().Be(0);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public void FocusSlot_WithNonExistentSlot_ShouldReturnFalse()
    {
        // Act
        var result = _slotManager.FocusSlot(99);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FocusNextSlot_WithNoSlots_ShouldNotThrow()
    {
        // Act
        var action = () => _slotManager.FocusNextSlot();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void FocusPreviousSlot_WithNoSlots_ShouldNotThrow()
    {
        // Act
        var action = () => _slotManager.FocusPreviousSlot();

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Event Tests

    [Fact]
    public void SlotAdded_WhenSlotCreated_ShouldBeRaised()
    {
        // Arrange
        var eventRaised = false;
        _slotManager.SlotAdded += (s, e) => eventRaised = true;

        // Act
        _slotManager.GetOrCreateSlot(1);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void SlotRemoved_WhenSlotRemoved_ShouldBeRaised()
    {
        // Arrange
        _slotManager.GetOrCreateSlot(1);
        var eventRaised = false;
        _slotManager.SlotRemoved += (s, e) => eventRaised = true;

        // Act
        _slotManager.RemoveSlot(1);

        // Assert
        eventRaised.Should().BeTrue();
    }

    #endregion

    #region CloseAll Tests

    [Fact]
    public void CloseAll_WithNoSlots_ShouldNotThrow()
    {
        // Act
        var action = () => _slotManager.CloseAll();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void CloseAll_ShouldRemoveAllSlots()
    {
        // Arrange
        _slotManager.GetOrCreateSlot(1);
        _slotManager.GetOrCreateSlot(2);
        _slotManager.GetOrCreateSlot(3);

        // Act
        _slotManager.CloseAll();

        // Assert
        _slotManager.ActiveSlots.Should().BeEmpty();
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void GetOrCreateSlot_AtMaxSlots_ShouldWork()
    {
        // Act
        var slot = _slotManager.GetOrCreateSlot(SlotManager.MaxSlots);

        // Assert
        slot.Should().NotBeNull();
        slot.Id.Should().Be(72);
    }

    [Fact]
    public void GetOrCreateSlot_AllSlots_ShouldWork()
    {
        // Act - create all 40 slots
        for (int i = 1; i <= SlotManager.MaxSlots; i++)
        {
            _slotManager.GetOrCreateSlot(i);
        }

        // Assert
        _slotManager.ActiveSlots.Should().HaveCount(SlotManager.MaxSlots);
    }

    #endregion
}
