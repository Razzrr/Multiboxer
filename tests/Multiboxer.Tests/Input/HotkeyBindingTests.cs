using FluentAssertions;
using Multiboxer.Core.Input;
using Multiboxer.Native;
using Xunit;

namespace Multiboxer.Tests.Input;

/// <summary>
/// Tests for HotkeyBinding class
/// </summary>
public class HotkeyBindingTests
{
    #region Factory Method Tests

    [Fact]
    public void CreateSlotHotkey_ShouldSetCorrectProperties()
    {
        // Arrange
        int slotId = 5;
        uint keyCode = VirtualKeys.VK_F5;
        string keyName = "F5";

        // Act
        var binding = HotkeyBinding.CreateSlotHotkey(slotId, keyCode, keyName, ModifierKeys.None);

        // Assert
        binding.SlotId.Should().Be(slotId);
        binding.KeyCode.Should().Be(keyCode);
        binding.KeyName.Should().Be(keyName);
        binding.Action.Should().Contain("slot");
        binding.IsGlobal.Should().BeTrue();
        binding.Enabled.Should().BeTrue();
    }

    [Fact]
    public void CreateSlotHotkey_WithModifiers_ShouldSetModifiers()
    {
        // Arrange
        var modifiers = ModifierKeys.Control | ModifierKeys.Alt;

        // Act
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_1, "1", modifiers);

        // Assert
        binding.Modifiers.Should().Be(modifiers);
    }

    [Fact]
    public void CreateNavigationHotkey_PreviousWindow_ShouldSetCorrectAction()
    {
        // Act
        var binding = HotkeyBinding.CreateNavigationHotkey(
            100,
            "previousWindow",
            VirtualKeys.VK_Z,
            "Z",
            ModifierKeys.Control | ModifierKeys.Alt);

        // Assert
        binding.Action.Should().Be("previousWindow");
        binding.SlotId.Should().BeNull();
        binding.IsGlobal.Should().BeTrue();
    }

    [Fact]
    public void CreateNavigationHotkey_NextWindow_ShouldSetCorrectAction()
    {
        // Act
        var binding = HotkeyBinding.CreateNavigationHotkey(
            101,
            "nextWindow",
            VirtualKeys.VK_X,
            "X",
            ModifierKeys.Control | ModifierKeys.Alt);

        // Assert
        binding.Action.Should().Be("nextWindow");
    }

    #endregion

    #region DisplayString Tests

    [Fact]
    public void DisplayString_WithNoModifiers_ShouldShowKeyOnly()
    {
        // Arrange
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.None);

        // Act
        var displayString = binding.DisplayString;

        // Assert
        displayString.Should().Be("F1");
    }

    [Fact]
    public void DisplayString_WithControl_ShouldShowCtrlPrefix()
    {
        // Arrange
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.Control);

        // Act
        var displayString = binding.DisplayString;

        // Assert
        displayString.Should().Contain("Ctrl");
        displayString.Should().Contain("F1");
    }

    [Fact]
    public void DisplayString_WithAlt_ShouldShowAltPrefix()
    {
        // Arrange
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.Alt);

        // Act
        var displayString = binding.DisplayString;

        // Assert
        displayString.Should().Contain("Alt");
        displayString.Should().Contain("F1");
    }

    [Fact]
    public void DisplayString_WithShift_ShouldShowShiftPrefix()
    {
        // Arrange
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.Shift);

        // Act
        var displayString = binding.DisplayString;

        // Assert
        displayString.Should().Contain("Shift");
        displayString.Should().Contain("F1");
    }

    [Fact]
    public void DisplayString_WithAllModifiers_ShouldShowAll()
    {
        // Arrange
        var modifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", modifiers);

        // Act
        var displayString = binding.DisplayString;

        // Assert
        displayString.Should().Contain("Ctrl");
        displayString.Should().Contain("Alt");
        displayString.Should().Contain("Shift");
        displayString.Should().Contain("F1");
    }

    #endregion

    #region Property Tests

    [Fact]
    public void KeyName_ShouldMatchProvidedName()
    {
        // Arrange
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_END, "End", ModifierKeys.None);

        // Act
        var keyName = binding.KeyName;

        // Assert
        keyName.Should().Be("End");
    }

    [Fact]
    public void Id_SlotHotkeys_ShouldMatchSlotId()
    {
        // Arrange
        var binding1 = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.None);
        var binding2 = HotkeyBinding.CreateSlotHotkey(2, VirtualKeys.VK_F2, "F2", ModifierKeys.None);

        // Assert
        binding1.Id.Should().Be(1);
        binding2.Id.Should().Be(2);
    }

    [Fact]
    public void Enabled_DefaultShouldBeTrue()
    {
        // Arrange & Act
        var binding = HotkeyBinding.CreateSlotHotkey(1, VirtualKeys.VK_F1, "F1", ModifierKeys.None);

        // Assert
        binding.Enabled.Should().BeTrue();
    }

    #endregion

    #region Slot Hotkey Specific Tests

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(40)]
    public void CreateSlotHotkey_ValidSlotIds_ShouldWork(int slotId)
    {
        // Act
        var binding = HotkeyBinding.CreateSlotHotkey(slotId, VirtualKeys.VK_F1, "F1", ModifierKeys.None);

        // Assert
        binding.SlotId.Should().Be(slotId);
    }

    #endregion
}
