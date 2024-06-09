using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Beamable.Microservices.Idem.Shared;
using UnityEngine;

namespace Beamable.Microservices.Idem.Tools
{
    public static class AWSAuth
    {
        public class AuthPayload
        {
            public AuthParams AuthParameters;
            public string AuthFlow;
            public string ClientId;
        }

        public class AuthParams
        {
            public string USERNAME;
            public string PASSWORD;
        }

        public class AuthResponse
        {
            public AuthTokenHolder AuthenticationResult;
        }

        public class AuthTokenHolder
        {
            public string IdToken;
        }

        public static async Task<string> AuthAndGetToken(string username, string password, string clientId, bool debug = false)
        {
            var authParams = new AuthParams
            {
                USERNAME = username,
                PASSWORD = password
            };
            var authPayload = new AuthPayload
            {
                AuthParameters = authParams,
                AuthFlow = "USER_PASSWORD_AUTH",
                ClientId = clientId
            };
            var json = authPayload.ToJson();

            if (debug)
                Debug.Log($"[DEBUG] Auth Message: {json}");

            var authResponse = await MakePostRequest<AuthResponse, AuthPayload>(
                "https://cognito-idp.eu-central-1.amazonaws.com/",
                authPayload,
                ("X-Amz-Target", "AWSCognitoIdentityProviderService.InitiateAuth"),
                "application/x-amz-json-1.1"
            );
            
            if (debug)
                Debug.Log($"IdToken: {authResponse.AuthenticationResult.IdToken}");
            
            return authResponse.AuthenticationResult.IdToken;
        }

        private static async Task<T> MakePostRequest<T, TP>(string url, TP param, (string key, string val) header,
            string contentType, bool debug = false)
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Add(header.key, header.val);

            var json = param.ToJson();
            var content = new StringContent(json, Encoding.UTF8, contentType);

            if (debug)
                Debug.Log($"Sending request to {url} with body: {json}");
            
            var response = await client.PostAsync(url, content);
            
            if (debug)
                Debug.Log($"Response: {response}");
            
            var responseString = await response.Content.ReadAsStringAsync();

            return JsonUtil.Parse<T>(responseString);
        }
    }
}