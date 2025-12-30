using Microsoft.Win32;

namespace FF14RisingstoneCheckIn.Utils;

public static class AutoStartHelper
{
    private const string AppName = "FF14RisingstoneCheckIn";
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return false;

            if (enable)
            {
                var exePath = Application.ExecutablePath;
                key.SetValue(AppName, $"\"{exePath}\" --silent");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
