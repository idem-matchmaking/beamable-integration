using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable;
using Beamable.Coroutines;
using Beamable.Microservices.Idem.Shared;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;
using Beamable.Server.Clients;
using UnityEngine;

namespace Idem
{
    public class IdemService
    {
        public bool IsMatchmaking { get; private set; }

        public MatchInfo? CurrentMatchInfo { get; private set; } = null;
        /**
         * Event fired when a match is found, but not all players confirmed ready.
         */
        public event Action<MatchInfo> OnMatchFound;
        /**
         * Event fired when all players confirmed ready and the match is ready to start.
         */
        public event Action<MatchInfo> OnMatchReady;

        private readonly float MatchmakingPollIntervalS = 2f;
        private readonly BeamContext ctx;
        private readonly IdemMicroserviceClient idemClient;
        private readonly CoroutineService coroutineService;

        private bool isPlaying;
        private Coroutine matchmakingCoroutine = null;

        public IdemService(BeamContext ctx, IdemMicroserviceClient idemClient, CoroutineService coroutineService)
        {
            this.ctx = ctx;
            this.idemClient = idemClient;
            this.coroutineService = coroutineService;
        }

        public async Task<bool> StartMatchmaking(string gameMode, params string[] availableServers)
        {
            if (IsMatchmaking)
            {
                Debug.LogWarning($"Trying to start matchmaking while already matchmaking");
                return true;
            }

            if (isPlaying)
            {
                Debug.LogError($"Cannot start matchmaking while playing. Call {nameof(CompleteMatch)}first.");
                return false;
            }
            
            try
            {
                Reset();
                
                IsMatchmaking = true;
                var response = await idemClient.StartMatchmaking(gameMode, availableServers);

                if (JsonUtil.TryParse<BaseResponse>(response, out var parsed) && !parsed.success)
                    Debug.LogError($"Error starting matchmaking: {parsed.error}");

                IsMatchmaking = parsed.success;
                
                if (IsMatchmaking)
                {
                    matchmakingCoroutine = coroutineService.StartCoroutine(MatchmakingCoroutine());
                }
                
                return parsed.success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error starting matchmaking: {e.Message}");
                IsMatchmaking = false;
                return false;
            }
        }

        public async Task<bool> StopMatchmaking()
        {
            if (!IsMatchmaking)
            {
                Debug.LogWarning($"Trying to stop matchmaking while not matchmaking");
                return true;
            }
            
            try
            {
                var response = await idemClient.StopMatchmaking();
                if (!JsonUtil.TryParse<BaseResponse>(response, out var parsed))
                    return false;

                if (!parsed.success)
                    Debug.LogError($"Error stopping matchmaking: {parsed.error}");

                if (matchmakingCoroutine != null)
                {
                    coroutineService.StopCoroutine(matchmakingCoroutine);
                    matchmakingCoroutine = null;
                }

                if (parsed.success)
                    Reset();
                
                return parsed.success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error stopping matchmaking: {e.Message}");
                return false;
            }
        }

        public async Task<bool> CompleteMatch(float gameLength, Dictionary<int, int> teamsRank, Dictionary<string, float> playersScore)
        {
            if (CurrentMatchInfo == null || !isPlaying)
            {
                Debug.LogError($"Trying to complete match without a match");
                return false;
            }

            var match = CurrentMatchInfo.Value;
            var payload = new IdemMatchResult();
            payload.gameId = match.gameMode;
            payload.matchId = match.matchId;
            payload.server = match.server;
            payload.gameLength = gameLength;
            var teamIds = match.players.Select(p => p.teamId).Distinct().ToArray();
            payload.teams = new IdemTeamResult[teamIds.Length];
            for (var i = 0; i < teamIds.Length; i++)
            {
                var teamId = teamIds[i];
                var teamRank = teamsRank.GetValueOrDefault(teamId);
                var teamPlayers = match.players
                    .Where(p => p.teamId == teamId)
                    .Select(p => new IdemPlayerResult
                    {
                        playerId = p.playerId,
                        score = playersScore.GetValueOrDefault(p.playerId)
                    })
                    .ToArray();
                payload.teams[i] = new IdemTeamResult
                {
                    rank = teamRank,
                    players = teamPlayers
                };
            }

            try
            {
                var response = await idemClient.CompleteMatch(payload.ToJson());
                if (!JsonUtil.TryParse<BaseResponse>(response, out var parsed))
                    return false;

                if (!parsed.success)
                    Debug.LogError($"Error completing match: {parsed.error}");

                return parsed.success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error stopping matchmaking: {e.Message}");
                return false;
            }
            finally
            {
                Reset();
            }
        }

