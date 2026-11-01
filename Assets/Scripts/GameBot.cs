using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 시험용 자동 플레이어. 실행 인자로만 켜진다: -bot / -balance N / -shots 폴더
public partial class Game
{
    bool botOn, godMode, greedy, botPractice, botCombo, botNoCombo; int botPI; float botPT; string botWeapon; Keys botKeys; int balanceRuns, wonRuns; string shotDir; float chooseDelay, botT;
    readonly HashSet<string> shotDone = new HashSet<string>();
    readonly List<string> results = new List<string>();
    float runStart; bool endWait; float endT;
    float runClock, paceNext = 15, botTemp = 20; int botCode = 2;

    // 0~3 = 구간 3개, 3~4 = 보스
    float BotProgress()
    {
        if (lobby) return 3 + (boss == null || bossDown ? 1 : 1 - Mathf.Clamp01(boss.hp / boss.maxHp));
        return stageIndex + (platform ? 0 : Mathf.Clamp01(pPos.x / targetX));
    }

    void InitBot()
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == "-bot") botOn = true;
            if (a[i] == "-god") godMode = true;
            if (a[i] == "-greedy") greedy = true;
            if (a[i] == "-combo") botCombo = true;
            if (a[i] == "-nocombo") botNoCombo = true;
            if (a[i] == "-practice") botPractice = true;
            if (a[i] == "-weapon" && i + 1 < a.Length) botWeapon = a[i + 1];
            if (a[i] == "-rank" && i + 1 < a.Length) botRank = int.Parse(a[i + 1]);
            if (a[i] == "-balance" && i + 1 < a.Length) { botOn = true; balanceRuns = int.Parse(a[i + 1]); Time.captureDeltaTime = 1f / 30; Application.targetFrameRate = -1; }
            if (a[i] == "-shots" && i + 1 < a.Length) { botOn = true; shotDir = a[i + 1]; Directory.CreateDirectory(shotDir); }
            if (a[i] == "-temp" && i + 1 < a.Length) botTemp = float.Parse(a[i + 1]);
            if (a[i] == "-code" && i + 1 < a.Length) botCode = int.Parse(a[i + 1]);
            if (a[i] == "-seed" && i + 1 < a.Length) UnityEngine.Random.InitState(int.Parse(a[i + 1]));
        }
        if (!botOn) return;
        Weather.Set(botTemp, botCode);
        if (Array.IndexOf(a, "-liveweather") >= 0) StartCoroutine(Weather.Fetch());
        AudioListener.volume = 0;
        botT = shotDir != null ? 2.5f : .2f;
    }

    void TakeShot(string name)
    {
        if (shotDir == null || !shotDone.Add(name)) return;
        ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + ".png"));
    }

    void BotOnChoices() { chooseDelay = shotDir != null ? 1.0f : .02f; }
    void BotOnStage() { Debug.Log($"[BOT] stage {stageIndex} clock={runClock:0} line={line.name} doors={doorCount} stops={stopsLeft} route={route.name} mental={mental:0}/{maxMental:0}"); }

    void BotOnEnd(bool won)
    {
        if (!botOn) return;
        float secs = Time.time - runStart;
        var names = string.Join("+", weapons.ConvertAll(w => w.id));
        Debug.Log($"[BOT] END {(won ? "WIN" : overReason)} stage={stageIndex} kills={kills} t={secs:0} weapons={names} skill={skill?.id} mental={mental:0}");
        results.Add((won ? "WIN" : overReason) + "@" + stageIndex);
        Debug.Log($"[BOT] DONE w={botWeapon} result={(won ? "WIN" : overReason)} clock={runClock:0} prog={(won ? 4 : BotProgress()):0.00} mental={mental:0} weather={Weather.Label} combo={ActiveCombo?.name}");
        runClock = 0; paceNext = 15;
        if (won) wonRuns++;
        endWait = true; endT = shotDir != null ? 1.5f : .05f;
    }

    void BotUpdate()
    {
        if (!botOn) return;
        botKeys = new Keys();
        float rdt = Time.unscaledDeltaTime;
        switch (state)
        {
            case St.Title:
                botT -= Mathf.Min(rdt, .05f);
                if (shotDir != null && botT < 1f) TakeShot("1_title");
                if (botT <= 0 && botPractice) { StartPractice(); botPI = 0; botPT = 2.2f; weapons[0] = GameData.Weapons[0]; break; }
                if (botT <= 0)
                {
                    if (shotDir != null && !shotDone.Contains("1b_map")) { state = St.Map; botT = 1.2f; break; }
                    runStart = Time.time; StartRun();
                }
                break;
            case St.Map:
                botT -= rdt;
                if (botT < .6f) TakeShot("1b_map");
                if (botT <= 0) { runStart = Time.time; StartRun(); }
                break;
            case St.Choose:
                chooseDelay -= rdt;
                if (shotDir != null && chooseDelay < .4f) TakeShot("2_choose_" + chooseTitle.Replace(" ", ""));
                if (chooseDelay <= 0)
                {
                    int pick = UnityEngine.Random.Range(0, choices.Count);
                    // 시험용: 무기 강화만 고르는 사람
                    if (greedy) for (int i = 0; i < choices.Count; i++) if (choices[i].title.Contains(" Lv.") && choices[i].icon == weapons[Mathf.Clamp(curWeapon, 0, weapons.Count - 1)].id) pick = i;
                    // 시험용: 한 무기만 끝까지 든다
                    if (botWeapon != null)
                        for (int t = 0; t < 8 && choices[pick].replaces != null && GameData.Weapons.Exists(x => x.id == choices[pick].icon); t++) pick = UnityEngine.Random.Range(0, choices.Count);
                    if (botNoCombo) for (int t = 0; t < 12 && choices[pick].combo; t++) pick = UnityEngine.Random.Range(0, choices.Count);
                    // 시험용: 조합이 보이면 잡고, 잡은 조합은 안 버린다
                    if (botCombo)
                    {
                        if (ActiveCombo != null) for (int t = 0; t < 8 && choices[pick].replaces != null; t++) pick = UnityEngine.Random.Range(0, choices.Count);
                        else for (int i = 0; i < choices.Count; i++) if (choices[i].combo) pick = i;
                    }
                    Choose(pick);
                }
                break;
            case St.Stage: BotPlay(); break;
            default:
                if (shotDir != null && endT < .8f) TakeShot(state == St.Clear ? "9_clear" : "9_over");
                endT -= rdt;
                if (endWait && endT <= 0)
                {
                    endWait = false;
                    balanceRuns--;
                    if (shotDir == null && balanceRuns > 0) { runStart = Time.time; StartRun(); }
                    else
                    {
                        var c = new Dictionary<string, int>();
                        foreach (var r in results) { c.TryGetValue(r, out var n); c[r] = n + 1; }
                        var parts = new List<string>();
                        foreach (var kv in c) parts.Add(kv.Key + "=" + kv.Value);
                        parts.Sort();
                        Debug.Log("[BOT] SUMMARY runs=" + results.Count + " wins=" + wonRuns + " " + string.Join(" ", parts));
                        Application.Quit();
                    }
                }
                break;
        }
    }

    void BotPlay()
    {
        runClock += Time.deltaTime;
        if (runClock >= paceNext) { Debug.Log($"[BOT] PACE w={botWeapon} t={paceNext:0} prog={BotProgress():0.00} mental={mental:0}"); paceNext += 15; }
        if (botPractice)
        {
            // 무기를 차례로 쓰면서 공격 중간 모습을 찍는다
            botPT -= Time.deltaTime;
            var pw = weapons[0];
            if (atkAnim > 0 && atkAnim < atkDur * .6f) TakeShot("m_" + pw.id);
            botKeys.mx = 1; botKeys.atk = true; facing = pw.kind == "back" ? -1 : 1;
            if (botPT <= 0)
            {
                botPI++; botPT = 2.2f;
                if (botPI >= GameData.Weapons.Count) { Application.Quit(); return; }
                PracticeWeapon(GameData.Weapons[botPI]);
            }
            return;
        }
        if (shotDir != null)
        {
            if (stageT > 12) TakeShot("3_stage" + stageIndex);
            if (comboT > 0 && comboT < 3.2f) TakeShot("c_combo");
            if (!lobby && phase == Phase.Open && !finalStop && phaseT < 3.5f) TakeShot("4_doors");
            if (!lobby && finalStop && phase == Phase.Open) TakeShot("5_final");
            if (ringing) TakeShot("6_call");
            if (arrived && !finalStop && phase != Phase.Exit) TakeShot("8_arrived");
            if (announced && phase == Phase.Ride && !arrived) TakeShot("8_announce");
            if (lobby && stageT > 3) TakeShot("7_lobby");
        }
        var w = weapons[Mathf.Clamp(curWeapon, 0, weapons.Count - 1)];
        bool back = w.kind == "back" && !frontpack;
        if (platform)
        {
            if (phase == Phase.Brake && phaseT < 1.6f) TakeShot("p_train");
            if (phase == Phase.Ride && phaseT < .8f) TakeShot("p_wait");
            if (phase == Phase.Open && openT > .9f) TakeShot("p_open");
            if (phase == Phase.Exit) TakeShot("p_board");
            float dx = Mathf.Clamp(Mathf.Round(pPos.x / DoorGap), 0, platMax) * DoorGap;
            if (phase != Phase.Open && pPos.x < 0) dx = 0;
            botKeys.mx = pPos.x < dx - .25f ? 1 : (pPos.x > dx + .25f ? -1 : 0);
            botKeys.my = 1; botKeys.atk = true;
            if (back) { botKeys.atk = false; if (botKeys.mx == 0 || runClock % .5f < .1f) botKeys.mx = -1; else botKeys.mx = 1; }
            else facing = botKeys.mx < 0 ? -1 : 1;
            return;
        }
        float goal = lobby && boss != null && !bossDown ? boss.pos.x - (w.kind == "photo" ? 3.2f : 0) : targetX;
        botKeys.mx = pPos.x < goal - .4f ? 1 : (pPos.x > goal + .4f ? -1 : 0);
        botKeys.atk = true;
        if (back) { botKeys.atk = false; if (botKeys.mx != 0) botKeys.mx = runClock % .5f < .1f ? -botKeys.mx : botKeys.mx; else botKeys.mx = runClock % .5f < .25f ? -1 : 1; }
        else facing = botKeys.mx < 0 ? -1 : 1;
        botKeys.my = Mathf.Sin(Time.time * .9f) * .6f;
        if (InExitWave())
        {
            float doorX = Mathf.Round(pPos.x / DoorGap) * DoorGap;
            if (Mathf.Abs(pPos.x - doorX) < 3.5f) { botKeys.my = -1; botKeys.mx = pPos.x < doorX ? -.6f : .6f; }
        }
        int near = 0;
        foreach (var p in crowd) if (!p.dying && (p.pos - pPos).magnitude < 2.6f) near++;
        if (near >= 3 && skillCd <= 0) botKeys.skillDown = true;
        if (weapons.Count > 1 && Time.frameCount % 700 == 0) botKeys.swapDown = true;
        if (ringing && ringT < 2.2f) { if (UnityEngine.Random.value < .5f) botKeys.accept = true; else botKeys.reject = true; }
    }
}
