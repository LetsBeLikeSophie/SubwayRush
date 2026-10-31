using System.Collections.Generic;
using UnityEngine;

// 무기: 좁고 넓음이 아니라 맞은 사람의 반응으로 나뉜다. 모션도 무기마다 다르다.
public partial class Game
{
    bool dashRam; float atkDur = .18f, atkWind, hitStop, ramStartX; WeaponDef windW;
    class Lure { public GameObject go; public Vector2 pos; public float life; }
    class TunnelDef { public float x0, x1, y, life; }
    readonly List<Lure> lures = new List<Lure>(); readonly List<TunnelDef> tunnels = new List<TunnelDef>();

    static float DurOf(string id)
    {
        switch (id)
        {
            case "keyring": case "selfie": case "tumbler": case "flash": return .2f;
            case "umbrella": case "backpack": case "battery": case "hook": return .23f;
            case "heel": return .26f;
            case "laptop": return .3f;
            case "beadbag": case "keybag": return .32f;
            case "lanyard": return .36f;
            case "card": return .16f;
            default: return .18f;
        }
    }

    static float StopOf(string id)
    {
        switch (id)
        {
            case "laptop": return .12f;
            case "heel": return .09f;
            case "beadbag": return .07f;
            case "backpack": return .05f;
            case "tumbler": return .04f;
            case "keyring": return .012f;
            default: return .02f;
        }
    }

