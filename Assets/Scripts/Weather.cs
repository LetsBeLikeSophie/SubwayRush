using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// 서울 실시간 날씨. 못 가져오면 달별 평균 기온으로 대신한다
public static class Weather
{
    public const int Normal = 0, Short = 1, Coat = 2, Padding = 3;
    static readonly float[] MonthAvg = { -2, 1, 6, 13, 18, 23, 26, 27, 22, 15, 7, 0 };
    const string Url = "https://api.open-meteo.com/v1/forecast?latitude=37.57&longitude=126.98&current=temperature_2m,weather_code";

    public static float temp = MonthAvg[DateTime.Now.Month - 1];
    public static bool rain, snow;
    static int code = 2;
    static float fetchedAt = -9999;

    [Serializable] class Current { public float temperature_2m; public int weather_code; }
    [Serializable] class Reply { public Current current; }

    public static bool Wet => rain || snow;
    public static string Label => (snow ? "눈" : rain ? "비" : code <= 1 ? "맑음" : "흐림") + " " + Mathf.RoundToInt(temp) + "°";

    public static void Set(float t, int wmo)
    {
        temp = t; code = wmo;
        snow = wmo >= 71 && wmo <= 77 || wmo == 85 || wmo == 86;
        rain = wmo >= 51 && wmo <= 67 || wmo >= 80 && wmo <= 82 || wmo >= 95;
    }

    public static IEnumerator Fetch()
    {
        if (Time.realtimeSinceStartup - fetchedAt < 1800) yield break;
        fetchedAt = Time.realtimeSinceStartup;
        using (var r = UnityWebRequest.Get(Url))
        {
            r.timeout = 5;
            yield return r.SendWebRequest();
            if (r.result != UnityWebRequest.Result.Success) yield break;
            var reply = JsonUtility.FromJson<Reply>(r.downloadHandler.text);
            if (reply?.current != null) Set(reply.current.temperature_2m, reply.current.weather_code);
        }
    }

    // 기온에 맞춰 입는다. 같은 날에도 옷은 사람마다 조금씩 다르다
    public static int PickOutfit()
    {
        float v = UnityEngine.Random.value;
        if (temp <= 5) return v < .7f ? Padding : Coat;
        if (temp <= 12) return v < .25f ? Padding : v < .85f ? Coat : Normal;
        if (temp <= 18) return v < .3f ? Coat : Normal;
        if (temp >= 25) return v < .75f ? Short : Normal;
        return Normal;
    }

    public static float DamageMul(int outfit) { return outfit == Padding ? .7f : outfit == Coat ? .88f : outfit == Short ? 1.15f : 1; }
    public static float KnockMul(int outfit) { return outfit == Padding ? .8f : 1; }
}
