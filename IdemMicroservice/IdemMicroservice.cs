using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable.Microservices.Idem.IdemLogic;
using Beamable.Microservices.Idem.Shared;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;
using Beamable.Microservices.Idem.Tools;
using Beamable.Server;
using Beamable.Server.Api.RealmConfig;
using UnityEngine;
using WebSocketSharp;

namespace Beamable.Microservices
{
	[Microservice("IdemMicroservice")]
	public class IdemMicroservice : Microservice
	{
        // microsservice config keys
        private const string ConfigNamespace = "Idem";
        private const string IdemUsernameConfigKey = "Username";
        private const string IdemPasswordConfigKey = "Password";
        private const string IdemClientIdConfigKey = "ClientId";
        private const string DebugConfigKey = "Debug";
        private const string SupportedGameModesConfigKey = "SupportedGameModes";
        private const string PlayerTimoutMsConfigKey = "PlayerTimeoutMs";
        private const string GlobalMatchTimeoutSConfigKey = "GlobalMatchTimeoutS";
        
        // microservice state
        private static bool debug = false;
        private static WebSocket ws = null;
        private static Task<bool> connectionTask = null;
        private static IdemLogic logic = null;
        private static readonly List<string> gameModes = new();
        private static DateTime lastInitStartedAt;
        
        // convenience
        private static readonly TimeSpan InitRetryDelay = TimeSpan.FromSeconds(10);
        private string playerId => Context.UserId == 0 ? null : Context.UserId.ToString();

        [ClientCallable]
        public async Task<string> DebugGetPlayers(string gameId)
        {
            if (!debug)
                return string.Empty;
            
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = await logic.GetPlayers(gameId);
            return result.ToJson();
        }

        [ClientCallable]
        public async Task<string> DebugGetMatches(string gameId)
        {
            if (!debug)
                return string.Empty;
            
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = await logic.GetMatches(gameId);
            return result.ToJson();
        }

        [ClientCallable]
        public async Task<string> DebugGetRecentPlayer(string anyPlayerId)
        {
            if (!debug)
                return string.Empty;
            
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.GetRecentPlayer(anyPlayerId);
            return result.ToJson();
        }
        
        [ClientCallable]
        public async Task<string> StartMatchmaking(string gameMode, string[] servers)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.StartMatchmaking(playerId, gameMode, servers);
            var response = result.ToJson();
            if (debug)
                Debug.Log($"StartMatchmaking called for playerId {playerId}, gameMode {gameMode}, servers {string.Join(", ", servers)}, response: {response}");
            return response;
        }
        