    void Attack(WeaponDef w)
    {
        // 노트북은 들어 올리는 동안 꼼짝 못 한다
        if (w.id == "laptop" && atkWind <= 0 && windW == null) {
            windW = w; atkWind = .3f; atkCd = w.cooldown * mCooldown * (1 + WL(w) * UpOf(w.kind).cool);
            Say("이거 비싼 건데...", .8f);
            foreach (var p in crowd) if (!p.dying && (p.pos - pPos).magnitude < 3.6f) p.stun = Mathf.Max(p.stun, .42f);
            return;
        }
        atkDur = DurOf(w.id); atkAnim = atkDur;
        if (windW == null) atkCd = w.cooldown * mCooldown;
        int dir = facing;
        string kind = w.kind;
        if (kind == "back") { if (frontpack) kind = "front"; else dir = -facing; }
        atkDir = dir;
        var up = UpOf(w.kind); int ul = WL(w);
        float dmg = w.damage * mDamage * (1 + ul * up.dmg), range = w.range * (1 + ul * up.range), knock = w.knock * mKnock * (1 + ul * up.knock);
        if (windW == null) atkCd *= 1 + ul * up.cool;
        windW = null;
        if (kind == "throw" || kind == "bomb") Sfx.Hit(w.hit, .5f); else Sfx.Hit("swish", .28f, .15f);
        switch (kind)
        {
            case "sling": dmg += (beads + ul * 3) * 2.4f; break;
            case "extend": if (extT > 0) extend = Mathf.Min(extend + 1, 4); extT = 1.3f; range += extend * .6f; dmg *= 1 - extend * .14f; break;
            case "bomb":
            case "throw":
                int shotCount = kind == "throw" ? 3 + ul : 1;
                for (int n = 0; n < shotCount; n++)
                {
                    var g = Spr("shot", Art.Icon(w.id), pPos + new Vector2(facing * .5f, 1.1f), 9000); g.transform.localScale = Vector3.one * .45f;
                    shots.Add(new Shot { bomb = kind == "bomb", read = kind == "throw", vy = shotCount > 1 ? Random.Range(-2.6f, 2.6f) : 0, go = g, pos = pPos + new Vector2(facing * .5f, 0), dir = facing, life = range / 11f, dmg = dmg, knock = knock });
                }
                return;
            case "dash":
                dashT = .3f * (1 + ul * .15f); dashSpeed = 9; dashHit.Clear(); tripNext = Random.value < .12f + ul * .08f; Dust(); return;
            case "ram":
                dashT = .4f; dashRam = true; dashSpeed = 8.5f; dashHit.Clear(); tripNext = false; ramStartX = pPos.x; Dust(); return;
            case "stomp": if (++heelCount % 6 == 0) { mental -= 4; pFlash = .15f; Say("아 발 아파", .9f); } break;
            case "photo":
                {
                    var center = pPos + new Vector2(dir * range, 0);
                    AddRing(center + new Vector2(0, 1), w.depth * 1.5f, Color.white);
                    bool shot = false;
                    foreach (var p in crowd)
                    {
                        if (p.dying || (p.pos - center).magnitude > w.depth) continue;
                        Damage(p, dmg, knock * dir, shot ? null : w.hit); shot = true;
                        p.stun = Mathf.Max(p.stun, (p.isBoss ? .4f : 1.4f) + ul * .3f); if (!p.isBoss) p.posing = 1.4f;
                        if (Random.value < .4f) Float("브이!", p.pos + new Vector2(0, 2.2f), Color.white);
                    }
                    if (shot) Sfx.Play(w.hit, .6f);
                    return;
                }
        }
        AddSlash(w, kind, dir, range);
        bool any = false; int pulled = 0; Passenger firstHit = null;
        // 한 명만 치는 무기는 가장 가까운 사람만 맞는다
        bool singleKind = kind == "stab" || kind == "stomp" || kind == "single";
        Passenger only = null; float onlyD = 1e9f;
        if (singleKind)
            foreach (var p in crowd)
            {
                if (p.dying) continue;
                Vector2 d = p.pos - pPos;
                if (d.x * dir > -.25f && d.x * dir < range + p.Radius && Mathf.Abs(d.y) < w.depth && d.magnitude < onlyD) { only = p; onlyD = d.magnitude; }
            }
        foreach (var p in crowd)
        {
            if (p.dying || (singleKind && p != only)) continue;
            Vector2 d = p.pos - pPos;
            bool hit;
            if (kind == "cone") hit = d.x * dir > -.2f && d.x * dir < range && Mathf.Abs(d.y) < .45f + d.x * dir * .4f;
            else if (kind == "spin") hit = new Vector2(d.x, d.y * 1.3f).magnitude < range + p.Radius;
            else if (kind == "wide") hit = Mathf.Abs(d.x) < range + p.Radius && Mathf.Abs(d.y) < w.depth;
            else hit = d.x * dir > -.25f && d.x * dir < range + p.Radius && Mathf.Abs(d.y) < w.depth;
            if (!hit) continue;
            // 쇼핑백 걸기: 앞사람을 내 등 뒤로 넘긴다
            if (kind == "pull")
            {
                if (p.isBoss) { Damage(p, dmg, 0, any ? null : w.hit); any = true; continue; }
                if (pulled >= 2) continue;
                pulled++; p.pos.x = pPos.x - dir * Random.Range(.9f, 1.6f); p.vel.x = -dir * 2;
                Damage(p, dmg, 0, any ? null : w.hit); any = true;
                continue;
            }
            if (firstHit == null) firstHit = p;
            float kdir = (kind == "spin" || kind == "wide") ? (d.x >= 0 ? 1 : -1) : dir;
            Damage(p, dmg, knock * kdir, any ? null : w.hit);
            ApplyEffect(w, p, dir, ul);
            any = true;
        }
        if (w.id == "beadbag" && firstHit != null) Beads(firstHit, dir, Mathf.Min(10, 5 + (beads + ul * 3) / 3), dmg * .22f);
        if (any)
        {
            float heavy = StopOf(w.id);
            hitStop = Mathf.Max(hitStop, heavy);
            Sfx.Hit(w.hit, .65f);
            // 맞는 소리를 겹쳐 묵직하게
            Sfx.Hit(heavy >= .05f ? "thudH" : "thudL", heavy >= .05f ? .8f : .4f, .12f);
            // 칠 때 몸이 앞으로 살짝 쏠린다
            if (kind != "back" && kind != "spin" && kind != "wide") pVel.x += dir * (.5f + heavy * 10);
            // 백팩은 칠 때 반동으로 내가 밀려난다
            if (kind == "back") pVel.x -= dir * 1.7f * (1 + ul * .3f);
        }
    }

