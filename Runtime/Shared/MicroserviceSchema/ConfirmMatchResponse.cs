namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class ConfirmMatchResponse : BaseResponse
    {
        public static readonly ConfirmMatchResponse MatchConfirmed = new(true);
        public static readonly ConfirmMatchResponse MatchNotConfirmed = new(false);
        
        public bool canStartMatch;
        
        public ConfirmMatchResponse()
        {
        }
        
        public ConfirmMatchResponse(bool canStartMatch) : base(true)
        {
            this.canStartMatch = canStartMatch;
        }
    }
}