        [ClientCallable]
        public async Task<string> StopMatchmaking()
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.StopMatchmaking(playerId);
            var response = result.ToJson();
            if (debug)
                Debug.Log($"StopMatchmaking called for playerId {playerId}, response: {response}");
            return response;
        }
        
        [ClientCallable]
        public async Task<string> GetMatchmakingStatus()
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.GetMatchmakingStatus(playerId);
            return result.ToJson();
        }
        
        [ClientCallable]
        public async Task<string> ConfirmMatch(string matchId)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.ConfirmMatch(playerId, matchId);
            var response = result.ToJson();
            if (debug)
                Debug.Log($"ConfirmMatch called for playerId {playerId}, matchId {matchId}, response: {response}");
            return response;
        }
        
        
        [ClientCallable]
        public async Task<string> CompleteMatch(string payload)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var matchResult = JsonUtil.Parse<IdemMatchResult>(payload);

            var result = logic.CompleteMatch(playerId, matchResult);
            var response = result.ToJson();
            if (debug)
                Debug.Log($"CompleteMatch called for playerId {playerId}, matchId {matchResult.matchId}, response: {response}");
            return response;
        }

        private async Task<string> CheckConnection()
        {
            if (connectionTask == null)
            {
                await Initialize();
            }

            if (connectionTask == null || !await connectionTask)
            {
                // init failed
                return BaseResponse.InternalErrorFailure.ToJson();
            }

            return null;
        }
        
        private async void InitAfterDelay()
        {
            await Task.Delay(2000);
            await Initialize();
        }

        private async Task Initialize()
        {
            if (connectionTask != null)
            {
                if (!connectionTask.IsCompleted || connectionTask.Result ||
                    DateTime.Now - lastInitStartedAt < InitRetryDelay)
                    return;
            }

            if (Services.RealmConfig == null)
            {
                Debug.LogWarning($"Initialization attempt with null RealmConfig");
                return;
            }

            lastInitStartedAt = DateTime.Now;
            var connectionCompletion = new TaskCompletionSource<bool>();
                
            try
            {
                connectionTask = connectionCompletion.Task;

                var config = await Services.RealmConfig.GetRealmConfigSettings();
                debug = !string.IsNullOrWhiteSpace(config.GetSetting(ConfigNamespace, DebugConfigKey));
                var idemUsername = config.GetSetting(ConfigNamespace, IdemUsernameConfigKey);
                var idemPassword = config.GetSetting(ConfigNamespace, IdemPasswordConfigKey);
                var idemClientId = config.GetSetting(ConfigNamespace, IdemClientIdConfigKey);
                var gameModesStr = config.GetSetting(ConfigNamespace, SupportedGameModesConfigKey);
                if (string.IsNullOrWhiteSpace(idemUsername) || string.IsNullOrWhiteSpace(idemPassword) ||
                    string.IsNullOrWhiteSpace(idemClientId) || string.IsNullOrWhiteSpace(gameModesStr))
                {
                    Fail(
                        $"Idem config is not complete: {IdemUsernameConfigKey}, {IdemPasswordConfigKey}, {IdemClientIdConfigKey}, {SupportedGameModesConfigKey} are required");
                    return;
                }

                var token = await AWSAuth.AuthAndGetToken(idemUsername, idemPassword, idemClientId, debug);
                if (string.IsNullOrWhiteSpace(token))
                {
                    Fail($"Could not authorize with AWS Cognito: response token is empty");
                    return;
                }

                gameModes.Clear();
                gameModes.AddRange(gameModesStr
                    .Split(",")
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                );

                Debug.Log($"Supported game modes: {string.Join(", ", gameModes.Select(s => $"'{s}'"))}");
                if (gameModes.Count == 0)
                {
                    Fail($"No supported game modes. Halt.");
                    return;
                }

                const int defaultPlayerTimeoutMs = 5000;
                var playerTimeoutMs = ParseConfigInt(config, PlayerTimoutMsConfigKey, defaultPlayerTimeoutMs);

                const int defaultGlobalMatchTimeoutS = 86400;
                var globalMatchTimeoutS =
                    ParseConfigInt(config, GlobalMatchTimeoutSConfigKey, defaultGlobalMatchTimeoutS);

                logic ??= new(debug, playerTimeoutMs, globalMatchTimeoutS, SendThroughWs);
                logic.UpdateSupportedGameModes(gameModes);

                // TODO_IDEM implement multiple game modes in the push mode
                var pushParams = $"receiveMatches=true&gameMode={gameModes[0]}&";
                var connectionUrl = $"wss://ws-int.idem.gg/?{pushParams}authorization={token}";
                Debug.Log($"Starting Idem connection to: {connectionUrl}");

                ws = new WebSocket(connectionUrl);
                ws.OnOpen += (sender, param) =>
                {
                    connectionCompletion.SetResult(ws.ReadyState == WebSocketState.Open);
                    Debug.Log("Idem WS is open");
                    logic.InitialGetMatches(gameModes);
                };
                ws.OnClose += OnWsClose;
                ws.OnMessage += OnWsMessage;
                ws.OnError += OnWsError;

                ws.Connect();

                Debug.Log($"Idem WS state: {ws.ReadyState}");
            }
            catch (Exception e)
            {
                Fail($"Initialization exception: {e.Message}\n{e.StackTrace}");
            }

            return;

            void Fail(string message)
            {
                Debug.LogError(message);
                connectionCompletion.SetResult(false);
            }
        }

        private int ParseConfigInt(RealmConfig config, string keyName, int defaultValue)
        {
            var playerTimeoutMsStr = config.GetSetting(ConfigNamespace, keyName);
            if (!int.TryParse(playerTimeoutMsStr, out var parsedValue))
            {
                Debug.LogWarning($"Could not parse {keyName} value: '{playerTimeoutMsStr}', using default {defaultValue}");
                parsedValue = defaultValue;
            }

            return parsedValue;
        }

        private bool SendThroughWs(object payload)
        {
            if (payload == null)
            {
                Debug.LogError($"Trying to send null payload to Idem WS");
                return false;
            }
            
            var json = payload.ToJson();
            if (debug)
                Debug.Log($"Sending message to Idem: {json}");

            if (ws is not { ReadyState: WebSocketState.Open })
            {
                Debug.LogError($"Trying to send payload with Idem WS in the wrong state '{ws?.ReadyState}'");
                return false;
            }

            ws.Send(json);

            return true;
        }

        private void OnWsError(object sender, ErrorEventArgs errorArgs)
        {
            Debug.LogError(
                $"Idem WS error: {errorArgs.Message}\n" +
                $"{errorArgs.Exception.StackTrace}\n" +
                $"Inner:{errorArgs.Exception.InnerException?.Message}\n" +
                $"Inner stack:{errorArgs.Exception.InnerException?.StackTrace}");
            Debug.Log($"Idem WS state: {ws.ReadyState}");
        }

        private void OnWsClose(object sender, CloseEventArgs closeArgs)
        {
            Debug.Log($"Idem WS is closed: {closeArgs.Code}, reason {closeArgs.Reason}, clean {closeArgs.WasClean}");
            ws = null;
            connectionTask = null;
            InitAfterDelay();
        }

        private void OnWsMessage(object sender, MessageEventArgs messageArgs)
        {
            logic?.HandleIdemMessage(messageArgs.Data);
        }
	}
}