    void Beads(Passenger from, int dir, int n, float dmg)
    {
        for (int i = 0; i < n; i++)
        {
            var g = Spr("bead", Art.Bead(i), from.pos + new Vector2(0, 1.1f), 9000);
            var b = new Shot { bead = true, speed = Random.Range(3f, 7.5f), vy = Random.Range(-4f, 4f), go = g, pos = from.pos, dir = Random.value < .8f ? dir : -dir, life = Random.Range(.25f, .5f), dmg = dmg, knock = 1.6f };
            b.hit.Add(from);
            shots.Add(b);
        }
    }

    void ApplyEffect(WeaponDef w, Passenger p, int dir, int ul)
    {
        if (p.dying) return;
        switch (w.id)
        {
            case "keyring":
                p.flail = 2f + ul * .3f;
                if (Random.value < .3f) Speak(p, "따가워요!", .9f);
                break;
            case "heel": if (!p.isBoss) { p.bowl = .9f; p.bowlDir = dir; p.vel.y += Random.Range(-1f, 1f); Speak(p, "아아아악!", .8f); } break;
            case "tumbler": if (Random.value < .5f) MakeSpill(p.pos, 3f, .45f); break;
            case "lanyard": p.slow = 1.8f + ul * .5f; break;
            case "keybag": if (Random.value < .3f) MakeLure(); break;
        }
    }

    // 넘어지면 옆사람도 걸려 넘어진다
    void Trip(Passenger p, float t, int chain, int dir)
    {
        if (p.fall > 0 || p.dying) return;
        p.fall = t; p.fallDir = dir; p.stun = Mathf.Max(p.stun, t);
        if (chain <= 0) return;
        foreach (var o in crowd)
            if (o != p && !o.dying && (o.pos - p.pos).magnitude < 1f) { o.vel += (o.pos - p.pos).normalized * 2.5f; Trip(o, t * .6f, chain - 1, dir); }
    }

    void MakeSpill(Vector2 pos, float life, float scale)
    {
        if (spills.Count >= 6) return;
        var g = Spr("spill", Art.Puddle(), pos, -650); g.transform.localScale = new Vector3(scale, scale * .85f, 1);
        spills.Add(new Spill { go = g, pos = pos, life = life, r = scale });
    }

    // 떨어진 인형을 주우러 사람들이 몰려간다
    void MakeLure()
    {
        if (lures.Count >= 3) return;
        var pos = new Vector2(pPos.x - facing * Random.Range(2f, 3.2f), Random.Range(FloorBottom, FloorTop));
        var g = Spr("doll", Art.Icon("doll"), pos + new Vector2(0, .3f), Order(pos.y)); g.transform.localScale = Vector3.one * .4f;
        lures.Add(new Lure { go = g, pos = pos, life = 2.8f });
        Float("인형이다!", pos + new Vector2(0, 1.2f), new Color(1, .8f, .9f));
    }

    void TickWeaponFx(float dt)
    {
        for (int i = lures.Count - 1; i >= 0; i--) { lures[i].life -= dt; if (lures[i].life <= 0) { Destroy(lures[i].go); lures.RemoveAt(i); } }
        for (int i = tunnels.Count - 1; i >= 0; i--)
        {
            var t = tunnels[i]; t.life -= dt;
            if (t.life <= 0) { tunnels.RemoveAt(i); continue; }
            foreach (var p in crowd)
                if (!p.dying && !p.isBoss && p.pos.x > t.x0 && p.pos.x < t.x1 && Mathf.Abs(p.pos.y - t.y) < .5f)
                    p.pos.y += (p.pos.y >= t.y ? 1 : -1) * dt * 3.5f;
        }
    }

    void Dust() { for (int i = 0; i < 3; i++) AddSpark(pPos + new Vector2(-facing * (.2f + i * .25f), .1f)); }

    // ---------- 무기별 모션 ----------

