namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class QueueCountResponse : BaseResponse
    {
        public int count;

        public QueueCountResponse()
        {
        }

        public QueueCountResponse(int count) : base(true)
        {
            this.count = count;
        }
    }
}