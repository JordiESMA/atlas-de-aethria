using System;
using System.Linq;
using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace AuteTurno.Conquest
{
    /// <summary>Touch-friendly UI prototype for the two-player conquest game.</summary>
    public sealed partial class ConquestPrototype : MonoBehaviour
    {
        private enum AppScreen { HowTo, Menu, OnlineSetup, Lobby, Match, Result }
        private AppScreen screen;
        private ConquestGame game;
        private Territory selected, target;
        private string[] names = { "Jugador azul", "Jugador rojo" };
        private string serverUrl = "http://127.0.0.1:3080", shareAddress = "", joinCode = "", networkStatus = "";
        private ConquestNetworkClient network;
        private ConquestHostLauncher hostLauncher;
        private string pendingInvite;
        private bool onlineMode, sendingAction;
        private int myPlayer = -1, lastActivePlayer = -1;
        private void Awake()
        {
            network = gameObject.AddComponent<ConquestNetworkClient>();
            network.OnState = OnNetworkState;
            hostLauncher = gameObject.AddComponent<ConquestHostLauncher>();
#if !ATLAS_VISUAL_QA
            ConquestHostLauncher.RegisterInviteProtocol();
#endif
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("aethria://join", StringComparison.OrdinalIgnoreCase)) { pendingInvite = arg; break; }
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(pendingInvite)) yield break;
            yield return null;
            string address, code;
            if (!TryReadInvite(pendingInvite, out address, out code))
            { screen = AppScreen.OnlineSetup; networkStatus = "El enlace de invitación no es válido."; yield break; }
            serverUrl = address; inviteInput = address; joining = true; joinCode = code; names[0] = "Invitado"; screen = AppScreen.OnlineSetup;
            JoinOnlineRoom();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        { if (FindAnyObjectByType<ConquestPrototype>() == null) new GameObject("Conquista de Islas").AddComponent<ConquestPrototype>(); }

        private void Update()
        {
            if (screen == AppScreen.Match && game != null && game.Finished) screen = AppScreen.Result;
            if (game == null) return;
            if (lastActivePlayer >= 0 && lastActivePlayer != game.ActivePlayer) { selected = null; target = null; }
            lastActivePlayer = game.ActivePlayer;
        }

        private void StartMatch()
        {
            LeaveOnlineRoom(); onlineMode = false;
            shownRollTurn = -1;
            game = new ConquestGame(); game.Players[0].Name = string.IsNullOrWhiteSpace(names[0]) ? "Jugador azul" : names[0].Trim();
            game.Players[1].Name = string.IsNullOrWhiteSpace(names[1]) ? "Jugador rojo" : names[1].Trim();
            selected = null; target = null; lastActivePlayer = game.ActivePlayer; screen = AppScreen.Match;
        }

        private void CreateOnlineRoom()
        {
            if (hostLauncher == null) hostLauncher = gameObject.AddComponent<ConquestHostLauncher>();
            roomBusy = true; StartCoroutine(StartHostAndCreateRoom());
        }

        private IEnumerator StartHostAndCreateRoom()
        {
            networkStatus = "Abriendo sala automáticamente…"; onlineMode = true; myPlayer = 0;
            bool done = false, ready = false; string result = "";
            yield return StartCoroutine(hostLauncher.EnsureRunning((ok, message) => { ready = ok; result = message; done = true; }));
            while (!done) yield return null;
            if (!ready) { roomBusy = false; onlineMode = false; networkStatus = result; yield break; }
            serverUrl = "http://127.0.0.1:3080";
            shareAddress = "http://" + GetLanAddress() + ":3080";
            network.BaseUrl = serverUrl; networkStatus = "Anfitrión listo · creando código de sala…";
            network.CreateRoom(string.IsNullOrWhiteSpace(names[0]) ? "Jugador azul" : names[0].Trim());
        }

        private void JoinOnlineRoom()
        {
            if (joinCode.Trim().Length != 6) { networkStatus = "El código debe tener 6 caracteres."; return; }
            if (!ValidServerUrl(serverUrl)) { networkStatus = "Escribe la dirección del anfitrión, por ejemplo http://192.168.1.25:3080"; return; }
            network.BaseUrl = serverUrl.Trim(); networkStatus = "Conectando a la sala…"; onlineMode = true; myPlayer = 1;
            roomBusy = true; network.JoinRoom(joinCode, string.IsNullOrWhiteSpace(names[0]) ? "Jugador" : names[0].Trim());
        }

        private void OnNetworkState(RemoteGameState state, string error)
        {
            sendingAction = false;
            networkStatusIsError = !string.IsNullOrEmpty(error);
            if (networkStatusIsError) networkStatus = error;
            roomBusy = false; if (state == null) return;
            Territory previousSelection = selected, previousTarget = target;
            myPlayer = state.yourPlayer; game = game ?? new ConquestGame(); game.ApplySnapshot(state);
            selected = previousSelection == null ? null : game.Board[previousSelection.X,previousSelection.Y];
            target = previousTarget == null ? null : game.Board[previousTarget.X,previousTarget.Y];
            if (networkStatusIsError) return;
            if (state.status == "waiting") { lobbyCode = state.code; networkStatus = "Esperando al segundo jugador…"; screen = AppScreen.Lobby; }
            else if (state.finished) { networkStatus = "Partida finalizada."; screen = AppScreen.Result; }
            else if (state.status == "playing")
            {
                networkStatus = "Conectado · Jugador " + (myPlayer + 1); screen = AppScreen.Match;
            }
        }

        private void SendAction(string action, Territory from = null, Territory to = null)
        {
            sendingAction = true;
            network.SendAction(action, from != null ? from.X : -1, from != null ? from.Y : -1, to != null ? to.X : -1, to != null ? to.Y : -1);
        }

        private void LeaveOnlineRoom()
        {
            if (network != null) network.Disconnect();
            onlineMode = false; sendingAction = false; networkStatusIsError = false; myPlayer = -1; networkStatus = ""; shownRollTurn = -1;
        }
        private static bool ValidServerUrl(string value)
        { Uri uri; return Uri.TryCreate((value ?? "").Trim(), UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https") && !string.IsNullOrEmpty(uri.Host); }

        private static bool TryReadInvite(string invite, out string address, out string code)
        {
            address = ""; code = "";
            Uri uri;
            if (!Uri.TryCreate(invite, UriKind.Absolute, out uri)) return false;
            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                string[] pair = part.Split(new[] { '=' }, 2);
                if (pair.Length != 2) continue;
                string key = Uri.UnescapeDataString(pair[0].Replace("+", "%20"));
                string value = Uri.UnescapeDataString(pair[1].Replace("+", "%20"));
                if (key == "address") address = value;
                else if (key == "code") code = value.ToUpperInvariant();
            }
            return ValidServerUrl(address) && code.Length == 6;
        }
        private static string GetLanAddress()
        {
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || n.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                    .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    bool hasGateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                        && !g.Address.Equals(IPAddress.Any));
                    if (!hasGateway) continue;
                    IPAddress address = properties.UnicastAddresses
                        .Select(u => u.Address)
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254."));
                    if (address != null) return address.ToString();
                }
            }
            catch { return "IP_LOCAL_DEL_ANFITRION"; }
            return "IP_LOCAL_DEL_ANFITRION";
        }
    }
}
