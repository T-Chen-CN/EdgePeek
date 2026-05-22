using System.Windows.Input;

namespace EdgePeek;

public readonly record struct HotkeyGesture(uint Modifiers, Key Key);

public static class HotkeyGestureParser
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    public static bool TryParse(string gesture, out HotkeyGesture parsed)
    {
        var modifiers = 0u;
        var key = Key.None;
        var keySeen = false;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            parsed = default;
            return false;
        }

        foreach (var part in gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part, "Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
            }
            else if (string.Equals(part, "Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
            }
            else if (string.Equals(part, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
            }
            else if (string.Equals(part, "Win", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
            }
            else if (Enum.TryParse(part, ignoreCase: true, out Key parsedKey))
            {
                if (keySeen || parsedKey == Key.None || IsModifierKey(parsedKey))
                {
                    parsed = default;
                    return false;
                }

                key = parsedKey;
                keySeen = true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }

        parsed = new HotkeyGesture(modifiers, key);
        return key != Key.None && modifiers != 0;
    }

    public static string? Build(ModifierKeys modifiers, Key key)
    {
        if (modifiers == ModifierKeys.None || key == Key.None || IsModifierKey(key))
        {
            return null;
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
    }
}
