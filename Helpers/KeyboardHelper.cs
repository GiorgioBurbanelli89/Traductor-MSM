using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Traductor.Helpers
{
    public static class KeyboardHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// Simula Ctrl+C para copiar texto seleccionado
        /// </summary>
        public static async Task SimulateCopyAsync()
        {
            // Key down Ctrl
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);

            // Key down C
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);

            // Pequeña pausa
            await Task.Delay(50);

            // Key up C
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // Key up Ctrl
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // Esperar a que el sistema procese el comando
            await Task.Delay(100);
        }
    }
}
