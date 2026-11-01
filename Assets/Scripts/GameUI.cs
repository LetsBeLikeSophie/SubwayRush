using UnityEngine;

// 화면 글은 최소로. 아이콘·키캡·막대로 알게 한다.
public partial class Game
{
    GUIStyle s; Texture2D white;
    float gw, gh, gs;
    static readonly Color Ink = new Color(.12f, .12f, .15f);
    static readonly Color Gold = new Color(.95f, .76f, .3f), Navy = new Color(.1f, .14f, .32f), Balloon = new Color(.36f, .22f, .12f);

    // 글자 테두리
    void TO(Rect r, string text, int size, Color c, Color o, float t = 2)
    {
        for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; T(new Rect(r.x + Mathf.Cos(a) * t, r.y + Mathf.Sin(a) * t, r.width, r.height), text, size, o, TextAnchor.MiddleCenter, FontStyle.Bold); }
        T(r, text, size, c, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    void OnGUI()
    {
        if (s == null)
        {
            s = new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
            white = Texture2D.whiteTexture;
        }
        gs = Screen.height / 720f; gw = Screen.width / gs; gh = 720;
        GUI.matrix = Matrix4x4.Scale(new Vector3(gs, gs, 1));
        switch (state)
        {
            case St.Title: Title(); break;
            case St.Choose: ChooseScreen(); break;
            case St.Map: MapScreen(); break;
            case St.Stage: Hud(); break;
            case St.Over: OverScreen(); break;
            case St.Clear: ClearScreen(); break;
        }
    }

    // 작지 않은 칸은 모두 둥글게
    void Box(Rect r, Color c)
    {
        float rad = r.width >= gw - 2 || Mathf.Min(r.width, r.height) < 14 ? 0 : Mathf.Min(10, Mathf.Min(r.width, r.height) / 3);
        GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, c, 0, rad);
    }

    void Card(Rect r, Color c)
    {
        Box(new Rect(r.x + 4, r.y + 7, r.width, r.height), new Color(0, 0, 0, .3f));
        Box(r, c);
    }

    void T(Rect r, string text, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle f = FontStyle.Normal)
    {
        s.fontSize = size; s.normal.textColor = c; s.alignment = a; s.fontStyle = f;
        GUI.Label(r, text, s);
    }

    void Icon(Rect r, string id)
    {
        var tex = Art.IconTex(id);
        GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
    }

