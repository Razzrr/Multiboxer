using FluentAssertions;
using Multiboxer.Core.Input;
using Multiboxer.Native;
using Xunit;

namespace Multiboxer.Tests.Input;

/// <summary>
/// Tests for HotkeyManager class
/// </summary>
public class HotkeyManagerTests : IDisposable
{
    private readonly HotkeyManager _hotkeyManager;

    public HotkeyManagerTests()
    {
        // Use low-level hook mode (no window handle)
        _hotkeyManager = new HotkeyManager();
    }

    public void Dispose()
    {
        _hotkeyManager.Dispose();
    }

    #region Registration Tests

    [Fact]
    public void RegisterHotkey_ShouldAddBinding()
    {
        // Arrange
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.None);

        // Act
        var result = _hotkeyManager.RegisterHotkey(binding);

        // Assert
        result.Should().BeTrue();
        _hotkeyManager.Bindings.Should().ContainKey(binding.Id);
    }

    [Fact]
    public void RegisterHotkey_DifferentIdSameKey_ShouldSucceed()
    {
        // Arrange
        var binding1 = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.None);
        _hotkeyManager.RegisterHotkey(binding1);

        // Create another binding with the same key but different ID
        var binding2 = HotkeyBinding.CreateSlotHotkey(2, VirtualKeys.VK_F1, "F1", ModifierKeys.None);

        // Act - low-level hook mode allows duplicate keys with different IDs
        var result = _hotkeyManager.RegisterHotkey(binding2);

        // Assert - succeeds in low-level hook mode (tracks by ID, not key)
        result.Should().BeTrue();
        _hotkeyManager.Bindings.Should().HaveCount(2);
    }

    [Fact]
    public void RegisterDefaultSlotHotkeys_ShouldRegister12Hotkeys()
    {
        // Act
        _hotkeyManager.RegisterDefaultSlotHotkeys();

        // Assert - F1-F12 + End = 13 hotkeys
        _hotkeyManager.Bindings.Should().HaveCountGreaterOrEqualTo(12);
    }

    [Fact]
    public void RegisterDefaultNavigationHotkeys_ShouldRegisterNavigation()
    {
        // Act
        _hotkeyManager.RegisterDefaultNavigationHotkeys();

        // Assert
        var previousBinding = _hotkeyManager.GetBindingByAction("previousWindow");
        var nextBinding = _hotkeyManager.GetBindingByAction("nextWindow");

        previousBinding.Should().NotBeNull();
        nextBinding.Should().NotBeNull();
    }

    #endregion

    #region Unregistration Tests

    [Fact]
    public void UnregisterHotkey_WithExistingId_ShouldRemove()
    {
        // Arrange
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.None);
        _hotkeyManager.RegisterHotkey(binding);

        // Act
        _hotkeyManager.UnregisterHotkey(binding.Id);

        // Assert
        _hotkeyManager.Bindings.Should().NotContainKey(binding.Id);
    }

    [Fact]
    public void UnregisterHotkey_WithNonExistentId_ShouldNotThrow()
    {
        // Act
        var action = () => _hotkeyManager.UnregisterHotkey(99999);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void UnregisterAll_ShouldClearAllBindings()
    {
        // Arrange
        _hotkeyManager.RegisterDefaultSlotHotkeys();
        _hotkeyManager.Bindings.Should().NotBeEmpty();

        // Act
        _hotkeyManager.UnregisterAll();

        // Assert
        _hotkeyManager.Bindings.Should().BeEmpty();
    }

    #endregion

    #region Query Tests

    [Fact]
    public void GetBindingByAction_WithExistingAction_ShouldReturnBinding()
    {
        // Arrange
        _hotkeyManager.RegisterDefaultNavigationHotkeys();

        // Act
        var binding = _hotkeyManager.GetBindingByAction("previousWindow");

        // Assert
        binding.Should().NotBeNull();
        binding!.Action.Should().Be("previousWindow");
    }

    [Fact]
    public void GetBindingByAction_WithNonExistentAction_ShouldReturnNull()
    {
        // Act
        var binding = _hotkeyManager.GetBindingByAction("nonExistentAction");

        // Assert
        binding.Should().BeNull();
    }

    [Fact]
    public void GetSlotBinding_WithRegisteredSlot_ShouldReturnBinding()
    {
        // Arrange
        var slotBinding = HotkeyBinding.CreateSlotHotkey(5, VirtualKeys.VK_F5, "F5", ModifierKeys.None);
        _hotkeyManager.RegisterHotkey(slotBinding);

        // Act
        var binding = _hotkeyManager.GetSlotBinding(5);

        // Assert
        binding.Should().NotBeNull();
        binding!.SlotId.Should().Be(5);
    }

    [Fact]
    public void GetSlotBinding_WithUnregisteredSlot_ShouldReturnNull()
    {
        // Act
        var binding = _hotkeyManager.GetSlotBinding(99);

        // Assert
        binding.Should().BeNull();
    }

    #endregion

    #region Bindings Property Tests

    [Fact]
    public void Bindings_ShouldBeReadOnly()
    {
        // Assert
        _hotkeyManager.Bindings.Should().BeAssignableTo<IReadOnlyDictionary<int, HotkeyBinding>>();
    }

    [Fact]
    public void Bindings_InitialState_ShouldBeEmpty()
    {
        // Assert
        _hotkeyManager.Bindings.Should().BeEmpty();
    }

    #endregion

    #region Event Tests

    [Fact]
    public void HotkeyPressed_ShouldBeSubscribable()
    {
        // Arrange
        var eventRaised = false;
        _hotkeyManager.HotkeyPressed += (s, e) => eventRaised = true;

        // Assert - just verify we can subscribe without error
        eventRaised.Should().BeFalse(); // Not raised yet
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldUnregisterAll()
    {
        // Arrange
        var manager = new HotkeyManager();
        manager.RegisterDefaultSlotHotkeys();

        // Act
        manager.Dispose();

        // Assert
        manager.Bindings.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var manager = new HotkeyManager();

        // Act
        var action = () =>
        {
            manager.Dispose();
            manager.Dispose();
        };

        // Assert
        action.Should().NotThrow();
    }

    #endregion
}
