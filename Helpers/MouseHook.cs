using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Traductor.Helpers
{
    public class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Evento que se dispara cuando hay un arrastre (posible selección)
        public event Action<int, int>? SelectionDrag;

        private LowLevelMouseProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;

        // Para detectar arrastre
        private int _mouseDownX = 0;
        private int _mouseDownY = 0;
        private bool _isMouseDown = false;
        private const int MIN_DRAG_DISTANCE = 20; // Mínimo 20 pixeles para considerar arrastre

        public void Start()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        public void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    _mouseDownX = hookStruct.pt.x;
                    _mouseDownY = hookStruct.pt.y;
                    _isMouseDown = true;
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP && _isMouseDown)
                {
                    _isMouseDown = false;

                    // Calcular distancia del arrastre
                    int dx = Math.Abs(hookStruct.pt.x - _mouseDownX);
                    int dy = Math.Abs(hookStruct.pt.y - _mouseDownY);
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    // Solo disparar si hubo arrastre significativo (probable selección de texto)
                    if (distance >= MIN_DRAG_DISTANCE)
                    {
                        SelectionDrag?.Invoke(hookStruct.pt.x, hookStruct.pt.y);
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}
