using System;
using UnityEngine;

namespace Beamable.Microservices.Idem.Shared
{
    public static class JsonUtil
    {
        public static bool TryParse<T>(string json, out T result)
        {
            try
            {
                result = CompactJson.Serializer.Parse<T>(json);
                return true;
            }
            catch (Exception e)
            {
                result = default;
                return false;
            }
        }
        
        public static T Parse<T>(string json)
        {
            try
            {
                return CompactJson.Serializer.Parse<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not parse json '{json}' to {typeof(T).Name}: {e.Message}\n{e.StackTrace}");
                return default;
            }
        }
    }

    public static class Extentions
    {
        public static string ToJson(this object obj, bool pretty = false)
            => CompactJson.Serializer.ToString(obj, pretty);
    }
}