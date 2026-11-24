using System;
using System.Diagnostics;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AuteTurno.Conquest
{
    /// <summary>Starts the bundled room service and registers the game's invite link on Windows.</summary>
    public sealed class ConquestHostLauncher : MonoBehaviour
    {
        private const string HealthUrl = "http://127.0.0.1:3080/api/health";
        private static readonly IntPtr CurrentUserKey = new IntPtr(unchecked((int)0x80000001));
        private Process ownedServer;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegCreateKeyEx(IntPtr key, string subKey, int reserved, string keyClass,
            uint options, int access, IntPtr security, out IntPtr result, out uint disposition);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegSetValueEx(IntPtr key, string valueName, int reserved, uint type, byte[] data, int dataSize);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr key);

        public IEnumerator EnsureRunning(Action<bool, string> completed)
        {
            using (var check = UnityWebRequest.Get(HealthUrl))
            {
                check.timeout = 2;
                yield return check.SendWebRequest();
                if (IsOurServer(check)) { completed(true, "Anfitrión listo."); yield break; }
            }

            string folder = Path.Combine(Application.streamingAssetsPath, "P2PHost");
            string node = Path.Combine(folder, "node.exe");
            string script = Path.Combine(folder, "index.js");
            if (!File.Exists(node) || !File.Exists(script))
            {
                completed(false, "Faltan los archivos del anfitrión. Reinstala el juego completo.");
                yield break;
            }

            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = node,
                    Arguments = "\"" + script + "\"",
                    WorkingDirectory = folder,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                ownedServer = Process.Start(info);
            }
            catch (Exception e)
            {
                completed(false, "No se pudo iniciar el anfitrión: " + e.Message);
                yield break;
            }

            for (int attempt = 0; attempt < 24; attempt++)
            {
                if (ownedServer == null || ownedServer.HasExited)
                {
                    completed(false, "El anfitrión no pudo abrir el puerto 3080. Puede estar ocupado por otra aplicación.");
                    yield break;
                }
                using (var check = UnityWebRequest.Get(HealthUrl))
                {
                    check.timeout = 2;
                    yield return check.SendWebRequest();
                    if (IsOurServer(check)) { completed(true, "Anfitrión listo."); yield break; }
                }
                yield return new WaitForSecondsRealtime(.25f);
            }
            completed(false, "El anfitrión tardó demasiado en responder. Comprueba si el puerto 3080 está libre.");
        }

        private static bool IsOurServer(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success && request.downloadHandler != null
                && request.downloadHandler.text.IndexOf("islas-en-disputa", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void RegisterInviteProtocol()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer) return;
            try
            {
                string executable = Process.GetCurrentProcess().MainModule.FileName;
                SetUserProtocolValue(@"Software\Classes\aethria", "", "URL:Atlas de Aethria invite");
                SetUserProtocolValue(@"Software\Classes\aethria", "URL Protocol", "");
                SetUserProtocolValue(@"Software\Classes\aethria\shell\open\command", "", "\"" + executable + "\" \"%1\"");
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("No se pudo registrar el enlace de invitación: " + e.Message); }
        }

        private static void SetUserProtocolValue(string path, string name, string value)
        {
            IntPtr key;
            uint disposition;
            const int keySetValue = 0x0002, keyCreateSubKey = 0x0004;
            int result = RegCreateKeyEx(CurrentUserKey, path, 0, null, 0, keySetValue | keyCreateSubKey, IntPtr.Zero, out key, out disposition);
            if (result != 0) throw new InvalidOperationException("Windows no permitió registrar el enlace (" + result + ").");
            try
            {
                byte[] data = Encoding.Unicode.GetBytes((value ?? "") + "\0");
                result = RegSetValueEx(key, name, 0, 1, data, data.Length);
                if (result != 0) throw new InvalidOperationException("Windows no permitió guardar el enlace (" + result + ").");
            }
            finally { RegCloseKey(key); }
        }

        private void OnApplicationQuit()
        {
            try { if (ownedServer != null && !ownedServer.HasExited) ownedServer.Kill(); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("No se pudo cerrar el anfitrión: " + e.Message); }
        }
    }
}
