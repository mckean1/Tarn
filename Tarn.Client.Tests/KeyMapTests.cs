using Tarn.ClientApp.Play.App;

namespace Tarn.Client.Tests;

public sealed class KeyMapTests
{
    [Theory]
    [InlineData(ConsoleKey.UpArrow, '\0', InputAction.MoveUp)]
    [InlineData(ConsoleKey.Enter, '\r', InputAction.Select)]
    [InlineData(ConsoleKey.Escape, '\0', InputAction.Back)]
    [InlineData(ConsoleKey.Q, 'q', InputAction.Quit)]
    [InlineData(ConsoleKey.A, 'a', InputAction.AdvanceWeek)]
    [InlineData(ConsoleKey.N, 'n', InputAction.NextEvent)]
    [InlineData(ConsoleKey.R, 'r', InputAction.NextRound)]
    [InlineData(ConsoleKey.P, 'p', InputAction.ToggleAutoplay)]
    [InlineData(ConsoleKey.Y, 'y', InputAction.Confirm)]
    [InlineData(ConsoleKey.D2, '2', InputAction.Screen2)]
    public void MapsKeys(ConsoleKey key, char character, InputAction expected)
    {
        var info = new ConsoleKeyInfo(character, key, false, false, false);
        Assert.Equal(expected, KeyMap.Map(info));
    }

    [Fact]
    public void MapsQuestionMarkToHelp()
    {
        var info = new ConsoleKeyInfo('?', ConsoleKey.Oem2, false, false, false);
        Assert.Equal(InputAction.ToggleHelp, KeyMap.Map(info));
    }
}
