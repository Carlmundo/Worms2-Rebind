using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace GameKeyRebinder
{
    /// <summary>
    /// Retrieves key names from the currently active Windows keyboard layout.
    /// The INI still stores Windows virtual-key codes; this class only affects
    /// what the user sees in the UI.
    /// </summary>
    internal static class KeyNameHelper
    {
        private const uint MapVkVkToVsc = 0;
        private const int KeyNameBufferLength = 128;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetKeyNameText(
            int lParam,
            [Out] StringBuilder lpString,
            int nSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(
            uint uCode,
            uint uMapType,
            IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicodeEx(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out] StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags,
            IntPtr dwhkl);

        public static string GetLocalizedKeyName(int virtualKeyCode)
        {
            string fallback = GetFallbackKeyName(virtualKeyCode);

            if (virtualKeyCode <= 0 || virtualKeyCode > 255)
            {
                return fallback;
            }

            try
            {
                IntPtr keyboardLayout = GetKeyboardLayout(0);
                uint scanCode = MapVirtualKeyEx(
                    (uint)virtualKeyCode,
                    MapVkVkToVsc,
                    keyboardLayout);

                // GetKeyNameText deliberately reports VK_A through VK_Z as
                // Latin A-Z regardless of layout. For those keys, translate
                // the unmodified key through the active layout first so, for
                // example, a non-Latin layout can display its actual character.
                if (virtualKeyCode >= (int)Keys.A && virtualKeyCode <= (int)Keys.Z)
                {
                    string characterName = TryGetLayoutCharacter(
                        virtualKeyCode,
                        scanCode,
                        keyboardLayout);

                    if (characterName != null && characterName.Length > 0)
                    {
                        return characterName;
                    }
                }

                if (scanCode != 0)
                {
                    int keyData = ((int)(scanCode & 0xFFU)) << 16;

                    if (IsExtendedKey((Keys)virtualKeyCode))
                    {
                        keyData |= (1 << 24);
                    }

                    StringBuilder keyName = new StringBuilder(KeyNameBufferLength);
                    int length = GetKeyNameText(keyData, keyName, keyName.Capacity);

                    if (length > 0)
                    {
                        string result = keyName.ToString().Trim();
                        if (result.Length > 0)
                        {
                            return result;
                        }
                    }
                }
            }
            catch (DllNotFoundException)
            {
                // The application is Windows-only, but keep a safe fallback.
            }
            catch (EntryPointNotFoundException)
            {
                // Fall back to the managed key name on an unexpected platform.
            }

            return fallback;
        }

        private static string TryGetLayoutCharacter(
            int virtualKeyCode,
            uint scanCode,
            IntPtr keyboardLayout)
        {
            byte[] keyboardState = new byte[256];
            StringBuilder buffer = new StringBuilder(8);

            int characterCount = ToUnicodeEx(
                (uint)virtualKeyCode,
                scanCode,
                keyboardState,
                buffer,
                buffer.Capacity,
                0,
                keyboardLayout);

            if (characterCount <= 0)
            {
                return null;
            }

            string result = buffer.ToString();
            if (result.Length == 0)
            {
                return null;
            }

            // Key labels are conventionally shown in upper case. Invariant
            // casing keeps this independent of the application's UI culture.
            return result.ToUpperInvariant();
        }

        private static bool IsExtendedKey(Keys key)
        {
            switch (key)
            {
                case Keys.Insert:
                case Keys.Delete:
                case Keys.Home:
                case Keys.End:
                case Keys.Prior:
                case Keys.Next:
                case Keys.Left:
                case Keys.Up:
                case Keys.Right:
                case Keys.Down:
                case Keys.NumLock:
                case Keys.Divide:
                case Keys.RControlKey:
                case Keys.RMenu:
                case Keys.LWin:
                case Keys.RWin:
                case Keys.Apps:
                    return true;
                default:
                    return false;
            }
        }

        private static string GetFallbackKeyName(int virtualKeyCode)
        {
            Keys key = (Keys)virtualKeyCode;

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return ((char)('0' + (virtualKeyCode - (int)Keys.D0))).ToString();
            }

            switch (key)
            {
                case Keys.Back:
                    return "Backspace";
                case Keys.Return:
                    return "Enter";
                case Keys.Space:
                    return "Space";
                case Keys.Prior:
                    return "Page Up";
                case Keys.Next:
                    return "Page Down";
                case Keys.Capital:
                    return "Caps Lock";
                case Keys.ControlKey:
                    return "Ctrl";
                case Keys.Menu:
                    return "Alt";
                case Keys.ShiftKey:
                    return "Shift";
                default:
                    return key.ToString();
            }
        }
    }
}
