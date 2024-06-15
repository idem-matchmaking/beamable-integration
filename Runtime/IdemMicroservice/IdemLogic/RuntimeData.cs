using System;
using System.Collections.Generic;
using System.Linq;
using Beamable.Common.Runtime.Collections;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;

namespace Beamable.Microservices.Idem.IdemLogic
{
	internal class GameModeContainer
	{
		/**
		 * Players with AddPlayer sent but no match suggestions received
		 */
		public readonly ConcurrentDictionary<string, WaitingPlayer> waitingPlayers = new();

		/**
		 * Players with match suggestions received but not confirmed
		 */
		public readonly ConcurrentDictionary<string, CachedMatch> pendingMatches = new();

		/**
		 * Players with confirmed matches without completion
		 */
		public readonly ConcurrentDictionary<string, CachedMatch> activeMatches = new();
	}

	internal class WaitingPlayer
	{
		public readonly string playerId;
		public DateTime lastSeen;
		public bool isInactive;

		public WaitingPlayer(string playerId)
		{
			this.playerId = playerId;
			lastSeen = DateTime.Now;
		}
	}

	internal class CachedMatch
	{
		public readonly string gameId;
		public readonly string matchId;
		public readonly IdemPlayer[] players;
		public readonly bool[] confirmed;
		public readonly bool[] playerLeft;
		public readonly DateTime[] lastSeen;
		public readonly DateTime createdAt;
		public DateTime activatedAt { get; private set; }
		public bool isActive { get; private set; }
		public bool isCompleted;

		public bool ConfirmedByAll => confirmed.All(t => t);
		public bool hasPlayerLeft => playerLeft.Any(t => t);

		public CachedMatch(string gameId, Match match)
		{
			this.gameId = gameId;
			matchId = match.uuid;
			createdAt = DateTime.Now;
			
			var players = new List<IdemPlayer>();
			for (int i = 0; i < match.teams.Length; i++)
			{
				foreach (var p in match.teams[i].players)
				{
					players.Add(new IdemPlayer(i, p.playerId));
				}
			}
			
			this.players = players.ToArray();

			confirmed = new bool[this.players.Length];
			playerLeft = new bool[this.players.Length];
			lastSeen = new DateTime[this.players.Length];
		}

		public void ConfirmBy(string playerId)
		{
			for (int i = 0; i < players.Length; i++)
			{
				if (players[i].playerId == playerId)
				{
					confirmed[i] = true;
				}
			}
		}

		public void Seen(string playerId)
		{
			for (int i = 0; i < players.Length; i++)
			{
				if (players[i].playerId == playerId)
				{
					lastSeen[i] = DateTime.Now;
					return;
				}
			}
		}

		public void Activate()
		{
			isActive = true;
			activatedAt = DateTime.Now;
		}

		public void PlayerLeft(string playerId)
		{
			for (int i=0; i<players.Length; i++)
			{
				if (players[i].playerId == playerId)
				{
					playerLeft[i] = true;
				}
			}
		}

		public void PlayersLeft(List<string> timeoutedList)
		{
			for (int i=0; i<players.Length; i++)
			{
				if (timeoutedList.Contains(players[i].playerId))
				{
					playerLeft[i] = true;
				}
			}
		}
	}
}