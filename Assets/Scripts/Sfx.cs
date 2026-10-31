using System.Collections.Generic;
using UnityEngine;

// 소리 파일 없이 코드로 합성한 효과음
public class Sfx : MonoBehaviour
{
    const int Rate = 22050;
    const float Master = 0.55f;
    static Sfx inst;
    AudioSource src;
    readonly List<AudioSource> pool = new List<AudioSource>(); int poolI;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly System.Random rnd = new System.Random(7);

    public static void Init(GameObject host)
    {
        inst = host.AddComponent<Sfx>();
        inst.src = host.AddComponent<AudioSource>();
        for (int i = 0; i < 8; i++) inst.pool.Add(host.AddComponent<AudioSource>());
        inst.Build();
        AudioListener.volume = PlayerPrefs.GetInt("mute", 0) == 1 ? 0 : Master;
    }

    public static void Play(string id, float vol = 0.6f)
    {
        if (inst != null && inst.clips.TryGetValue(id, out var c)) inst.src.PlayOneShot(c, vol);
    }

    // 타격음: 칠 때마다 높낮이가 조금씩 달라 덜 질린다
    public static void Hit(string id, float vol, float spread = .08f)
    {
        if (inst == null || !inst.clips.TryGetValue(id, out var c)) return;
        var a = inst.pool[inst.poolI]; inst.poolI = (inst.poolI + 1) % inst.pool.Count;
        a.pitch = 1 + ((float)inst.rnd.NextDouble() * 2 - 1) * spread;
        a.PlayOneShot(c, vol);
    }

    AudioClip Tones(string name, float[] notes, float each, float decay, float noise = 0, int wave = 0)
    {
        int n = (int)(notes.Length * each * Rate) + Rate / 8;
        var d = new float[n];
        int per = (int)(each * Rate);
        for (int k = 0; k < notes.Length; k++)
            for (int i = 0; i < n - k * per; i++)
            {
                float t = i / (float)Rate, env = Mathf.Exp(-t * decay), s = 0;
                if (notes[k] > 0)
                {
                    s = Mathf.Sin(2 * Mathf.PI * notes[k] * t);
                    if (wave == 1) s = Mathf.Sign(s) * 0.6f;
                    if (wave == 2) s = s * 0.7f + Mathf.Sin(2 * Mathf.PI * notes[k] * 2.76f * t) * 0.4f;
                }
                d[k * per + i] += (s + noise * ((float)rnd.NextDouble() * 2 - 1)) * env;
            }
        var c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(d, 0);
        clips[name] = c;
        return c;
    }

    void Build()
    {
        Tones("퍽!", new[] { 120f }, .12f, 28, .6f);
        Tones("텅!", new[] { 880f, 1320f }, .02f, 11, 0, 2);
        Tones("촤르륵!", new[] { 2400f, 2800f, 2200f, 3000f, 2500f }, .03f, 26, .3f);
        Tones("콕!", new[] { 1700f }, .06f, 45, .1f);
        Tones("뽁!", new[] { 1250f, 1900f }, .035f, 34, .25f);
        Tones("쿡!", new[] { 780f, 520f }, .035f, 34, .55f);
        Tones("찰칵!", new[] { 2600f, 900f }, .04f, 50, .4f);
        Tones("찰싹!", new[] { 300f, 170f }, .03f, 34, 1f);
        Tones("슉!", new[] { 0f, 0f }, .05f, 42, .85f);
        Tones("또각!", new[] { 1000f, 700f }, .05f, 40, .2f, 2);
        Tones("후다닥!", new[] { 300f, 340f, 300f, 340f }, .05f, 20, .5f);
        Tones("팍!", new[] { 900f }, .05f, 45, .5f);
        Tones("바삭!", new[] { 2200f, 1800f, 2600f }, .03f, 40, .6f);
        Tones("착!", new[] { 1200f, 800f }, .04f, 35, .3f);
        Tones("쾅!", new[] { 90f, 70f }, .1f, 16, .7f);
        Tones("드르륵!", new[] { 180f, 150f, 180f, 150f, 180f }, .05f, 18, .5f);
        Tones("철컹!", new[] { 600f, 1400f }, .05f, 22, .2f, 2);
        Tones("휙!", new[] { 700f, 1100f }, .04f, 26, .4f);
        Tones("번쩍!", new[] { 3200f, 2400f }, .04f, 30, .2f);
        Tones("칙!", new[] { 5000f }, .1f, 22, .8f);
        Tones("annc", new[] { 784f, 659f, 523f, 659f, 784f }, .24f, 3.5f, 0, 2);
        Tones("swish", new[] { 0f }, .12f, 16, .55f);
        Tones("thudL", new[] { 170f, 120f }, .03f, 30, .55f);
        Tones("thudH", new[] { 95f, 62f, 48f }, .05f, 16, .75f);
        Tones("door", new[] { 659f, 523f }, .28f, 4, 0, 2);
        Tones("close", new[] { 523f, 523f, 523f }, .16f, 10, 0, 1);
        Tones("transfer", new[] { 523f, 587f, 659f, 784f, 880f, 784f, 880f, 1047f }, .13f, 5, 0, 2);
        Tones("fail", new[] { 330f, 262f, 196f }, .24f, 3, 0, 1);
        Tones("ouch", new[] { 200f, 150f }, .07f, 14, .2f, 1);
        Tones("skill", new[] { 700f, 1050f, 1400f }, .06f, 10, 0, 2);
        Tones("pick", new[] { 784f, 1175f }, .07f, 10, 0, 2);
        Tones("trot", new[] { 392f, 440f, 523f, 440f, 392f, 330f, 392f, 440f }, .18f, 5, 0, 1);

        var hum = new float[Rate * 2];
        float lp = 0;
        for (int i = 0; i < hum.Length; i++)
        {
            float t = i / (float)Rate;
            lp += ((float)rnd.NextDouble() * 2 - 1 - lp) * 0.04f;
            // 끝과 처음이 이어지도록 정수 주파수만 쓴다
            hum[i] = lp * 0.9f + Mathf.Sin(2 * Mathf.PI * 55 * t) * 0.05f + Mathf.Sin(2 * Mathf.PI * 2 * t) * lp * 0.5f;
        }
        var clip = AudioClip.Create("hum", hum.Length, 1, Rate, false);
        clip.SetData(hum, 0);
        var loop = gameObject.AddComponent<AudioSource>();
        loop.clip = clip; loop.loop = true; loop.volume = 0.05f; loop.Play();
        humSource = loop;
    }

    AudioSource humSource;
    public static bool Muted => PlayerPrefs.GetInt("mute", 0) == 1;
    public static void ToggleMute() { PlayerPrefs.SetInt("mute", Muted ? 0 : 1); AudioListener.volume = Muted ? 0 : Master; }
    public static void Hum(float v) { if (inst != null && inst.humSource != null) inst.humSource.volume = Mathf.MoveTowards(inst.humSource.volume, v, Time.unscaledDeltaTime * .3f); }
}
