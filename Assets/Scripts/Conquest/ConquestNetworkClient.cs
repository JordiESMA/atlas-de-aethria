using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AuteTurno.Conquest
{
    /// <summary>HTTP long-poll client for temporary, code-based game rooms.</summary>
    public sealed class ConquestNetworkClient : MonoBehaviour
    {
        public string BaseUrl = "http://127.0.0.1:3080";
        public string RoomCode { get; private set; }
        public string Token { get; private set; }
        public RemoteGameState State { get; private set; }
        public bool Connected { get; private set; }
        public Action<RemoteGameState, string> OnState;
        private Coroutine pollLoop;

        public void CreateRoom(string playerName)
        { StartCoroutine(RoomRequest("/api/rooms", new RoomInput { name = playerName }, false)); }

        public void JoinRoom(string code, string playerName)
        { RoomCode = (code ?? "").Trim().ToUpperInvariant(); StartCoroutine(RoomRequest("/api/rooms/" + UnityWebRequest.EscapeURL(RoomCode) + "/join", new RoomInput { name = playerName }, true)); }

        public void SendAction(string action, int x = -1, int y = -1, int toX = -1, int toY = -1)
        {
            if (!Connected || State == null) { OnState?.Invoke(State, "Conéctate a una sala antes de jugar."); return; }
            var body = new RemoteActionRequest { token = Token, action = action, x = x, y = y, toX = toX, toY = toY, expectedVersion = State.version };
            StartCoroutine(PostAction(body));
        }

        public void Disconnect()
        {
            Connected = false;
            if (pollLoop != null) { StopCoroutine(pollLoop); pollLoop = null; }
            RoomCode = null; Token = null; State = null;
        }

        private IEnumerator RoomRequest(string path, RoomInput input, bool joining)
        {
            Connected = false;
            string url = CleanBaseUrl() + path;
            using (var request = CreatePost(url, JsonUtility.ToJson(input)))
            {
                yield return request.SendWebRequest();
                RemoteGameEnvelope envelope = Parse(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || envelope == null || envelope.state == null || string.IsNullOrEmpty(envelope.token))
                { OnState?.Invoke(State, ErrorText(request, envelope)); yield break; }
                Token = envelope.token; State = envelope.state; RoomCode = envelope.state.code; Connected = true;
                OnState?.Invoke(State, "");
                if (pollLoop != null) StopCoroutine(pollLoop);
                pollLoop = StartCoroutine(PollLoop());
            }
        }

        private IEnumerator PostAction(RemoteActionRequest body)
        {
            using (var request = CreatePost(CleanBaseUrl() + "/api/rooms/" + UnityWebRequest.EscapeURL(RoomCode) + "/actions", JsonUtility.ToJson(body)))
            {
                yield return request.SendWebRequest();
                RemoteGameEnvelope envelope = Parse(request.downloadHandler.text);
                if (envelope != null && envelope.state != null) { State = envelope.state; OnState?.Invoke(State, envelope.error ?? ""); }
                else OnState?.Invoke(State, ErrorText(request, envelope));
            }
        }

        private IEnumerator PollLoop()
        {
            while (Connected && State != null && !State.finished)
            {
                string url = CleanBaseUrl() + "/api/rooms/" + UnityWebRequest.EscapeURL(RoomCode)
                    + "?token=" + UnityWebRequest.EscapeURL(Token) + "&after=" + State.version + "&wait=1";
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 30;
                    yield return request.SendWebRequest();
                    if (!Connected) yield break;
                    RemoteStateEnvelope envelope = null;
                    try { envelope = JsonUtility.FromJson<RemoteStateEnvelope>(request.downloadHandler.text); } catch { }
                    if (request.result == UnityWebRequest.Result.Success && envelope != null && envelope.state != null)
                    {
                        State = envelope.state; OnState?.Invoke(State, "");
                    }
                    else OnState?.Invoke(State, ErrorText(request, envelope));
                }
                if (!Connected || State == null || State.finished) yield break;
            }
        }

        private UnityWebRequest CreatePost(string url, string json)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 30;
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private string CleanBaseUrl() => (BaseUrl ?? "").Trim().TrimEnd('/');
        private static RemoteGameEnvelope Parse(string json)
        { try { return JsonUtility.FromJson<RemoteGameEnvelope>(json); } catch { return null; } }
        private static string ErrorText(UnityWebRequest request, RemoteGameEnvelope envelope)
        {
            if (envelope != null && !string.IsNullOrEmpty(envelope.error)) return envelope.error;
            return request.result == UnityWebRequest.Result.Success ? "El servidor devolvió una respuesta incompleta." : "No se pudo conectar con " + request.url + " (" + request.error + ").";
        }
        private static string ErrorText(UnityWebRequest request, RemoteStateEnvelope envelope)
        {
            if (envelope != null && !string.IsNullOrEmpty(envelope.error)) return envelope.error;
            return ErrorText(request, (RemoteGameEnvelope)null);
        }

        [Serializable] private sealed class RoomInput { public string name; }
        [Serializable] private sealed class RemoteStateEnvelope { public RemoteGameState state; public string error; }
    }
}