    bool Button(Rect r, string text, Color bg, Color fg, int size = 24)
    {
        bool over = r.Contains(Event.current.mousePosition);
        Box(r, over ? Color.Lerp(bg, Color.white, .15f) : bg);
        T(r, text, size, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
        return Event.current.type == EventType.MouseDown && over;
    }

    void Title()
    {
        Box(new Rect(0, 0, gw, gh), new Color(.05f, .06f, .1f, .35f));
        TO(new Rect(gw - 190, 18, 170, 40), Weather.Label, 26, Color.white, Ink, 2);
        Card(new Rect(gw / 2 - 250, 90, 500, 390), new Color(.09f, .28f, .17f, .96f));
        T(new Rect(0, 110, gw, 140), "지옥철", 104, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        float cx = gw / 2;
        Card(new Rect(cx - 90, 255, 180, 90), new Color(.97f, .8f, .2f));
        T(new Rect(cx - 90, 255, 180, 90), "10-4", 68, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (Button(new Rect(cx - 110, 375, 220, 70), "출근", Color.white, Ink, 32)) OpenMap();
        int wins = PlayerPrefs.GetInt("wins", 0), lates = PlayerPrefs.GetInt("lates", 0);
        if (Button(new Rect(cx - 230, 490, 220, 44), "복지몰  " + Salary, new Color(.97f, .8f, .2f), Ink, 22)) OpenShop();
        if (Button(new Rect(cx + 10, 490, 220, 44), "무기 연습", Color.white, Ink, 22)) StartPractice();
        TO(new Rect(0, 540, gw, 36), $"{RankName}  ·  출근 {wins}  ·  지각 {lates}", 22, Color.white, Ink, 1.5f);
        KeyRow(600);
    }

    // 조작법은 키캡만 보여준다
    void KeyRow(float y)
    {
        string[][] rows = { new[] { "WASD", "이동" }, new[] { "J", "공격" }, new[] { "K", "방해" }, new[] { "L", "무기 교체" }, new[] { "E", "전화 받기" }, new[] { "R", "거절" }, new[] { "M", "소리" } };
        float x = gw / 2 - 560;
        foreach (var r in rows)
        {
            float kw = r[0].Length > 1 ? 70 : 36;
            Box(new Rect(x, y, kw, 36), Color.white);
            T(new Rect(x, y, kw, 36), r[0], 18, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            T(new Rect(x + kw + 6, y, 100, 36), r[1], 18, Color.white, TextAnchor.MiddleLeft);
            x += kw + 118;
        }
    }

    void ChooseScreen()
    {
        Box(new Rect(0, 0, gw, gh), new Color(.07f, .08f, .13f, .82f));
        T(new Rect(0, 60, gw, 70), chooseTitle, 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        float gap = 30, cw = Mathf.Min(330, (gw - 80 - gap * (choices.Count - 1)) / choices.Count), ch = 405, x = gw / 2 - (choices.Count * cw + (choices.Count - 1) * gap) / 2;
        Choice hover = null;
        for (int i = 0; i < choices.Count; i++)
        {
            var c = choices[i]; var r = new Rect(x + i * (cw + gap), 160, cw, ch);
            bool over = r.Contains(Event.current.mousePosition);
            if (over) hover = c;
            if (over) r.y -= 8;
            Card(r, over ? Color.white : c.tint * new Color(.96f, .96f, .96f));
            Box(new Rect(r.x + 14, r.y + 14, 36, 36), Ink);
            T(new Rect(r.x + 14, r.y + 14, 36, 36), (i + 1).ToString(), 22, Color.white);
            Box(new Rect(r.x + cw / 2 - 90, r.y + 40, 180, 180), new Color(1, 1, 1, .6f));
            Icon(new Rect(r.x + cw / 2 - 75, r.y + 55, 150, 150), c.icon == "speed" ? "dash" : c.icon);
            if (c.gradeText != null) { Box(new Rect(r.x + cw - 118, r.y + 14, 106, 30), new Color(.1f, .1f, .13f, .85f)); T(new Rect(r.x + cw - 118, r.y + 14, 106, 30), c.gradeText, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); }
            T(new Rect(r.x + 10, r.y + 225, cw - 20, 56), c.title, 30, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            T(new Rect(r.x + 24, r.y + 285, cw - 48, 70), c.sub, 21, new Color(.25f, .25f, .3f), TextAnchor.UpperCenter);
            if (c.replaces != null) { Box(new Rect(r.x + 30, r.y + ch - 46, cw - 60, 34), new Color(.8f, .15f, .15f)); T(new Rect(r.x + 30, r.y + ch - 46, cw - 60, 34), c.replaces, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); }
            if (Event.current.type == EventType.MouseDown && over) { Choose(i); Event.current.Use(); return; }
        }
        LoadoutBar(620);
        if (weapons.Count > 0) StatStrip(hover != null ? hover.item : null);
        // 한 번 다시 뽑기
        if (chooseTitle == "환승 통로 매점" && !rerollUsed && Salary >= 50 && Button(new Rect(gw - 220, 620, 190, 56), "다시 뽑기  50", new Color(.97f, .8f, .2f), Ink, 20))
        {
            PlayerPrefs.SetInt("salary", Salary - 50); rerollUsed = true; pendingRewards++; NextReward();
        }
    }

    // 지금 내 능력치. 아이템에 마우스를 올리면 바뀌는 값을 함께 보여준다
    void StatStrip(ItemDef it)
    {
        float y = 580, x = gw / 2 - 500;
        Stat(ref x, y, "위력", mDamage, it != null ? it.damage : 0, true);
        Stat(ref x, y, "이속", mSpeed, it != null ? it.speed : 0, true);
        Stat(ref x, y, "밀치기", mKnock, it != null ? it.knock : 0, true);
        Stat(ref x, y, "쿨타임", mCooldown, it != null ? it.cooldown : 0, false);
        Stat(ref x, y, "받는 피해", mTaken, it != null ? it.taken : 0, false);
        string m = "멘탈 " + Mathf.CeilToInt(mental) + "/" + Mathf.CeilToInt(maxMental);
        if (it != null && (it.heal != 0 || it.maxMental != 0)) m += "  →  " + Mathf.CeilToInt(Mathf.Min(maxMental + it.maxMental, mental + it.heal)) + "/" + Mathf.CeilToInt(maxMental + it.maxMental);
        T(new Rect(x, y, 240, 40), m, 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    void Stat(ref float x, float y, string label, float now, float delta, bool upGood)
    {
        string text = label + " " + Mathf.RoundToInt(now * 100) + "%";
        Color c = Color.white;
        if (Mathf.Abs(delta) > .001f)
        {
            text += " → " + Mathf.RoundToInt((now + delta) * 100) + "%";
            c = (delta > 0) == upGood ? new Color(.45f, 1, .5f) : new Color(1, .45f, .4f);
        }
        T(new Rect(x, y, 158, 40), text, 15, c, TextAnchor.MiddleLeft, FontStyle.Bold);
        x += Mathf.Abs(delta) > .001f ? 158 : 130;
    }

    // 지하철 노선도처럼 역을 잇는다. 깬 역에는 그때의 조합이 남는다
    void MapScreen()
    {
        Box(new Rect(0, 0, gw, gh), new Color(.07f, .08f, .13f, .88f));
        T(new Rect(30, 14, 400, 50), PName, 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        if (Button(new Rect(gw - 230, 16, 200, 46), "복지몰  " + Salary, new Color(.97f, .8f, .2f), Ink, 20)) { OpenShop(); return; }
        int cols = 5; float cw = (gw - 60 - 12 * (cols - 1)) / cols, ch = 190, sy = 78;
        var stages = GameData.Stages;
        for (int i = 0; i < stages.Count; i++)
        {
            int row = i / cols, col = i % cols;
            if (row % 2 == 1) col = cols - 1 - col;
            var r = new Rect(30 + col * (cw + 12), sy + row * (ch + 18), cw, ch);
            if (i < stages.Count - 1 && (i + 1) % cols != 0) Box(new Rect(r.x - 12, r.y + ch / 2 - 4, gw * 0 + cw + 24, 8), new Color(.2f, .7f, .35f));
            else if (i < stages.Count - 1) Box(new Rect(r.x + cw / 2 - 4, r.y + ch, 8, 18), new Color(.2f, .7f, .35f));
        }
        for (int i = 0; i < stages.Count; i++)
        {
            int row = i / cols, col = i % cols;
            if (row % 2 == 1) col = cols - 1 - col;
            var st = stages[i]; var r = new Rect(30 + col * (cw + 12), sy + row * (ch + 18), cw, ch);
            bool open = i <= Cleared, done = i < Cleared, over = open && r.Contains(Event.current.mousePosition);
            Color bg = !open ? new Color(.25f, .25f, .28f) : st.hell ? new Color(.5f, .05f, .08f) : done ? new Color(.86f, .84f, .74f) : new Color(1, .9f, .5f);
            if (over) { r.y -= 5; bg = Color.Lerp(bg, Color.white, .15f); }
            Card(r, bg);
            Color fg = st.hell || !open ? Color.white : Ink;
            T(new Rect(r.x + 10, r.y + 8, 40, 28), (i + 1).ToString(), 18, fg, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (st.hell) T(new Rect(r.x + 40, r.y + 8, cw - 50, 28), "헬게이트", 16, new Color(1, .75f, .3f), TextAnchor.MiddleRight, FontStyle.Bold);
            T(new Rect(r.x + 6, r.y + 44, cw - 12, 50), st.name, 22, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            var rec = PlayerPrefs.GetString("rec_" + i, "");
            if (rec.Length > 0)
            {
                var ids = rec.Split(',');
                float iw = Mathf.Min(44, (cw - 16) / ids.Length), ix = r.x + cw / 2 - iw * ids.Length / 2;
                for (int k = 0; k < ids.Length; k++) Icon(new Rect(ix + k * iw, r.y + 108, iw - 2, iw - 2), ids[k]);
            }
            if (Event.current.type == EventType.MouseDown && over) { Event.current.Use(); PlayStage(i); return; }
        }
    }

    // 무기 연습: 눌러서 바로 바꾸고, 30초 동안 얼마나 전진했는지 잰다
    void PracticePanel()
    {
        float x0 = 20, y0 = 66, sz = 54, gap = 6; int cols = 4;
        Box(new Rect(x0 - 8, y0 - 6, cols * (sz + gap) + 10, 268), new Color(.05f, .05f, .08f, .86f));
        var ws = GameData.Weapons;
        for (int i = 0; i < ws.Count; i++)
        {
            var r = new Rect(x0 + (i % cols) * (sz + gap), y0 + (i / cols) * (sz + gap), sz, sz);
            bool cur = ws[i] == weapons[0], over = r.Contains(Event.current.mousePosition);
            Box(r, cur ? new Color(1, .86f, .4f) : over ? Color.white : new Color(.7f, .72f, .78f));
            Icon(new Rect(r.x + 5, r.y + 5, sz - 10, sz - 10), ws[i].id);
            if (pracRec.TryGetValue(ws[i].id, out var rec)) TO(new Rect(r.x, r.y + sz - 20, sz, 20), rec.ToString("0"), 14, new Color(1, .95f, .4f), Ink, 1.2f);
            if (Event.current.type == EventType.MouseDown && over) { PracticeWeapon(ws[i]); Event.current.Use(); }
        }
        var w = weapons[0];
        float bx = 290, bw = gw - 290 - 190;
        Box(new Rect(bx, 16, bw, 76), new Color(.05f, .05f, .08f, .92f));
        T(new Rect(bx + 14, 18, bw - 28, 34), w.name + (WL(w) > 0 ? "  Lv." + (WL(w) + 1) : ""), 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        T(new Rect(bx + 14, 52, bw - 28, 34), w.desc, 16, new Color(.85f, .88f, .95f), TextAnchor.MiddleLeft);
        float t = Mathf.Max(0, 30 - pracT);
        TO(new Rect(bx, 98, bw, 50), pracT < 30 ? Mathf.CeilToInt(t) + "초  ·  " + pracDist.ToString("0.0") + "m" : "기록 " + pracDist.ToString("0.0") + "m", 28, new Color(1, .9f, .4f), Ink, 2);
        float by = 340, bh = 40;
        if (Button(new Rect(x0, by, 118, bh), "처음부터", Color.white, Ink, 17)) PracticeReset();
        if (Button(new Rect(x0 + 124, by, 116, bh), "나가기 Esc", Color.white, Ink, 16)) { ExitPractice(); return; }
        if (Button(new Rect(x0, by + 46, 118, bh), "강화 " + (WL(w) + 1), new Color(1, .86f, .5f), Ink, 17)) { wLv[w] = (WL(w) + 1) % (MaxUp + 1); PracticeReset(); }
        if (Button(new Rect(x0 + 124, by + 46, 116, bh), (selected + 1) + "단계", new Color(.7f, .86f, 1f), Ink, 17)) { selected = (selected + 3) % GameData.Stages.Count; PracticeReset(); }
    }

    void LoadoutBar(float y)
    {
        float x = 40;
        foreach (var w in weapons) { Box(new Rect(x, y, 64, 64), new Color(1, .93f, .8f)); Icon(new Rect(x + 6, y + 6, 52, 52), w.id); x += 72; }
        if (skill != null) { Box(new Rect(x, y, 64, 64), new Color(.82f, .93f, 1f)); Icon(new Rect(x + 6, y + 6, 52, 52), skill.id); x += 72; }
        foreach (var i in items) { Box(new Rect(x, y + 14, 36, 36), new Color(.86f, 1f, .84f)); Icon(new Rect(x + 3, y + 17, 30, 30), i.icon); x += 42; }
    }

    Vector2 W2G(Vector2 w)
    {
        var p = cam.WorldToScreenPoint(w);
        return new Vector2(p.x / gs, (Screen.height - p.y) / gs);
    }

    void Hud()
    {
        // 말풍선
        foreach (var b in bubbles) if (b.who != null && b.who.rig != null) DrawBubble(b.who.pos + new Vector2(0, b.who.isBoss ? 2.7f : 2.3f), b.text, Color.white, Ink);
        if (playerLineT > 0) DrawBubble(pPos + new Vector2(0, 2.55f), playerLine, new Color(1, .96f, .7f), Ink);
        foreach (var f in floaters)
        {
            var g = W2G(f.pos); var c = f.c; c.a = Mathf.Clamp01(f.ttl * 3);
            bool num = char.IsDigit(f.text[0]);
            float pop = 1 + Mathf.Clamp01((f.ttl - .6f) * 8) * .5f;
            TO(new Rect(g.x - 80, g.y - 20, 160, 40), f.text, (int)((f.big ? 30 : 19) * pop), c, new Color(.32f, .08f, .02f, c.a), 2.2f);
        }
        if (pFlash > 0) Box(new Rect(0, 0, gw, gh), new Color(.9f, .05f, .05f, pFlash * .55f));
        if (mental < maxMental * .3f && Mathf.Sin(Time.time * 7) > 0) Box(new Rect(0, 0, gw, gh), new Color(.8f, 0, 0, .1f));

        var me = W2G(pPos + new Vector2(0, -.08f));
        Box(new Rect(me.x - 34, me.y - 2, 68, 22), new Color(0, 0, 0, .72f));
        T(new Rect(me.x - 34, me.y - 2, 68, 22), PName, 15, new Color(1, .95f, .55f), TextAnchor.MiddleCenter, FontStyle.Bold);
        if (sweepWarn > .15f)
        {
            Box(new Rect(0, 0, gw, gh), new Color(.9f, .1f, .1f, sweepWarn * .28f));
            var warn = W2G(pPos + new Vector2(0, 3f));
            TO(new Rect(warn.x - 120, warn.y - 30, 240, 60), "버텨!", 44, new Color(1, .3f, .25f), Color.white, 3);
        }
if (arrived && phase != Phase.Exit)
        {
            Box(new Rect(gw / 2 - 250, 200, 500, 92), new Color(.06f, .06f, .08f, .94f));
            T(new Rect(gw / 2 - 250, 204, 500, 50), DoorLabel(doorCount) + " 도착!", 32, new Color(1, .85f, .3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            T(new Rect(gw / 2 - 250, 250, 500, 34), "곧 환승역", 20, Color.white);
        }
        var top = W2G(pPos + new Vector2(0, 2.28f));
        TO(new Rect(top.x - 20, top.y - 14, 40, 28), "▼", 20, new Color(1, .85f, .1f), Ink, 1.5f);
        if (Sfx.Muted) T(new Rect(gw - 170, 104, 150, 26), "소리 끔", 17, Color.white);

        Box(new Rect(20, 18, 300, 40), CurStage.hell ? new Color(.55f, .05f, .08f, .95f) : new Color(.08f, .08f, .1f, .9f));
        T(new Rect(20, 18, 300, 40), (Level + 1) + "단계  " + CurStage.name, 20, CurStage.hell ? new Color(1, .75f, .35f) : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        // 배지를 들었으면 쓸 수 있는 분홍 자리에 K 표시
        if (skill != null && skill.id == "badge" && skillCd <= 0 && NearSeat(out float seat))
        {
            var sg = W2G(new Vector2(seat, .2f));
            Box(new Rect(sg.x - 24, sg.y - 24, 48, 48), new Color(1, .45f, .7f, .9f));
            Cap(new Rect(sg.x - 14, sg.y - 14, 28, 28), "K");
        }
if (stageT < (platform ? 1.4f : 3.5f) && !practice)
        {
            string bt = lobby ? BossName + " 등장" : (stageIndex + 1) + " / 3 구간";
            string bs = lobby ? "" : "문 " + doorCount + "칸  ·  정차 " + (stopsLeft + stopNo) + "번  ·  승객 체력 " + Mathf.RoundToInt((1 + stageIndex * .12f) * HardHp * 100) + "%" + (route.density > 1 ? "  ·  지옥철" : route.density < 1 ? "  ·  급행" : "");
            Box(new Rect(gw / 2 - 330, 200, 660, bs.Length > 0 ? 98 : 70), new Color(.05f, .05f, .08f, .9f));
            TO(new Rect(0, 205, gw, 60), bt, 44, new Color(1, .9f, .4f), Ink, 3);
            if (bs.Length > 0) TO(new Rect(0, 258, gw, 34), bs, 20, Color.white, Ink, 2);
        }
        // 시계
        int sec = (int)clockSec;
        Box(new Rect(gw - 170, 18, 150, 44), Ink);
        T(new Rect(gw - 170, 18, 150, 44), $"{sec / 3600:00}:{sec / 60 % 60:00}:{sec % 60:00}", 26, lobby ? new Color(1, .4f, .35f) : new Color(.45f, 1f, .5f), TextAnchor.MiddleCenter, FontStyle.Bold);

        Box(new Rect(gw - 170, 62, 150, 30), new Color(.05f, .05f, .08f, .8f));
        T(new Rect(gw - 170, 62, 150, 30), Weather.Label, 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

        if (practice) PracticePanel(); else if (lobby) LobbyBar(); else RouteBar();

        StatusBar();
        CallWidget();
    }

    // 하단 상태창: 멘탈·방해 게이지, 빠른 슬롯, 맨 아래 출근 진행 막대
    void StatusBar()
    {
        float h = 66, y = gh - h;
        Box(new Rect(0, y, gw, h), new Color(.1f, .1f, .13f, .96f));
        Box(new Rect(12, y + 10, 58, 40), new Color(.25f, .25f, .3f));
        T(new Rect(12, y + 10, 58, 40), Ranks[Mathf.Min(Rank, Ranks.Length - 1)], 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        T(new Rect(78, y + 8, 70, 44), PName, 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        Gauge(new Rect(152, y + 7, 300, 22), "멘탈", Mathf.Clamp01(mental / maxMental), $"{Mathf.Max(0, Mathf.CeilToInt(mental))} / {Mathf.CeilToInt(maxMental)}", new Color(.95f, .2f, .25f), new Color(1, .55f, .5f));
        float sf = skill == null ? 0 : 1 - Mathf.Clamp01(skillCd / (SkillCdMax));
        Gauge(new Rect(152, y + 32, 300, 22), "방해", sf, sf >= 1 ? "준비" : Mathf.CeilToInt(Mathf.Max(0, skillCd)) + "초", new Color(.15f, .45f, 1f), new Color(.5f, .8f, 1f));
        // 출근 진행
        float prog = platform ? 0 : lobby ? (bossDown ? 1 : 1 - lobbyT / 75f) : Mathf.Clamp01(pPos.x / targetX);
        Box(new Rect(0, gh - 8, gw, 8), new Color(.05f, .05f, .12f));
        Box(new Rect(0, gh - 8, gw * prog, 8), new Color(1, .84f, .25f));

        float sz = 50, x = gw - 12 - sz, sy = y + 6;
        var active = ActiveCombo;
        if (active != null && active == comboSeen) TO(new Rect(gw - 232, sy - 34, 220, 24), active.name, 17, new Color(1, .85f, .3f), Ink, 1.3f);
        if (skill != null)
        {
            Slot(x, sy, sz, skill.id, "K", new Color(.7f, .86f, 1f));
            if (skill.id == "badge" && lobby) Box(new Rect(x, sy, sz, sz), new Color(0, 0, 0, .7f));
            if (skLv > 0) TO(new Rect(x - 4, sy + 26, 34, 24), "+" + skLv, 15, new Color(1, .95f, .4f), Ink, 1.3f);
            float cd = Mathf.Clamp01(skillCd / (SkillCdMax));
            if (cd > 0) { Box(new Rect(x, sy + sz * (1 - cd), sz, sz * cd), new Color(0, 0, 0, .55f)); TO(new Rect(x, sy, sz, sz), Mathf.CeilToInt(Mathf.Max(0, skillCd)).ToString(), 22, Color.white, Ink); }
            x -= sz + 6;
        }
        for (int i = weapons.Count - 1; i >= 0; i--)
        {
            bool cur = i == curWeapon;
            Slot(x, sy, sz, weapons[i].id, cur ? "J" : "L", cur ? new Color(1, .86f, .4f) : new Color(.6f, .62f, .7f));
            if (WL(weapons[i]) > 0) TO(new Rect(x - 4, sy + 26, 34, 24), "+" + WL(weapons[i]), 15, new Color(1, .95f, .4f), Ink, 1.3f);
            if (cur)
            {
                float cd = Mathf.Clamp01(atkCd / Mathf.Max(.01f, weapons[i].cooldown * mCooldown));
                if (cd > 0) Box(new Rect(x, sy + sz * (1 - cd), sz, sz * cd), new Color(0, 0, 0, .35f));
                if (weapons[i].kind == "back" && !frontpack) T(new Rect(x - 2, sy + 22, 26, 26), "◀", 18, new Color(.85f, .1f, .1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            x -= sz + 6;
        }
        if (HasBeadBag()) { x -= 34; Box(new Rect(x, sy + 8, 74, 34), new Color(1, .6f, .8f)); T(new Rect(x, sy + 8, 74, 34), "비즈 " + beads, 16, Ink, TextAnchor.MiddleCenter, FontStyle.Bold); x -= 8; }
        for (int i = items.Count - 1; i >= 0; i--) { if (x < 470) break; x -= 34; Box(new Rect(x, sy + 10, 30, 30), new Color(.86f, 1f, .84f, .95f)); Icon(new Rect(x + 3, sy + 13, 24, 24), items[i].icon); }
    }

    void Gauge(Rect r, string label, float v, string text, Color c, Color top)
    {
        T(new Rect(r.x, r.y, 46, r.height), label, 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        var bar = new Rect(r.x + 48, r.y, r.width - 48, r.height);
        GUI.DrawTexture(bar, white, ScaleMode.StretchToFill, true, 0, new Color(.05f, .05f, .12f), 0, 5);
        if (v > 0)
        {
            GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * v, bar.height - 4), white, ScaleMode.StretchToFill, true, 0, c, 0, 4);
            GUI.DrawTexture(new Rect(bar.x + 3, bar.y + 3, (bar.width - 6) * v, 5), white, ScaleMode.StretchToFill, true, 0, new Color(top.r, top.g, top.b, .75f), 0, 2);
        }
        TO(bar, text, 14, Color.white, new Color(0, 0, 0, .8f), 1.2f);
    }

    void Slot(float x, float y, float sz, string icon, string key, Color bg)
    {
        Box(new Rect(x, y, sz, sz), bg);
        Icon(new Rect(x + 5, y + 5, sz - 10, sz - 10), icon);
        Cap(new Rect(x + sz - 20, y - 6, 24, 24), key);
    }

    void Cap(Rect r, string k)
    {
        Box(r, Color.white); Box(new Rect(r.x + 1, r.y + r.height - 4, r.width - 2, 3), new Color(.7f, .7f, .7f));
        T(r, k, 18, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    void RouteBar()
    {
        float bw = 560, bx = gw / 2 - bw / 2, by = 16;
        Box(new Rect(bx, by, bw, 72), new Color(0.07f, .07f, .09f, .93f));
        // 노선 이름 + 전광판
        Box(new Rect(bx + 8, by + 8, 104, 28), line.color);
        T(new Rect(bx + 8, by + 8, 104, 28), line.name, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        string led;
        bool calling = !arrived && (announced && phase == Phase.Ride || phase == Phase.Brake || phase == Phase.Open && openT < 1.6f);
        bool transfer = phase == Phase.Ride ? stopsLeft <= 1 : finalStop;
        string tail = transfer ? " · 환승 " + DoorLabel(doorCount) : "";
        if (deaf) led = "♪ ♪ ♪";
        else if (platform) led = phase == Phase.Open || phase == Phase.Exit ? "문이 열렸습니다" : line.stations[line.stations.Length - 1] + "행 열차 들어옵니다";
        else if (phase == Phase.Open && openT < 1.6f) led = "문이 열립니다";
        else if (phase == Phase.Brake || phase == Phase.Open || phase == Phase.Exit) led = "이번 역은 " + stationName + tail;
        else if (announced) led = "이번 역은 " + nextName + tail;
        else led = "다음 역 " + line.stations[(stationI + stopNo + 1) % line.stations.Length] + tail;
        // 도착 직전엔 전광판이 깜빡인다
        if (calling && !deaf) Box(new Rect(bx + 114, by + 5, bw - 190, 32), new Color(1, .75f, .1f, .28f + Mathf.Sin(Time.time * 8) * .18f));
        T(new Rect(bx + 118, by + 6, bw - 200, 32), led, 20, calling ? new Color(1, .85f, .3f) : new Color(1, .45f, .25f), TextAnchor.MiddleLeft, FontStyle.Bold);
        // 남은 정차
        for (int i = 0; i < stopsLeft; i++) Box(new Rect(bx + bw - 20 - i * 14, by + 16, 10, 10), new Color(1, .75f, .1f));
        // 문 진행 막대: 지금 위치 → 빠른 환승 문
        float tx = bx + 22, tw = bw - 86, ty = by + 52;
        Box(new Rect(tx, ty, tw, 4), line.color);
        for (int i = 0; i <= doorCount; i++) Box(new Rect(tx + tw * i / doorCount - 2, ty - 3, 4, 10), Color.gray);
        float u = Mathf.Clamp01(pPos.x / targetX);
        Box(new Rect(tx + tw - 4, ty - 8, 8, 20), new Color(1, .75f, .1f));
        T(new Rect(bx + bw - 62, by + 38, 58, 30), DoorLabel(doorCount), 22, new Color(1, .75f, .1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Box(new Rect(tx + tw * u - 7, ty - 5, 14, 14), Color.white);

        if (passT > 0)
        {
            float bw3 = 460, bx3 = gw / 2 - bw3 / 2;
            Box(new Rect(bx3, 96, bw3, 54), new Color(.06f, .06f, .08f, .95f));
            T(new Rect(bx3, 96, bw3, 54), passName + "역 통과", 26, new Color(.5f, 1, .6f), TextAnchor.MiddleCenter, FontStyle.Bold);
        }
        // 환승역에서 문이 열려 있으면 남은 시간을 크게
        if (phase == Phase.Open && finalStop)
        {
            T(new Rect(0, 100, gw, 80), Mathf.CeilToInt(Mathf.Max(0, phaseT)).ToString(), 66, new Color(1, .35f, .25f), TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }

    void LobbyBar()
    {
        float bw = 520, bx = gw / 2 - bw / 2;
        Box(new Rect(bx, 16, bw, 46), new Color(.07f, .07f, .09f, .93f));
        if (boss != null && !bossDown)
        {
            T(new Rect(bx + 8, 16, 90, 46), BossName, 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Box(new Rect(bx + 102, 30, bw - 116, 18), Color.gray);
            Box(new Rect(bx + 102, 30, (bw - 116) * Mathf.Clamp01(boss.hp / boss.maxHp), 18), new Color(.85f, .2f, .2f));
        }
        else T(new Rect(bx, 16, bw, 46), "▶ 사원증 찍기", 24, new Color(.4f, 1f, .5f), TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    void DrawBubble(Vector2 world, string text, Color bg, Color fg)
    {
        var g = W2G(world);
        s.fontSize = 19; s.fontStyle = FontStyle.Bold; s.wordWrap = false;
        var size = s.CalcSize(new GUIContent(text));
        s.wordWrap = true;
        float w = size.x + 20, h = 34, x = Mathf.Clamp(g.x - w / 2, 4, gw - w - 4), y = Mathf.Max(g.y - h, 4);
        Box(new Rect(x - 2, y - 2, w + 4, h + 4), Balloon);
        Box(new Rect(x, y, w, h), bg);
        GUI.DrawTexture(new Rect(g.x - 5, y + h - 1, 10, 8), white, ScaleMode.StretchToFill, true, 0, Balloon, 0, 3);
        T(new Rect(x, y, w, h), text, 19, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    void CallWidget()
    {
        if (!ringing && onCallT <= 0)
        {
            float my = 68;
            foreach (var c in calls)
            {
                if (c.missed <= 0) continue;
                Box(new Rect(gw - 190, my, 170, 28), new Color(.75f, .1f, .1f, .95f));
                T(new Rect(gw - 186, my, 128, 28), c.who[0], 15, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                T(new Rect(gw - 60, my, 36, 28), c.missed.ToString(), 18, new Color(1, .9f, .4f), TextAnchor.MiddleRight, FontStyle.Bold);
                my += 31;
            }
            return;
        }
        float w = 340, h = ringing ? 196 : 92, x = gw / 2 - w / 2 + (ringing ? Mathf.Sin(Time.time * 40) * 3 : 0), y = 112;
        Box(new Rect(x - 4, y - 4, w + 8, h + 8), Ink);
        Box(new Rect(x, y, w, h), new Color(.16f, .18f, .24f));
        T(new Rect(x, y + 8, w, 40), caller[0] + (curCall != null && curCall.missed > 0 ? "  (" + curCall.missed + ")" : ""), 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        T(new Rect(x, y + 48, w, 34), caller[1], 20, new Color(.8f, .85f, .95f));
        if (ringing)
        {
            Box(new Rect(x + 12, y + 90, (w - 24) * Mathf.Clamp01(ringT / 2.8f), 6), new Color(1, .75f, .1f));
            if (Button(new Rect(x + 14, y + 108, 150, 72), "E  받기", new Color(.2f, .7f, .35f), Color.white, 26)) AnswerCall();
            if (Button(new Rect(x + w - 164, y + 108, 150, 72), "R  거절", new Color(.82f, .2f, .2f), Color.white, 26)) RejectCall();
        }
    }

    void OverScreen()
    {
        Box(new Rect(0, 0, gw, gh), new Color(.61f, .73f, .83f, .95f));
        string title = overReason == "pushed" ? "밀려 내림" : overReason == "mental" ? "멘탈 붕괴" : overReason == "missed" ? "환승 놓침" : "지각";
        string[] msgs = overReason == "pushed" ? new[] { PName + " 어디야?", "거기가 왜 " + stationName + "이야", "다음 열차는 없어" } : overReason == "mental" ? new[] { PName + " 오늘 왜 그래?", "목소리가 안 좋네", "오늘은 쉬어" } : new[] { PName + " 어디야?", "회의 시작했어", "내일 얘기 좀 하지" };
        T(new Rect(0, 60, gw, 90), title, 64, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        float x = gw / 2 - 250, y = 180;
        Box(new Rect(x, y, 56, 56), new Color(.45f, .55f, .9f));
        T(new Rect(x, y, 56, 56), "상사", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        foreach (var m in msgs)
        {
            s.fontSize = 24; s.wordWrap = false; float mw = s.CalcSize(new GUIContent(m)).x + 34; s.wordWrap = true;
            Box(new Rect(x + 68, y, mw, 52), Color.white);
            T(new Rect(x + 68, y, mw, 52), m, 24, Ink);
            y += 62;
        }
        T(new Rect(0, 400, gw, 40), $"{Mathf.Min(stageIndex + 1, 4)} / 4 구간  ·  {kills}명  ·  월급 +{earned}", 26, Ink);
        LoadoutBarCenter(452);
        if (Button(new Rect(gw / 2 - 120, 560, 240, 70), "내일 또 출근", Color.white, Ink, 28)) AfterEnd();
    }

    void LoadoutBarCenter(float y)
    {
        float x = gw / 2 - (weapons.Count * 72 + (skill != null ? 72 : 0)) / 2;
        foreach (var w in weapons) { Box(new Rect(x, y, 64, 64), new Color(1, .93f, .8f)); Icon(new Rect(x + 6, y + 6, 52, 52), w.id); x += 72; }
        if (skill != null) { Box(new Rect(x, y, 64, 64), new Color(.82f, .93f, 1f)); Icon(new Rect(x + 6, y + 6, 52, 52), skill.id); }
    }

    void ClearScreen()
    {
        Box(new Rect(0, 0, gw, gh), new Color(.97f, .86f, .4f, .95f));
        int sec = (int)clockSec;
        T(new Rect(0, 110, gw, 130), $"{sec / 3600:00}:{sec / 60 % 60:00}:{sec % 60:00}", 104, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        T(new Rect(0, 250, gw, 80), "출근 성공", 56, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        T(new Rect(0, 340, gw, 40), $"{kills}명 · 부재중 {missed} · 월급 +{earned}", 26, Ink);
        if (promoted != null) TO(new Rect(0, 60, gw, 50), "승진!  " + promoted, 34, new Color(1, .35f, .25f), Color.white, 2);
        if (unlockedNow.Count > 0)
        {
            float ux = gw / 2 + 110;
            T(new Rect(ux, 395, 90, 28), "새 무기", 18, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            for (int i = 0; i < unlockedNow.Count; i++) { Box(new Rect(ux + i * 60, 425, 54, 54), new Color(1, .96f, .85f)); Icon(new Rect(ux + 4 + i * 60, 429, 46, 46), unlockedNow[i].id); }
        }
        LoadoutBarCenter(400);
        if (Button(new Rect(gw / 2 - 130, 520, 260, 72), "다음 역으로", Ink, Color.white, 28)) AfterEnd();
    }
}
