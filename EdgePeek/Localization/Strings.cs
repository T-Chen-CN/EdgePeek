namespace EdgePeek.Localization;

public static class Strings
{
    public static bool IsChinese(string? language)
    {
        return string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase);
    }

    public static string SettingsTitle(bool zh) => zh ? "设置" : "Settings";
    public static string Language(bool zh) => zh ? "语言" : "Language";
    public static string DockEdge(bool zh) => zh ? "停靠边缘" : "Dock edge";
    public static string HotZonePx(bool zh) => zh ? "热区像素" : "Hot zone px";
    public static string EdgeDelayMs(bool zh) => zh ? "贴边触发时长(ms)" : "Edge delay ms";
    public static string HomeUrl(bool zh) => zh ? "主页地址" : "Home URL";
    public static string Hotkey(bool zh) => zh ? "快捷键" : "Hotkey";
    public static string Record(bool zh) => zh ? "录制" : "Record";
    public static string TopMost(bool zh) => zh ? "保持窗口置顶" : "Keep panel above other windows";
    public static string HideOnLostFocus(bool zh) => zh ? "失去焦点时隐藏" : "Hide when focus is lost";
    public static string StartWithWindows(bool zh) => zh ? "开机启动" : "Start with Windows";
    public static string EnableHotkey(bool zh) => zh ? "启用快捷键" : "Enable hotkey";
    public static string Back(bool zh) => zh ? "返回" : "Back";
    public static string Save(bool zh) => zh ? "保存" : "Save";
    public static string Cancel(bool zh) => zh ? "取消" : "Cancel";
    public static string Discard(bool zh) => zh ? "放弃" : "Discard";
    public static string Show(bool zh) => zh ? "显示" : "Show";
    public static string Hide(bool zh) => zh ? "隐藏" : "Hide";
    public static string Exit(bool zh) => zh ? "退出" : "Exit";
    public static string Left(bool zh) => zh ? "左侧" : "Left";
    public static string Right(bool zh) => zh ? "右侧" : "Right";
    public static string PressShortcut(bool zh) => zh ? "请按快捷键..." : "Press shortcut...";
    public static string InvalidSettingsTitle(bool zh) => zh ? "设置无效" : "Invalid settings";
    public static string UnsavedSettings(bool zh) => zh ? "设置有未保存的更改。" : "Settings have unsaved changes.";
    public static string HotZoneValidation(bool zh) => zh ? "热区像素必须在 1 到 32 之间。" : "Hot zone must be between 1 and 32 pixels.";
    public static string EdgeDelayValidation(bool zh) => zh ? "贴边触发时长必须在 0 到 5000 毫秒之间。" : "Edge delay must be between 0 and 5000 milliseconds.";
    public static string HotkeyValidation(bool zh) => zh ? "请至少包含一个修饰键，例如 Ctrl 或 Alt。" : "Use at least one modifier key, such as Ctrl or Alt.";
    public static string TriggerHelp(bool zh)
    {
        return zh
            ? "屏幕边缘的感应区域宽度。数值越大越容易触发，但也更容易误触。"
            : "Width of the screen-edge sensing area. Larger values are easier to trigger but may cause accidental popups.";
    }
}