    void DrawWeaponMotion(WeaponDef w, ref float arm, ref float lean, ref int shownFacing)
    {
        float u = atkAnim > 0 ? 1 - atkAnim / atkDur : 0;
        bool swinging = atkAnim > 0;
        int d = atkDir;
        bool back = w.kind == "back" && !frontpack;
        Vector2 at = new Vector2(back ? -facing * .42f : facing * .48f, back ? 1.05f : .72f);
        float rot = 0, size = back ? .75f : .55f, alpha = 1, kick = 999;
                if (atkWind > 0)
        {
            // 노트북: 머리 위로 들어 올리는 중
            arm = 175; lean = -14; at = new Vector2(0, 2.15f); size = .85f; rot = 0;
        }
        else if (dashT > 0)
        {
            lean = 28;
            if (w.kind == "ram") { at = new Vector2(facing * .95f, .55f); size = .95f; }
            else { at = new Vector2(-facing * .25f, .2f); size = .5f; }
        }
        else if (swinging)
        {
            size = .8f;
            float e = u * u * (3 - 2 * u), a, t = Mathf.Sin(Mathf.Pow(u, .6f) * Mathf.PI);
            switch (w.id)
            {
                case "tumbler": case "beadbag":
                    // 옆으로 크게 휘두르기
                    a = Mathf.Lerp(135, -45, e) * Mathf.Deg2Rad;
                    at = new Vector2(d * Mathf.Cos(a) * (w.id == "beadbag" ? 1.4f : 1f), 1f + Mathf.Sin(a) * .65f);
                    arm = Mathf.Lerp(150, 30, e); lean = Mathf.Lerp(-8, 14, e); rot = -d * Mathf.Lerp(-40, 140, e);
                    break;
                case "keyring":
                    // 콕콕 연타
                    t = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3));
                    at = new Vector2(d * (.45f + t * .75f), .98f); arm = 90; lean = 6; size = .6f; rot = d * 20;
                    break;
                case "umbrella":
                    // 펜싱 찌르기
                    at = new Vector2(d * (.5f + t * w.range * .55f), 1f); arm = 90; lean = 12 * t; rot = d * 45; size = 1f;
                    break;
                case "selfie":
                    // 쭉 뻗어 찍기
                    at = new Vector2(d * .75f, 1.4f); arm = 118; rot = d * 40;
                    break;
                case "heel":
                    // 다리를 들어 쾅
                    t = u < .5f ? u * 2 : (1 - u) * 2;
                    kick = u < .55f ? Mathf.Lerp(0, 85, u / .55f) : Mathf.Lerp(85, -5, (u - .55f) / .45f);
                    at = new Vector2(d * (.35f + t * .55f), .3f + t * .75f); lean = -12 * t + 6; arm = 40; size = .62f; rot = -d * 30;
                    break;
                case "laptop":
                    // 양손으로 내려치기
                    a = Mathf.Lerp(100, -30, e) * Mathf.Deg2Rad;
                    at = new Vector2(d * Mathf.Cos(a) * 1.15f + d * .2f, .95f + Mathf.Sin(a) * 1.15f);
                    arm = Mathf.Lerp(175, 30, e); lean = Mathf.Lerp(-14, 24, e); rot = -d * Mathf.Lerp(0, 150, e); size = .95f;
                    break;
                case "backpack":
                    // 가방은 등에 메고 몸만 휙 돌린다
                    at = new Vector2(-facing * .42f, 1.05f); size = .75f;
                    lean = -d * facing * 22 * Mathf.Sin(u * Mathf.PI); arm = 20; rot = d * 18 * Mathf.Sin(u * Mathf.PI);
                    break;
                case "keybag":
                    // 제자리에서 한 바퀴
                    a = u * Mathf.PI * 2;
                    at = new Vector2(Mathf.Cos(a) * 1.3f * d, .95f + Mathf.Sin(a) * .35f);
                    shownFacing = u > .25f && u < .75f ? -facing : facing; arm = 60; rot = -d * u * 360;
                    break;
                case "lanyard":
                    // 머리 위로 빙빙
                    a = u * Mathf.PI * 4;
                    at = new Vector2(Mathf.Cos(a) * 1.25f, 1.95f + Mathf.Sin(a) * .2f); arm = 175; size = .55f; rot = a * Mathf.Rad2Deg;
                    break;
                case "card": case "battery":
                    // 던지기
                    arm = Mathf.Lerp(180, 45, e); lean = Mathf.Lerp(-10, 18, e); at = new Vector2(d * Mathf.Lerp(-.4f, .7f, e), Mathf.Lerp(1.6f, 1f, e)); alpha = u < .45f ? 1 : 0; size = .5f;
                    break;
                case "hook":
                    // 걸어서 머리 위로 넘기기
                    a = Mathf.Lerp(0, 180, e) * Mathf.Deg2Rad;
                    at = new Vector2(d * Mathf.Cos(a) * 1.1f, 1f + Mathf.Sin(a) * 1.15f); arm = Mathf.Lerp(90, 170, e); lean = Mathf.Lerp(10, -14, e); rot = -d * e * 180;
                    break;
                case "flash":
                    // 폰을 치켜들어 번쩍
                    at = new Vector2(d * .62f, 1.4f); arm = 120; lean = -6; size = .6f;
                    break;
                default:
                    at = new Vector2(d * (.55f + t * w.range * .6f), 1.05f); arm = 100; rot = -d * u * 110;
                    break;
            }
        }
        wGo.transform.position = pPos + at;
        wGo.transform.rotation = Quaternion.Euler(0, 0, rot);
        wGo.transform.localScale = Vector3.one * size;
        if (phase != Phase.Exit && !ringing && onCallT <= 0) wSr.color = new Color(1, 1, 1, alpha);
        wSr.flipX = facing < 0;
        pendingKick = kick;
    }

    float pendingKick = 999;

    // ---------- 무기 연습 모드 ----------

    bool practice; float pracT, pracX0, pracDist; int pracLevel;
    readonly Dictionary<string, float> pracRec = new Dictionary<string, float>();

    public void StartPractice()
    {
        practice = true; pracLevel = 0; selected = 0;
        wLv.Clear(); skLv = 0; weapons.Clear(); items.Clear(); usedLines.Clear(); calls.Clear();
        weapons.Add(GameData.Weapons[0]); curWeapon = 0; skill = GameData.Skills[0];
        stageIndex = 0; kills = 0; beads = 3; maxMental = 100; mental = 100;
        frontpack = deaf = false; route = new RouteDef("일반", "", 1, 0, 0); clockSec = 8 * 3600 + 20 * 60;
        Recalc();
        BeginStage();
        PracticeReset();
    }

    public void PracticeReset() { pracT = 0; pracDist = 0; pracX0 = pPos.x; }

    public void PracticeWeapon(WeaponDef w) { weapons[0] = w; atkCd = 0; PracticeReset(); }

    public void ExitPractice()
    {
        practice = false; ClearStage(); weapons.Clear(); skill = null;
        state = St.Title; phase = Phase.Ride; BuildTitleScene();
    }

    void TickPractice(float dt)
    {
        mental = maxMental;
        pracT += dt;
        if (pracT <= 30) pracDist = Mathf.Max(pracDist, pPos.x - pracX0);
        else if (pracT - dt <= 30)
        {
            var id = weapons[0].id;
            if (!pracRec.TryGetValue(id, out var old) || pracDist > old) pracRec[id] = pracDist;
            Float(pracDist.ToString("0.0") + "m!", pPos + new Vector2(0, 2.6f), new Color(1, .9f, .3f));
        }
        // 끝에 닿으면 처음으로 되돌린다
        if (pPos.x > targetX - 4)
        {
            float shift = pPos.x - 4;
            pPos.x -= shift; pracX0 -= shift;
            foreach (var p in crowd) p.pos.x -= shift;
            var c = cam.transform.position; c.x -= shift; cam.transform.position = c;
        }
    }
}
