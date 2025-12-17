using FluentAssertions;
using Multiboxer.Core.Slots;
using Xunit;

namespace Multiboxer.Tests.Slots;

/// <summary>
/// Tests for SlotState enum
/// </summary>
public class SlotStateTests
{
    [Fact]
    public void SlotState_ShouldHaveAllExpectedValues()
    {
        // Assert
        Enum.GetValues<SlotState>().Should().Contain(SlotState.Empty);
        Enum.GetValues<SlotState>().Should().Contain(SlotState.Starting);
        Enum.GetValues<SlotState>().Should().Contain(SlotState.Running);
        Enum.GetValues<SlotState>().Should().Contain(SlotState.Foreground);
        Enum.GetValues<SlotState>().Should().Contain(SlotState.Minimized);
        Enum.GetValues<SlotState>().Should().Contain(SlotState.Exited);
        Enum.GetValues<SlotState>().Should().Contain(SlotState.Error);
    }

    [Theory]
    [InlineData(SlotState.Empty, 0)]
    [InlineData(SlotState.Starting, 1)]
    [InlineData(SlotState.Running, 2)]
    [InlineData(SlotState.Foreground, 3)]
    [InlineData(SlotState.Minimized, 4)]
    [InlineData(SlotState.Exited, 5)]
    [InlineData(SlotState.Error, 6)]
    public void SlotState_ShouldHaveCorrectValues(SlotState state, int expected)
    {
        ((int)state).Should().Be(expected);
    }

    [Fact]
    public void SlotState_Count_ShouldBe7()
    {
        Enum.GetValues<SlotState>().Should().HaveCount(7);
    }
}
