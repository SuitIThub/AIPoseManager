using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StudioPoseBridge.Http
{
    internal static class JsonHelper
    {
        public static byte[] SerializeToUtf8(ApiResponse response)
        {
            var dict = new Dictionary<string, object>
            {
                ["ok"] = response.ok
            };
            if (response.data != null) dict["data"] = response.data;
            if (!string.IsNullOrEmpty(response.error)) dict["error"] = response.error;
            if (!string.IsNullOrEmpty(response.code)) dict["code"] = response.code;
            var json = MiniJson.Serialize(dict);
            return Encoding.UTF8.GetBytes(json);
        }

        public static Dictionary<string, object> DeserializeObject(string json)
        {
            var o = MiniJson.Deserialize(json);
            if (o is Dictionary<string, object> d) return d;
            throw new System.Exception("Expected JSON object");
        }

        public static Dictionary<string, object> DeserializeObject(Stream stream)
        {
            string text;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                text = reader.ReadToEnd();
            return DeserializeObject(text);
        }
    }
}
