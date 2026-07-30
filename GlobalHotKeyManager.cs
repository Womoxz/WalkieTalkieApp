using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Hook global de teclado para el pulsar-para-hablar.
    ///
    /// Cambios: la tecla es configurable (antes F7 a fuego) y por defecto YA NO se
    /// bloquea para el resto del sistema. La versión anterior devolvía siempre 1,
    /// así que F7 dejaba de funcionar en Excel, navegadores y cualquier otro
    /// programa mientras la app estuviera abierta, incluso minimizada.
    /// </summary>
    public class GlobalHotKeyManager : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private IntPtr hookId = IntPtr.Zero;
        private bool disposed;
        private bool isDown;

        private readonly Action keyDownAction;
        private readonly Action keyUpAction;
        private readonly bool suppress;

        public Keys HotKey { get; set; }

        // Hay que conservar la referencia al delegado o el GC lo recoge y el hook
        // muere silenciosamente al rato.
        private readonly LowLevelKeyboardProc proc;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public GlobalHotKeyManager(Keys hotKey, Action keyDownAction, Action keyUpAction, bool suppress = false)
        {
            HotKey = hotKey;
            this.keyDownAction = keyDownAction;
            this.keyUpAction = keyUpAction;
            this.suppress = suppress;

            proc = HookCallback;
            hookId = SetHook(proc);

            if (hookId == IntPtr.Zero)
                AppPaths.Log(AppPaths.CrashLog,
                    $"No se pudo instalar el hook de teclado (error {Marshal.GetLastWin32Error()})");
        }

        public bool IsInstalled => hookId != IntPtr.Zero;

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            // GetModuleHandle(null) devuelve el módulo del proceso actual, que es lo
            // válido para un hook global de bajo nivel; usar MainModule.ModuleName
            // falla en aplicaciones publicadas como archivo único.
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == (int)HotKey)
                {
                    int msg = wParam.ToInt32();

                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        // El teclado repite mientras se mantiene pulsada la tecla.
                        if (!isDown)
                        {
                            isDown = true;
                            SafeInvoke(keyDownAction);
                        }
                        if (suppress) return (IntPtr)1;
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        if (isDown)
                        {
                            isDown = false;
                            SafeInvoke(keyUpAction);
                        }
                        if (suppress) return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private static void SafeInvoke(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                // Una excepción aquí dentro tumbaría el hook para todo el sistema.
                Debug.WriteLine($"Hotkey: {ex.Message}");
                AppPaths.Log(AppPaths.CrashLog, $"Hotkey: {ex}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookId);
                    hookId = IntPtr.Zero;
                }
                disposed = true;
            }
        }

        ~GlobalHotKeyManager() => Dispose(false);
    }
}
