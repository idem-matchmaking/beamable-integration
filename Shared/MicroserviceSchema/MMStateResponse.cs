using System.Collections.Generic;
using System.Linq;

namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class MMStateResponse : BaseResponse
    {
        public bool inQueue;
        public bool matchFound;
        public bool matchReady;
        public bool timeout;
        public string gameMode;
        public string matchId;
        public string server;
        public IdemPlayer[] players;

        public MMStateResponse()
        {
        }

        public MMStateResponse(bool inQueue, bool matchFound, bool matchReady, bool timeout, string gameMode = "", string matchId = "", string server = "", IdemPlayer[] players = null) : base(true)
        {
            this.inQueue = inQueue;
            this.matchFound = matchFound;
            this.matchReady = matchReady;
            this.timeout = timeout;
            this.matchReady = matchReady;
            this.gameMode = gameMode;
            this.matchId = matchId;
            this.server = server;
            this.players = players;
        }

        public static MMStateResponse None() => new(false, false, false, false);
        public static MMStateResponse InQueue(string gameMode) => new(true, false, false, false, gameMode);

        public static MMStateResponse MatchFound(string gameMode, string matchId, string server, IEnumerable<IdemPlayer> players)
            => new(false, true, false, false, gameMode, matchId, server, players.ToArray());
        
        public static MMStateResponse MatchReady(string gameMode, string matchId, string server, IEnumerable<IdemPlayer> players)
            => new(false, true, true, false, gameMode, matchId, server, players.ToArray());
        
        public static MMStateResponse Timeout() => new(false, false, false, true);
    }
}