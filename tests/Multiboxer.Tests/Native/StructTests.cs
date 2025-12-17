using FluentAssertions;
using Multiboxer.Native;
using System.Runtime.InteropServices;
using Xunit;

namespace Multiboxer.Tests.Native;

/// <summary>
/// Tests for native struct definitions to ensure correct memory layout for P/Invoke
/// </summary>
public class StructTests
{
    [Fact]
    public void RECT_ShouldHaveCorrectLayout()
    {
        // Arrange & Act
        var rect = new RECT(10, 20, 110, 220);

        // Assert
        rect.Left.Should().Be(10);
        rect.Top.Should().Be(20);
        rect.Right.Should().Be(110);
        rect.Bottom.Should().Be(220);
        rect.Width.Should().Be(100);
        rect.Height.Should().Be(200);
    }

    [Fact]
    public void RECT_ShouldHaveCorrectSize()
    {
        // RECT should be 16 bytes (4 ints)
        Marshal.SizeOf<RECT>().Should().Be(16);
    }

    [Fact]
    public void POINT_ShouldHaveCorrectLayout()
    {
        // Arrange & Act
        var point = new POINT(50, 100);

        // Assert
        point.X.Should().Be(50);
        point.Y.Should().Be(100);
    }

    [Fact]
    public void POINT_ShouldHaveCorrectSize()
    {
        // POINT should be 8 bytes (2 ints)
        Marshal.SizeOf<POINT>().Should().Be(8);
    }

    [Fact]
    public void WINDOWPLACEMENT_ShouldHaveCorrectSize()
    {
        // WINDOWPLACEMENT should be 44 bytes on x86/x64
        var size = Marshal.SizeOf<WINDOWPLACEMENT>();
        size.Should().Be(44);
    }

    [Fact]
    public void INPUT_ShouldHaveCorrectSize()
    {
        // INPUT struct size varies by architecture
        var size = Marshal.SizeOf<INPUT>();
        // On x64: type (4) + padding (4) + union (32) = 40 bytes
        // On x86: type (4) + union (28) = 32 bytes
        size.Should().BeOneOf(32, 40);
    }

    [Fact]
    public void KEYBDINPUT_ShouldHaveCorrectSize()
    {
        var size = Marshal.SizeOf<KEYBDINPUT>();
        // wVk (2) + wScan (2) + dwFlags (4) + time (4) + dwExtraInfo (8 on x64, 4 on x86)
        size.Should().BeOneOf(16, 24);
    }

    [Fact]
    public void MOUSEINPUT_ShouldHaveCorrectSize()
    {
        var size = Marshal.SizeOf<MOUSEINPUT>();
        // dx (4) + dy (4) + mouseData (4) + dwFlags (4) + time (4) + dwExtraInfo (8 on x64, 4 on x86)
        size.Should().BeOneOf(24, 28, 32);
    }

    [Fact]
    public void KBDLLHOOKSTRUCT_ShouldHaveCorrectSize()
    {
        var size = Marshal.SizeOf<KBDLLHOOKSTRUCT>();
        // vkCode (4) + scanCode (4) + flags (4) + time (4) + dwExtraInfo (8 on x64, 4 on x86)
        size.Should().BeOneOf(20, 24);
    }

    [Fact]
    public void SYSTEM_INFO_ShouldHaveCorrectMinimumFields()
    {
        // Ensure SYSTEM_INFO has required fields
        var info = new SYSTEM_INFO();
        info.numberOfProcessors.Should().Be(0u); // Default value
        info.pageSize.Should().Be(0u);
    }
}
