namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class BackfillingData
    {
        public string matchId;
        public string backfillingRequestId;
        public string droppedPlayerId;
        public ScoreData[] matchScores;

        public BackfillingData()
        {
        }

        public BackfillingData(string matchId, string backfillingRequestId, string droppedPlayerId, ScoreData[] matchScores)
        {
            this.matchId = matchId;
            this.backfillingRequestId = backfillingRequestId;
            this.droppedPlayerId = droppedPlayerId;
            this.matchScores = matchScores;
        }
    }
    
    public class ScoreData
    {
        public float score;
        public IdemPlayerResult[] players;
    }
}