        private void Reset()
        {
            isPlaying = false;
            IsMatchmaking = false;
            CurrentMatchInfo = null;
        }

        private IEnumerator MatchmakingCoroutine()
        {
            var minWaitingTime = Time.realtimeSinceStartup + 5f;
            float lastRequestTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < minWaitingTime ||
                IsMatchmaking || CurrentMatchInfo != null && !isPlaying)
            {
                var timeLeftToWait = lastRequestTime + MatchmakingPollIntervalS - Time.realtimeSinceStartup;
                yield return new WaitForSeconds(timeLeftToWait);

                lastRequestTime = Time.realtimeSinceStartup;
                var response = idemClient.GetMatchmakingStatus();
                while (!response.IsCompleted)
                    yield return null;

                Debug.Log($"[{Time.realtimeSinceStartup:F}] MM state: {response.GetResult()}");
                if (!response.IsFailed &&
                    JsonUtil.TryParse<MMStateResponse>(response.GetResult(), out var parsed))
                {
                    IsMatchmaking = parsed.inQueue;
                    
                    if (parsed.matchReady && CurrentMatchInfo != null)
                    {
                        StartPlaying();
                        yield break;
                    }
                    
                    if (parsed.matchFound && CurrentMatchInfo == null)
                    {
                        CurrentMatchInfo = new MatchInfo(parsed);
                        OnMatchFound?.Invoke(CurrentMatchInfo.Value);
                        yield return ConfirmMatch();
                    }

                    if (!parsed.matchFound && !parsed.matchReady && CurrentMatchInfo != null)
                    {
                        CurrentMatchInfo = null;
                    }
                }
            }
        }

        private void StartPlaying()
        {
            if (CurrentMatchInfo == null)
            {
                Debug.LogError($"Trying to start playing without a match");
                return;
            }
            
            isPlaying = true;
            IsMatchmaking = false;
            CurrentMatchInfo = CurrentMatchInfo.Value.Ready();
            OnMatchReady?.Invoke(CurrentMatchInfo.Value);
        }

        private IEnumerator ConfirmMatch()
        {
            if (CurrentMatchInfo == null)
            {
                Debug.LogWarning($"Trying to confirm match without a match");
                yield break;
            }

            var response = idemClient.ConfirmMatch(CurrentMatchInfo.Value.matchId);
            while (!response.IsCompleted)
                yield return null;
            
            if (response.IsFailed ||
                !JsonUtil.TryParse<ConfirmMatchResponse>(response.GetResult(), out var parsed) ||
                !parsed.canStartMatch)
                yield break;
            
            StartPlaying();
        }

        public readonly struct MatchInfo
        {
            public readonly bool ready;
            public readonly string gameMode;
            public readonly string matchId;
            public readonly string server;
            public readonly IReadOnlyList<MatchPlayer> players;
            
            public MatchInfo(MMStateResponse response)
            {
                ready = false;
                gameMode = response.gameMode;
                matchId = response.matchId;
                server = response.server;
                
                var p = new MatchPlayer[response.players.Length];
                for (var i = 0; i < response.players.Length; i++)
                {
                    p[i] = new MatchPlayer(response.players[i].teamId, response.players[i].playerId);
                }

                players = p;
            }

            private MatchInfo(bool ready, MatchInfo other)
            {
                this.ready = ready;
                gameMode = other.gameMode;
                matchId = other.matchId;
                server = other.server;
                players = other.players;
            }

            public MatchInfo Ready()
            {
                return new MatchInfo(true, this);
            }
        }

        public readonly struct MatchPlayer
        {
            public readonly int teamId;
            public readonly string playerId;

            public MatchPlayer(int teamId, string playerId)
            {
                this.teamId = teamId;
                this.playerId = playerId;
            }
        }
    }
}
