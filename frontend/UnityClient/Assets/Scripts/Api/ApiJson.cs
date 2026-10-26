using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace A305.Api
{
    /// <summary>
    /// 서버 API의 JSON 규칙. 필드명은 camelCase, enum은 문자열(<c>[EnumMember]</c>로 서버 값 지정)이고, 값이 없는 필드는 보내지 않는다.
    /// </summary>
    public static class ApiJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }
    }
}
