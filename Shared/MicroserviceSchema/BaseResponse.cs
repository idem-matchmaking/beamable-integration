using System;
using UnityEngine;

namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class BaseResponse
    {
        public static readonly BaseResponse Success = new(true);
        public static readonly BaseResponse UnauthorizedFailure = new(false, "Unauthorized access is not supported");
        public static readonly BaseResponse UnsupportedGameModeFailure = new(false, "Unsupported game mode");
        public static readonly BaseResponse IdemConnectionFailure = new(false, "No connection to Idem");
        public static readonly BaseResponse InternalErrorFailure = new(false, "Internal error");
        public static readonly BaseResponse UnknownMatchFailure = new(false, "Unknown match");
        public static readonly BaseResponse UnknownPlayerFailure = new(false, "Unknown player");

        public bool success;
        public string error;
        public string type;
        
        public BaseResponse()
        {
        }

        public BaseResponse(bool success, string error = null)
        {
            this.success = success;
            this.error = error;
            this.type = GetType().Name;
        }
        
        public static BaseResponse FromJson(string json)
        {
            try
            {
                var baseResponse = JsonUtil.Parse<BaseResponse>(json);
                return baseResponse.type switch
                {
                    nameof(StringResponse) => JsonUtil.Parse<StringResponse>(json),
                    nameof(MMStateResponse) => JsonUtil.Parse<MMStateResponse>(json),
                    nameof(ConfirmMatchResponse) => JsonUtil.Parse<ConfirmMatchResponse>(json),
                    _ => baseResponse
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not deserialize BaseResponse: {e.Message}");
                return null;
            }
        }
    }
}