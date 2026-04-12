namespace Tarn.ClientApp.Play.App;

public static class KeyMap
{
    public static InputAction Map(ConsoleKeyInfo key)
    {
        return key.Key switch
        {
            ConsoleKey.UpArrow => InputAction.MoveUp,
            ConsoleKey.DownArrow => InputAction.MoveDown,
            ConsoleKey.LeftArrow => InputAction.MoveLeft,
            ConsoleKey.RightArrow => InputAction.MoveRight,
            ConsoleKey.Enter => InputAction.Select,
            ConsoleKey.Escape => InputAction.Back,
            ConsoleKey.Q => InputAction.Quit,
            ConsoleKey.H => InputAction.ToggleHelp,
            ConsoleKey.D1 or ConsoleKey.NumPad1 => InputAction.Screen1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => InputAction.Screen2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => InputAction.Screen3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => InputAction.Screen4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => InputAction.Screen5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => InputAction.Screen6,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => InputAction.Screen7,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => InputAction.Screen8,
            ConsoleKey.A => InputAction.AdvanceWeek,
            ConsoleKey.N => InputAction.NextEvent,
            ConsoleKey.R => InputAction.NextRound,
            ConsoleKey.P => InputAction.ToggleAutoplay,
            ConsoleKey.Y => InputAction.Confirm,
            _ when key.KeyChar == '?' => InputAction.ToggleHelp,
            _ => InputAction.None,
        };
    }
}
