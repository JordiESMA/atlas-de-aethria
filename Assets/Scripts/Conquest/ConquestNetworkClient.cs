using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AuteTurno.Conquest
{
    public sealed class ConquestNetworkClient : MonoBehaviour
    {
        public string BaseUrl = "http://127.0.0.1:3080";
        public string RoomCode { get; private set; }
        public string Token { get; private set; }
        public RemoteGameState State { get; private set; }
        public bool Connected { get; private set; }
        public Action<RemoteGameState, string> OnState;

        private const int RoomTimeout = 8;
        private const string Unreachable = "El anfitrión no responde. Comprueba que la sala siga abierta y que ambos estéis en la misma red privada.";
        private readonly HashSet<UnityWebRequest> pending = new HashSet<UnityWebRequest>();
        private int session;
        private string lastJoinUrl, joinAttempt;

        public void CreateRoom(string playerName)
        {
            Disconnect();
            OpenRoom("/api/rooms", new RoomInput { name = playerName });
        }

        public void JoinRoom(string code, string playerName)
        {
            Disconnect();
            RoomCode = (code ?? "").Trim().ToUpperInvariant();
            string path = "/api/rooms/" + UnityWebRequest.EscapeURL(RoomCode) + "/join";
            string url = BaseUrl.TrimEnd('/') + path;
            if (url != lastJoinUrl)
            {
                lastJoinUrl = url;
                joinAttempt = Guid.NewGuid().ToString("N");
            }
            OpenRoom(path, new RoomInput { name = playerName, joinAttempt = joinAttempt });
        }

        private void OpenRoom(string path, RoomInput input)
        {
            StartCoroutine(Request(path, JsonUtility.ToJson(input), RoomTimeout, (reply, error) =>
            {
                if (error != null || string.IsNullOrEmpty(reply?.token))
                {
                    string message = error ?? "La respuesta del anfitrión no incluye el acceso a la sala.";
                    Debug.LogWarning("Atlas · sala: " + message);
                    OnState?.Invoke(null, message);
                    return;
                }
                Token = reply.token;
                State = reply.state;
                RoomCode = State.code;
                Connected = true;
                lastJoinUrl = null;
                Debug.Log("Atlas · sala: " + State.status + " · J" + (State.yourPlayer + 1));
                OnState?.Invoke(State, "");
                StartCoroutine(PollLoop(session));
            }));
        }

        public void SendAction(string action, int x = -1, int y = -1, int toX = -1, int toY = -1)
        {
            if (!Connected || State == null) { OnState?.Invoke(State, "Conéctate a una sala antes de jugar."); return; }
            var body = new RemoteActionRequest { token = Token, action = action, x = x, y = y, toX = toX, toY = toY, expectedVersion = State.version };
            StartCoroutine(Request("/api/rooms/" + RoomCode + "/actions", JsonUtility.ToJson(body), RoomTimeout, ApplyReply));
        }

        public void Disconnect()
        {
            ++session;
            foreach (var request in pending) request.Abort();
            Connected = false;
            RoomCode = null;
            Token = null;
            State = null;
        }

        private void ApplyReply(RemoteGameEnvelope reply, string error)
        {
            if (reply?.state != null && reply.state.version >= State.version) State = reply.state;
            OnState?.Invoke(State, error ?? "");
        }

        private IEnumerator PollLoop(int activeSession)
        {
            while (activeSession == session && Connected && !State.finished)
            {
                string path = "/api/rooms/" + RoomCode + "?token=" + UnityWebRequest.EscapeURL(Token) + "&after=" + State.version + "&wait=1";
                bool failed = false;
                yield return Request(path, null, 30, (reply, error) =>
                {
                    failed = error != null;
                    ApplyReply(reply, error);
                });
                if (failed) yield return new WaitForSecondsRealtime(2);
            }
        }

        private IEnumerator Request(string path, string json, int timeout, Action<RemoteGameEnvelope, string> completed)
        {
            int activeSession = session;
            UnityWebRequest request = null;
            UnityWebRequestAsyncOperation operation = null;
            string error = null;
            try
            {
                string url = BaseUrl.TrimEnd('/') + path;
                request = UnityWebRequest.Get(url);
                if (json != null)
                {
                    request.method = UnityWebRequest.kHttpVerbPOST;
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.timeout = timeout;
                operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Atlas · no se pudo iniciar la conexión (" + e.GetType().Name + ").");
                error = "El juego no pudo abrir la conexión. Revisa el enlace y la versión instalada.";
            }
            using (request)
            {
                RemoteGameEnvelope reply = null;
                if (operation != null)
                {
                    pending.Add(request);
                    yield return operation;
                    pending.Remove(request);
                    if (activeSession != session) yield break;
                    if (request.responseCode > 0)
                    {
                        try { reply = JsonUtility.FromJson<RemoteGameEnvelope>(request.downloadHandler.text); }
                        catch (ArgumentException) { }
                    }
                    if (request.result == UnityWebRequest.Result.ConnectionError) error = Unreachable;
                    else if (!string.IsNullOrEmpty(reply?.error)) error = reply.error;
                    else if (request.result != UnityWebRequest.Result.Success || reply?.state == null)
                        error = "La dirección respondió, pero no devolvió una partida válida. Pide un enlace nuevo.";
                }
                if (activeSession == session) completed(reply, error);
            }
        }

        [Serializable] private sealed class RoomInput { public string name, joinAttempt; }
    }
}
