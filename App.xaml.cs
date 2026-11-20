using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows;

namespace Traductor
{
    public partial class App : Application
    {
        public const string Tubo = "TraductorMSM_menu";

        [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int pid);

        /// <summary>Texto que llegó del menú del navegador (traductormsm:?t=...), o null.</summary>
        public static string? TextoNavegador(string[] args)
        {
            foreach (var a in args)
            {
                var s = a.Trim('"', ' ');
                if (!s.StartsWith("traductormsm:", StringComparison.OrdinalIgnoreCase)) continue;
                int i = s.IndexOf("t=", StringComparison.Ordinal);
                if (i < 0) return null;
                try { return Uri.UnescapeDataString(s.Substring(i + 2).Replace('+', ' ')); } catch { return null; }
            }
            return null;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Clic en «Traducir con Traductor MSM» del navegador: si el Traductor YA está abierto,
            // le pasa el texto por un tubo con nombre y esta segunda copia se cierra.
            var t = TextoNavegador(e.Args);
            if (t != null)
            {
                try
                {
                    using var c = new NamedPipeClientStream(".", Tubo, PipeDirection.Out);
                    c.Connect(600);
                    AllowSetForegroundWindow(-1);   // deja que la ventana abierta pase al frente
                    using var w = new StreamWriter(c);
                    w.Write(t);
                    w.Flush();
                    Shutdown();
                    return;
                }
                catch { /* no hay otro abierto: arranca normal y la ventana toma el texto */ }
            }
            base.OnStartup(e);
        }
    }
}
