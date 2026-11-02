using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 하루에 한 번만 먼저 다가오는 행인이 마지막으로 먼저 말을 건 날짜(기기 날짜)를 기억한다.
    /// 게임을 껐다 켜도 이어지게 서버가 아니라 이 기기(Web 빌드는 브라우저)에 저장한다.
    /// </summary>
    public static class PasserbyOfferRecord
    {
        private const string PreferenceKey = "A305.Npc.PasserbyOfferDates";
        private const string DateFormat = "yyyy-MM-dd";

        // NPC 키 → 마지막으로 먼저 말을 건 날짜. 처음 쓸 때 저장된 값을 읽는다.
        private static Dictionary<string, string> offeredDates;

        public static bool HasOfferedOn(string npcCode, DateTime date)
        {
            return OfferedDates.TryGetValue(npcCode, out string offered) && offered == Format(date);
        }

        public static void Save(string npcCode, DateTime date)
        {
            OfferedDates[npcCode] = Format(date);
            string saved = string.Join(
                "\n",
                OfferedDates.Select(offered => $"{offered.Key}={offered.Value}")
            );
            PlayerPrefs.SetString(PreferenceKey, saved);
            PlayerPrefs.Save();
        }

        /// <summary>저장된 값을 다시 읽는다. 게임을 다시 켠 것처럼 저장을 확인할 때 쓴다.</summary>
        public static void Reload()
        {
            offeredDates = null;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PreferenceKey);
            PlayerPrefs.Save();
            offeredDates = null;
        }

        private static Dictionary<string, string> OfferedDates =>
            offeredDates ??= Read(PlayerPrefs.GetString(PreferenceKey, string.Empty));

        private static Dictionary<string, string> Read(string saved)
        {
            var dates = new Dictionary<string, string>();
            foreach (string line in saved.Split('\n'))
            {
                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    dates[parts[0]] = parts[1];
                }
            }

            return dates;
        }

        private static string Format(DateTime date)
        {
            return date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }
    }
}
