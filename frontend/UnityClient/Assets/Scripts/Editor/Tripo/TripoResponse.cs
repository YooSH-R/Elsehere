using System;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoResponse
    {
        public static T Read<T>(string json, string apiKey = null)
            where T : class
        {
            var response = new Envelope<T> { code = int.MinValue };
            try
            {
                JsonUtility.FromJsonOverwrite(json, response);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Tripo 응답을 해석할 수 없습니다.", exception);
            }
            if (response.code == int.MinValue)
            {
                throw new FormatException("Tripo 응답이 비어 있습니다.");
            }
            if (response.code != 0)
            {
                string guidance =
                    response.code == 2010 ? "크레딧 잔액을 확인하세요."
                    : response.code == 1000 || response.code == 1001 ? "API 키를 확인하세요."
                    : "Tripo 콘솔에서 오류를 확인하세요.";
                if (!string.IsNullOrEmpty(apiKey))
                {
                    guidance += "\n" + SafeDetail(response.message, apiKey);
                    guidance += "\n" + SafeDetail(response.suggestion, apiKey);
                    if (!string.IsNullOrEmpty(response.request_id))
                        guidance += "\n요청 ID: " + SafeDetail(response.request_id, apiKey);
                }
                throw new TripoRequestRejectedException(response.code, guidance.Trim());
            }
            return response.data ?? throw new FormatException("Tripo 응답에 결과가 없습니다.");
        }

        private static string SafeDetail(string value, string apiKey)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            string redacted = value.Replace(apiKey, "[숨김]").Replace("\r", " ").Replace("\n", " ");
            return redacted.Length > 600 ? redacted.Substring(0, 600) + "…" : redacted;
        }

        [Serializable]
        private sealed class Envelope<T>
        {
            public int code;
            public string message = null;
            public string suggestion = null;
            public string request_id = null;
            public T data = default;
        }
    }
}
