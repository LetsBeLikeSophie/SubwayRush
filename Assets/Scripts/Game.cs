using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

public class Passenger
{
    public PassengerDef def; public Rig rig;
    public Vector2 pos, vel; public float size = .9f, press, hp, maxHp, stun, angry, attackT, flash, anim, phase, dieT, hurtT, moving, hitLean, flail, flailT, slow, fall, posing, bowl; public int fallDir = 1, bowlDir = 1;
    public bool dying, isBoss, chaser, exiting; public float Radius => def.radius;
}

public class RouteDef
{
    public string name, tag; public float density; public int stopDelta, bonus; public bool Express => bonus > 0;
    public RouteDef(string name, string tag, float density, int stopDelta, int bonus) { this.name = name; this.tag = tag; this.density = density; this.stopDelta = stopDelta; this.bonus = bonus; }
}

public class Choice { public string icon, title, sub, replaces, gradeText; public ItemDef item; public Action act; public Color tint = Color.white; }

public enum St { Title, Map, Choose, Stage, Over, Clear }
enum Phase { Ride, Brake, Open, Leave, Exit }

public partial class Game : MonoBehaviour
{
    const int MaxCrowd = 150;
    const float ExitWave = 3f;
    const float FloorTop = -1.05f, FloorBottom = -2.2f, DoorGap = 4f, PlayerR = 0.36f,  BaseSpeed = 3.1f, BaseCall = 24f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { var g = new GameObject("Game"); DontDestroyOnLoad(g); g.AddComponent<Game>(); }

    public St state = St.Title;
    Camera cam; Font font;
    GameObject root; Transform rootT;
    readonly List<Passenger> crowd = new List<Passenger>();

    // 런 상태
    public readonly List<WeaponDef> weapons = new List<WeaponDef>();
    public SkillDef skill; readonly List<ItemDef> items = new List<ItemDef>();
    int curWeapon, stageIndex, kills, beads, badgeUses, pendingRewards;
    float mental, maxMental;
    float mSpeed, mDamage, mCooldown, mKnock, mTaken;
    bool frontpack, deaf;
    RouteDef route;
    double clockSec;
    string overReason = "late";

    // 플레이어
    Rig pRig; GameObject wGo; SpriteRenderer wSr; float pMoving;
    Vector2 pPos, pVel; int facing = 1; float dashSpeed = 8; float pAnim, atkCd, atkAnim, skillCd, hurtCd, pFlash, selfStun, dizzy, latched, noAttack, dashT, extT;
    int extend, heelCount; bool tripNext; int atkDir = 1;
    readonly HashSet<Passenger> dashHit = new HashSet<Passenger>();

    // 스테이지
    LineDef line; int startDoor, doorCount, stopsLeft, stopNo; bool lobby, bossDown; float targetX, gateX;
    Phase phase; float phaseT, trainSpeed = 1, doorOpen, bgBlend, stageT, lobbyT; int stationI; string stationName = "";
    bool finalStop; Passenger boss;
    readonly List<Transform[]> doors = new List<Transform[]>();
    class Layer { public Transform[] t; public float tw, speed, off; }
    class Fore { public Transform t; public float x; }
    readonly List<Layer> layers = new List<Layer>(); readonly List<Fore> fore = new List<Fore>();
    Transform stationRoot, vignette, portal;
    readonly List<GameObject> stationTexts = new List<GameObject>();
    readonly List<Transform> straps = new List<Transform>(), lights = new List<Transform>();
    SpriteRenderer stationBg; Transform arrow;
    readonly List<int> usedLines = new List<int>();

    // 전화: 울리면 못 움직인다. 받으면 잠깐 묶이고, 거절하면 다음 전화가 빨리 온다
    float callTimer, callInterval, ringT, onCallT, ringBeep; bool ringing; string[] caller;
    class CallState { public string[] who; public int missed; public float interval, timer; }
    readonly List<CallState> calls = new List<CallState>(); CallState curCall;
    int missed { get { int n = 0; foreach (var c in calls) n += c.missed; return n; } }
    static readonly string[][] Callers =
    {
        new[]{"회사","어디쯤이야?"}, new[]{"엄마","밥은 먹고 다니니?"}, new[]{"고객님 대출","저금리 상품 안내드립니다"},
        new[]{"택배 기사님","문 앞에 놔둘까요?"}, new[]{"대학 동기","야 나 돈 좀..."}, new[]{"인사팀","연말정산 서류요"}, new[]{"010-XXXX-XXXX","여론조사입니다"},
    };

    // 효과
    class Fx { public SpriteRenderer sr; public float t, dur, size; public Color c; public int flip = 1; public bool beam; }
    class Shot { public GameObject go; public Vector2 pos; public int dir; public bool bomb, read, bead; public float speed = 11, vy, life, dmg, knock; public HashSet<Passenger> hit = new HashSet<Passenger>(); }
    class Spill { public GameObject go; public Vector2 pos; public float life, r = 1; }
    class Bubble { public string text; public Passenger who; public float ttl; }
    class Floater { public string text; public Vector2 pos; public float ttl; public Color c; public bool big; }
    readonly List<Fx> fxs = new List<Fx>(); readonly List<Shot> shots = new List<Shot>(); readonly List<Spill> spills = new List<Spill>();
    readonly List<Bubble> bubbles = new List<Bubble>(); readonly List<Floater> floaters = new List<Floater>();
    string playerLine; float playerLineT, sighT = 7;
    static readonly string[] Sighs = { "하아...", "집에 가고 싶다", "오늘 무슨 요일이지", "월급날 언제 오지", "어제도 이랬는데", "퇴사할까", "내일도 이러겠지", "앉고 싶다", "점심 뭐 먹지..." };

    public List<Choice> choices = new List<Choice>(); public string chooseTitle = "", chooseSub = "";

    void Awake()
    {
        Application.targetFrameRate = 60;
        GameData.Load();
        Sfx.Init(gameObject);
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 48);
        cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Camera"); DontDestroyOnLoad(cg); cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); cg.tag = "MainCamera"; }
        cam.orthographic = true; cam.orthographicSize = 3f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.08f, .08f, .1f);
        cam.transform.position = new Vector3(0, 0, -10);
        InitBot();
        BuildTitleScene();
    }

    void BuildTitleScene()
    {
        stageIndex = 0; route = new RouteDef("일반", "", 1, 0, 0);
        line = GameData.Lines[1]; doorCount = 6; startDoor = 8; targetX = doorCount * DoorGap; lobby = false; phase = Phase.Ride; trainSpeed = 1;
        root = new GameObject("Stage"); rootT = root.transform;
        BuildCar(false);
        cam.transform.position = new Vector3(8, 0, -10);
        string[] ids = { "school", "phone", "hike", "makeup", "bundle", "spread" };
        for (int i = 0; i < ids.Length; i++)
        {
            var d = System.Array.Find(GameData.Passengers, x => x.look == ids[i]);
            SpawnP(d, new Vector2(3.4f + i * 1.65f + Random.Range(-.2f, .2f), Random.Range(FloorBottom + .2f, FloorTop - .1f)));
        }
    }

    // ---------- 런 흐름 ----------

    public void StartRun()
    {
        wLv.Clear(); skLv = 0; weapons.Clear(); items.Clear(); skill = null; usedLines.Clear(); bubbles.Clear(); floaters.Clear();
        expressCount = 0; practice = false; curWeapon = 0; stageIndex = 0; kills = 0; beads = 3; badgeUses = 0; pendingRewards = 0; maxMental = 100; mental = 100 + Lv("vit") * 5; maxMental = mental;
        calls.Clear(); foreach (var who in Callers) calls.Add(new CallState { who = who, interval = BaseCall * 2.2f, timer = Random.Range(10f, 70f) });
        frontpack = deaf = false; route = new RouteDef("일반", "", 1, 0, 0); clockSec = 8 * 3600 + 20 * 60; callInterval = BaseCall;
        Recalc();
        var opts = botWeapon != null ? GameData.Weapons.FindAll(x => x.id == botWeapon) : Pick(WeaponPool(), 3);
        ShowChoices("오늘의 무기", "", opts.ConvertAll(w => WeaponChoice(w, () =>
        {
            var s = Pick(SkillPool(), 3);
            ShowChoices("오늘의 방해", "", s.ConvertAll(k => SkillChoice(k, () => BeginStage())));
        })));
    }

    static List<T> Pick<T>(List<T> src, int n)
    {
        var l = new List<T>(src);
        for (int i = 0; i < l.Count; i++) { int j = Random.Range(i, l.Count); var t = l[i]; l[i] = l[j]; l[j] = t; }
        if (l.Count > n) l.RemoveRange(n, l.Count - n);
        return l;
    }

    readonly Dictionary<WeaponDef, int> wLv = new Dictionary<WeaponDef, int>();
    int WL(WeaponDef w) { wLv.TryGetValue(w, out var n); return n; }

    const int MaxUp = 3;
    class Up { public float dmg, range, knock, cool, slow; public string desc; }

    // 무기 종류마다 강화 방향이 다르고, 대부분 대가가 있다
    static Up UpOf(string kind)
    {
        switch (kind)
        {
            case "back": return new Up { dmg = .15f, slow = .06f, desc = "위력 +15% · 반동 커짐 · 이속 -6%" };
            case "front": return new Up { dmg = .25f, knock = .15f, slow = .05f, desc = "위력 +25% · 이속 -5%" };
            case "stab": return new Up { dmg = .1f, cool = -.12f, desc = "더 빠름 · 더 오래 휘두름" };
            case "long": case "extend": return new Up { range = .18f, cool = .1f, desc = "사거리 +18% · 쿨타임 +10%" };
            case "spin": return new Up { dmg = .1f, cool = .05f, desc = "더 오래 느려짐" };
            case "wide": return new Up { dmg = .2f, cool = .08f, desc = "위력 +20% · 쿨타임 +8%" };
            case "throw": return new Up { desc = "한 번에 +1장" };
            case "bomb": return new Up { dmg = .25f, cool = .12f, desc = "위력 +25% · 쿨타임 +12%" };
            case "smash": return new Up { dmg = .25f, cool = .08f, desc = "위력 +25% · 쿨타임 +8%" };
            case "photo": return new Up { dmg = .2f, cool = .04f, desc = "더 오래 멈춤 · 위력 +20%" };
            case "stomp": case "single": return new Up { dmg = .25f, cool = .1f, desc = "위력 +25% · 쿨타임 +10%" };
            case "dash": return new Up { desc = "돌진 +15% · 넘어짐 +8%" };
            case "ram": return new Up { dmg = .15f, cool = .08f, desc = "터널 +0.5초 · 쿨타임 +8%" };
            case "sling": return new Up { dmg = .12f, slow = .05f, desc = "비즈 더 박음 · 이속 -5%" };
            case "pull": return new Up { dmg = .25f, range = .12f, desc = "위력 +25% · 사거리 +12%" };
            default: return new Up { range = .12f, cool = -.08f, desc = "범위 +12% · 쿨타임 -8%" };
        }
    }

    Choice WeaponUpChoice(WeaponDef w, Action then)
    {
        return new Choice { icon = w.id, title = w.name + " Lv." + (WL(w) + 2), sub = UpOf(w.kind).desc, tint = new Color(1, .86f, .5f), act = () => { wLv[w] = WL(w) + 1; then?.Invoke(); } };
    }

    // 방해 강화
    int skLv;
    float SkillCdMax => skill == null ? 1 : skill.cooldown * mCooldown * (1 - skLv * .12f);
    Choice SkillUpChoice(Action then)
    {
        return new Choice { icon = skill.id, title = skill.name + " Lv." + (skLv + 2), sub = skill.id == "badge" || skill.id == "dash" ? "쿨타임 -12%" : "멈춤 +25% · 쿨타임 -12%", tint = new Color(.7f, .86f, 1f), act = () => { skLv++; then?.Invoke(); } };
    }

    Choice WeaponChoice(WeaponDef w, Action then)
    {
        return new Choice { icon = w.id, title = w.name, sub = w.desc, replaces = weapons.Count >= 2 ? weapons[curWeapon].name + " 대신" : null, tint = new Color(1, .93f, .8f), act = () =>
        {
            if (weapons.Count < 2) { weapons.Add(w); curWeapon = weapons.Count - 1; } else weapons[curWeapon] = w;
            then?.Invoke();
        } };
    }

    Choice SkillChoice(SkillDef k, Action then)
    {
        var side = k.desc.Contains("·") ? k.desc.Substring(k.desc.IndexOf('·') + 1).Trim() : "";
        var reach = k.radius >= 5 ? "넓게" : k.radius >= 3.8f ? "주변" : k.radius > 0 ? "가까이" : "앞바닥";
        return new Choice { icon = k.id, title = k.name, sub = k.id == "badge" || k.id == "dash" ? k.desc : reach + " " + k.duration.ToString("0.#") + "초 멈춤" + (side.Length > 0 ? "\n" + side : ""), replaces = skill != null ? skill.name + " 대신" : null, tint = new Color(.82f, .93f, 1f), act = () => { skill = k; skLv = 0; then?.Invoke(); } };
    }

    static readonly string[] GradeNames = { "평범", "좋음", "대박", "위험한 대박" };
    static readonly Color[] GradeTints = { new Color(.92f, .92f, .9f), new Color(.8f, .96f, .8f), new Color(1, .86f, .42f), new Color(.92f, .62f, .78f) };
    bool rerollUsed; int expressCount;

    // 등급을 먼저 뽑고 그 등급에서 고른다. 급행 뒤엔 좋은 등급이 잘 나온다
    ItemDef RollItem(List<ItemDef> exclude)
    {
        float r = Random.value;
        bool rich = route != null && route.Express;
        int g = r < (rich ? .06f : .02f) ? 3 : r < (rich ? .26f : .10f) ? 2 : r < (rich ? .66f : .40f) ? 1 : 0;
        var pool = GameData.Items.FindAll(i => i.grade == g && !exclude.Contains(i));
        if (pool.Count == 0) pool = GameData.Items.FindAll(i => !exclude.Contains(i));
        return pool[Random.Range(0, pool.Count)];
    }

    Choice ItemChoice(ItemDef it, Action then)
    {
        return new Choice { item = it, gradeText = GradeNames[it.grade], icon = it.icon, title = it.name, sub = it.desc, tint = GradeTints[it.grade], act = () => { AddItem(it); then?.Invoke(); } };
    }

    void AddItem(ItemDef it)
    {
        items.Add(it);
        maxMental = Mathf.Max(30, maxMental + it.maxMental);
        mental = Mathf.Min(maxMental, mental + it.heal);
        if (it.id == "frontpack") frontpack = true;
        if (it.id == "earphone") deaf = true;
        if (it.id == "beads") beads += 4;
        Recalc();
    }

    void Recalc()
    {
        mSpeed = mDamage = mCooldown = mKnock = mTaken = 1;
        mSpeed += Lv("run") * .02f; mDamage += Lv("gym") * .03f; mKnock += Lv("push") * .04f; mTaken -= Lv("guard") * .03f; mCooldown -= Lv("cool") * .02f;
        foreach (var i in items) { mSpeed += i.speed; mDamage += i.damage; mCooldown += i.cooldown; mKnock += i.knock; mTaken += i.taken; }
        mSpeed = Mathf.Max(.5f, mSpeed); mCooldown = Mathf.Max(.45f, mCooldown); mTaken = Mathf.Max(.3f, mTaken);
    }

    void ShowChoices(string title, string sub, List<Choice> list)
    {
        state = St.Choose; chooseTitle = title; chooseSub = sub; choices = list; Sfx.Play("pick", .3f);
        BotOnChoices();
    }

    public void Choose(int i)
    {
        if (state != St.Choose || i < 0 || i >= choices.Count) return;
        Sfx.Play("pick");
        choices[i].act();
    }

    void AfterStage()
    {
        stageIndex++;
        mental = Mathf.Min(maxMental, mental + 25);
        pendingRewards = 1 + route.bonus; rerollUsed = false;
        NextReward();
    }

    void NextReward()
    {
        if (pendingRewards <= 0)
        {
            if (stageIndex >= 3) { BeginStage(); return; }
            int stops = 3 + stageIndex, fast = Mathf.Max(2, (stops + 1) / 2);
            ShowChoices("다음 노선", "", new List<Choice>
            {
                RouteChoice(new RouteDef("일반", "정차 " + stops + "번\n사람 보통 · 시간 넉넉\n보상 기본", 1f, 0, 0), "gear"),
                RouteChoice(new RouteDef("급행", "정차 " + fast + "번 · 역을 건너뜀\n사람 +60% · 시간 짧음\n월급 +50% · 좋은 카드 · 1장 더", 1.6f, 0, 1), "speed"),
            });
            return;
        }
        pendingRewards--;
        var list = new List<Choice>();
        Action next = NextReward;
        var shown = new List<ItemDef>();
        var firstItem = RollItem(shown); shown.Add(firstItem);
        list.Add(ItemChoice(firstItem, next));
        // 강화 자리는 무기와 방해가 번갈아 뽑힌다
        var held = weapons[Mathf.Clamp(curWeapon, 0, weapons.Count - 1)];
        bool canW = WL(held) < MaxUp, canS = skill != null && skLv < MaxUp;
        if (canW && (!canS || Random.value < .5f)) list.Add(WeaponUpChoice(held, next));
        else if (canS) list.Add(SkillUpChoice(next));
        else list.Add(ItemChoice(RollItem(shown), next));
        bool giveWeapon = Random.value < .6f;
        if (giveWeapon)
        {
            var pool = WeaponPool().FindAll(w => !weapons.Contains(w));
            list.Add(WeaponChoice(pool[Random.Range(0, pool.Count)], next));
        }
        else
        {
            var pool = SkillPool().FindAll(k => k != skill);
            list.Add(SkillChoice(pool[Random.Range(0, pool.Count)], next));
        }
        ShowChoices("환승 통로 매점", "", list);
    }

    Choice RouteChoice(RouteDef r, string icon)
    {
        return new Choice { icon = icon, title = r.name, sub = r.tag, act = () => { route = r; BeginStage(); } };
    }

    void GameOver(string reason)
    {
        if (state == St.Over) return;
        overReason = reason; state = St.Over; Sfx.Play("fail"); EndRun(false);
        if (!botOn) PlayerPrefs.SetInt("lates", PlayerPrefs.GetInt("lates", 0) + 1);
        BotOnEnd(false);
    }

    void GameClear()
    {
        state = St.Clear; Sfx.Play("transfer"); EndRun(true);
        if (!botOn) PlayerPrefs.SetInt("wins", PlayerPrefs.GetInt("wins", 0) + 1);
        BotOnEnd(true);
    }

    // ---------- 스테이지 만들기 ----------

    GameObject Spr(string name, Sprite s, Vector2 p, int order, Transform parent = null)
    {
        var g = new GameObject(name); g.transform.SetParent(parent != null ? parent : rootT); g.transform.position = p;
        var r = g.AddComponent<SpriteRenderer>(); r.sprite = s; r.sortingOrder = order;
        return g;
    }

    GameObject Txt(string text, Vector2 p, float height, Color c, int order)
    {
        var g = new GameObject("t"); g.transform.SetParent(rootT); g.transform.position = p;
        var t = g.AddComponent<TextMesh>(); t.font = font; t.fontSize = 48; t.characterSize = height / 4.8f; t.text = text; t.color = c;
        t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
        var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = font.material; mr.sortingOrder = order;
        return g;
    }

    void ClearStage()
    {
        if (root != null) Destroy(root);
        crowd.Clear(); doors.Clear(); stationTexts.Clear(); straps.Clear(); lights.Clear(); fxs.Clear(); shots.Clear(); spills.Clear(); bubbles.Clear(); floaters.Clear(); layers.Clear(); fore.Clear(); zones.Clear(); stationRoot = null; vignette = null; portal = null; pRig = null;
        arrow = null; boss = null;
    }

    void BeginStage()
    {
        ClearStage();
        state = St.Stage;
        root = new GameObject("Stage"); rootT = root.transform;
        lobby = stageIndex >= 3; bossDown = false; arrived = false; announced = false; stopType = 0; stageT = 0; ringing = false; onCallT = 0; callTimer = 6;
        var lineIdx = Random.Range(0, GameData.Lines.Length);
        while (usedLines.Contains(lineIdx)) lineIdx = (lineIdx + 1) % GameData.Lines.Length;
        usedLines.Add(lineIdx);
        line = GameData.Lines[lineIdx];
        doorCount = practice ? 30 : lobby ? 6 : 5 + stageIndex * 2;
        startDoor = Random.Range(0, 40 - doorCount);
        targetX = doorCount * DoorGap; gateX = targetX;
        stopsLeft = route.Express ? Mathf.Max(2, (3 + stageIndex + 1) / 2) : 3 + stageIndex; stopNo = 0; passDone = false; passT = 0; if (route.Express && !practice) expressCount++; finalStop = false;
        stationI = Random.Range(0, Mathf.Max(1, line.stations.Length - stopsLeft));
        lures.Clear(); tunnels.Clear(); BuildCar(stageIndex == 1);

        // 플레이어
        pRig = new Rig("player", rootT); pRig.root.localScale = Vector3.one * .9f;
        wGo = Spr("weapon", Art.Icon("tumbler"), Vector2.zero, 0); wSr = wGo.GetComponent<SpriteRenderer>();
        pPos = new Vector2(0, -1.75f); pVel = Vector2.zero; facing = 1; selfStun = dizzy = latched = noAttack = dashT = atkCd = atkAnim = 0; skillCd = 0;

        var a = Txt("▼", new Vector2(targetX, 2.1f), .6f, new Color(1, .75f, 0), 9000); arrow = a.transform;

        if (lobby)
        {
            lobbyT = 75; clockSec = 8 * 3600 + 58 * 60 + 45;
            Spr("gate", Art.Gate(), new Vector2(gateX, -1.95f), Order(-1.95f));
            boss = SpawnP(GameData.Boss, new Vector2(targetX * .55f, -1.75f)); boss.isBoss = true; boss.size = 1.12f; boss.hp *= .65f + Diff * .12f; boss.maxHp = boss.hp; Speak(boss, "자네, 잠깐 얘기 좀 하지", 3f);
            for (int i = 0; i < 12; i++) SpawnP(GameData.Coworker, new Vector2(Random.Range(4f, targetX - 1), Random.Range(FloorBottom, FloorTop)));
        }
        else
        {
            phase = Phase.Ride; phaseT = 8f; trainSpeed = 1; doorOpen = 0; arrived = false; sweepWarn = 0;
            stationName = line.stations[stationI % line.stations.Length];
            int n = Mathf.RoundToInt(CrowdWant() * .8f);
            for (int i = 0; i < n; i++) SpawnP(RandomDef(), new Vector2(Random.Range(1.2f, 11f), Random.Range(FloorBottom, FloorTop)));
            for (int i = 0; i < n / 4; i++) SpawnP(RandomDef(), new Vector2(Random.Range(-5f, -1.2f), Random.Range(FloorBottom, FloorTop)));
        }
        cam.transform.position = new Vector3(1, 0, -10);
        BotOnStage();
    }

    void AddLayer(Sprite spr, float tw, int count, float speed, float y, float sy, int order, Color c)
    {
        var l = new Layer { tw = tw, speed = speed, t = new Transform[count] };
        for (int i = 0; i < count; i++)
        {
            var g = Spr("bg", spr, new Vector2(0, y), order); g.transform.localScale = new Vector3(tw / spr.bounds.size.x, sy, 1);
            g.GetComponent<SpriteRenderer>().color = c; l.t[i] = g.transform;
        }
        layers.Add(l);
    }

    // 열차 안: 건너편 벽, 바닥, 앉은 승객, 손잡이, 그리고 창밖
    void BuildCar(bool outdoor)
    {
        const float wallY = -0.7f;
        var bg = Spr("bg", Art.Pixel(), new Vector2(targetX / 2, 1), -1000); bg.transform.localScale = new Vector3(60 + targetX, 12, 1);
        bg.GetComponent<SpriteRenderer>().color = lobby ? new Color(.78f, .86f, .92f) : new Color(.04f, .04f, .06f);
        if (!lobby)
        {
            if (outdoor)
            {
                var sky = Spr("sky", Art.Sky(), new Vector2(targetX / 2, 1.1f), -999); sky.transform.localScale = new Vector3(60 + targetX, 1.2f, 1);
                AddLayer(Art.Cloud(), 2.5f, 3, .6f, 1.62f, .8f, -998, Color.white); layers[layers.Count - 1].tw = 7;
                AddLayer(Art.Skyline(0), 8, 3, 1.6f, 1.05f, 1.15f, -998, Color.white);
                AddLayer(Art.Skyline(1), 8, 3, 4f, 1f, 1f, -997, Color.white);
                AddLayer(Art.Truss(), 2, 1, 22f, 1.1f, 1.5f, -996, Color.white);
                layers[layers.Count - 1].tw = 9;
            }
            else
            {
                AddLayer(Art.Tunnel(), 4, 7, 30f, 1.1f, 2.1f, -999, Color.white);
                AddLayer(Art.TunnelLight(), 1.4f, 1, 30f, 1.62f, 1f, -997, Color.white);
                layers[layers.Count - 1].tw = 5.5f;
            }
            stationRoot = new GameObject("station").transform; stationRoot.SetParent(rootT);
            for (float x = -66; x < targetX + 66; x += 6)
            {
                Spr("st", Art.Station(line.color), new Vector2(x, 1.1f), -950, stationRoot);
                var nm = Txt("", new Vector2(x - .98f, 1.46f), .3f, Color.white, -945); nm.transform.SetParent(stationRoot, true);
                stationTexts.Add(nm);
            }
            stationRoot.gameObject.SetActive(false);
        }
        int seed = stageIndex * 13 + 1;
        var rng = new System.Random(startDoor * 17 + seed);
        for (int i = -3; i <= doorCount + 3; i++)
        {
            float x = i * DoorGap; int idx = startDoor + i, car = Mathf.FloorToInt(idx / 4f) + 1, dn = ((idx % 4) + 4) % 4 + 1;
            Spr("wall", lobby ? Art.LobbyWall() : Art.Wall(line.color), new Vector2(x, wallY), -800);
            Spr("floor", lobby ? Art.LobbyFloor() : Art.Floor(), new Vector2(x, wallY), -800);
            if (lobby) continue;
            string label = car + "-" + dn; bool isT = i == doorCount;
            var dl = Spr("dl", Art.DoorLeaf(), new Vector2(x - .28f, wallY), -900); var dr = Spr("dr", Art.DoorLeaf(), new Vector2(x + .28f, wallY), -900);
            doors.Add(new[] { dl.transform, dr.transform });
            var zone = Spr("zone", Art.Zone(), new Vector2(x, -1.5f), -660); zone.GetComponent<SpriteRenderer>().color = Color.clear; zones.Add(zone.GetComponent<SpriteRenderer>());
            Txt(label, new Vector2(x, 2.16f), .17f, new Color(1, .35f, .25f), -790);
            if (isT)
            {
                var pl = Spr("sticker", Art.Pixel(), new Vector2(x, -1.23f), -795); pl.transform.localScale = new Vector3(1.1f, .36f, 1); pl.GetComponent<SpriteRenderer>().color = new Color(1, .8f, .15f);
                Txt("빠른 환승 " + label, new Vector2(x, -1.23f), .15f, new Color(.15f, .1f, 0), -790);
            }
            else Txt(label, new Vector2(x, -1.23f), .16f, new Color(.25f, .24f, .22f, .75f), -790);
            foreach (float side in new[] { -1.3f, 1.3f })
            {
                int ad = rng.Next(GameData.Ads.Length);
                Spr("ad", Art.Ad(ad), new Vector2(x + side, 2.07f), -795);
                Txt(GameData.Ads[ad].Replace("\n", " "), new Vector2(x + side, 2.07f), .1f, new Color(.15f, .15f, .2f), -790);
                for (int k = -1; k <= 1; k++) { var tr = Spr("strap", Art.Strap(), new Vector2(x + side + k * .46f, 2.32f), -760); straps.Add(tr.transform); }
            }
            foreach (float px in new[] { -.8f, .8f }) { var pol = Spr("pole", Art.Pole(), new Vector2(x + px, -1.02f), -770); pol.transform.localScale = new Vector3(1, 1.7f, 1); }
            if (((idx % 4) + 4) % 4 == 3) Spr("car", Art.ConnectDoor(line.color), new Vector2(x + 2, wallY), -775);
        }
        if (!lobby)
        {
            var rail = Spr("rail", Art.Pixel(), new Vector2(targetX / 2, 2.32f), -765); rail.transform.localScale = new Vector3(60 + targetX, .035f, 1); rail.GetComponent<SpriteRenderer>().color = new Color(.72f, .73f, .76f);
            // 카메라 바로 앞 손잡이: 더 빨리 움직여 안에 있는 느낌을 준다
            for (int i = 0; i < 3; i++)
            {
                var f = Spr("fore", Art.Strap(), new Vector2(0, 3.2f), 9500); f.transform.localScale = Vector3.one * 1.5f; f.GetComponent<SpriteRenderer>().color = new Color(.62f, .62f, .68f, .7f);
                fore.Add(new Fore { t = f.transform, x = i * 7f });
            }
        }
        var v = Spr("vig", Art.Vignette(), Vector2.zero, 9900); vignette = v.transform; vignette.localScale = new Vector3(1.5f, 1.5f, 1);
    }

    int CrowdWant() { return lobby ? 16 : Mathf.Min(MaxCrowd, Mathf.RoundToInt((46 + stopNo * 3) * route.density * HardCrowd)); }

    float pressT, pressLoad, pushLineT, openT, sweepWarn; bool arrived, boarded, announced; int stopType; string nextName = "";
    readonly List<SpriteRenderer> zones = new List<SpriteRenderer>();
    static readonly string[] PushLines = { "잠시만요 잠시만요", "내립니다! 내려요!", "저 환승해야 돼요", "죄송합니다...", "지나갈게요...", "숨 좀 쉴게요" };

    void TickPressure(float dt)
    {
        for (int i = crowd.Count - 1; i >= 0; i--)
        {
            var p = crowd[i];
            if (p.isBoss || p.dying || Mathf.Abs(p.pos.x - pPos.x) < 13.5f) continue;
            Destroy(p.rig.go); crowd.RemoveAt(i); bubbles.RemoveAll(b => b.who == p);
        }
        pressT -= dt;
        if (pressT > 0 || phase == Phase.Exit) return;
        pressT = .07f;
        if (crowd.Count >= CrowdWant()) return;
        float cx = cam.transform.position.x;
        bool ahead = Random.value < .65f && cx + 7.5f < targetX + 9;
        var np = SpawnP(lobby ? GameData.Coworker : RandomDef(), new Vector2(ahead ? cx + 7.5f : cx - 7.5f, Random.Range(FloorBottom, FloorTop)));
        np.vel.x = ahead ? -2 : 2;
    }

    PassengerDef RandomDef()
    {
        int max = GameData.Passengers.Length;
        return GameData.Passengers[Random.Range(0, stageIndex == 0 ? max - 2 : max)];
    }

    Passenger SpawnP(PassengerDef d, Vector2 pos)
    {
        var p = new Passenger { def = d, pos = pos, hp = d.hp * 1f * (1 + stageIndex * .12f) * HardHp, chaser = Random.value < .25f, phase = Random.value * 10, attackT = Random.value };
        p.maxHp = p.hp; if (d.small) p.size = .68f;
        p.rig = new Rig(d.look, rootT); p.rig.root.localScale = Vector3.one * p.size;
        crowd.Add(p);
        return p;
    }

    public int Doorstep() { return Mathf.RoundToInt(pPos.x / DoorGap); }
    public string DoorLabel(int i) { int idx = startDoor + i; return (Mathf.FloorToInt(idx / 4f) + 1) + "-" + (((idx % 4) + 4) % 4 + 1); }

    // ---------- 매 프레임 ----------

    void Update()
    {
        BotUpdate();
        ReadKeys();
        float dt = Mathf.Min(Time.deltaTime, .05f);
        if (state == St.Stage) TickStage(dt);
        else
        {
            if (state == St.Title) trainSpeed = 1;
            if (phase != Phase.Exit) foreach (var p in crowd) if (!p.dying) { p.phase += dt * 2; p.rig.Pose(p.pos, pFacingOf(p), 0, 0, 0, 0, p.phase); p.rig.SetOrder(Order(p.pos.y)); }
            DrawEnv(dt);
        }
    }

    struct Keys { public float mx, my; public bool atk, skillDown, swapDown, accept, reject; }
    Keys k;

    void ReadKeys()
    {
        k = new Keys();
        if (botOn) { k = botKeys; return; }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) k.mx -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) k.mx += 1;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) k.my += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) k.my -= 1;
        k.atk = Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.Z);
        k.skillDown = Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.X);
        k.swapDown = Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.C);
        k.accept = Input.GetKeyDown(KeyCode.E);
        k.reject = Input.GetKeyDown(KeyCode.R);
        if (Input.GetKeyDown(KeyCode.M)) Sfx.ToggleMute();
        if (practice && state == St.Stage && Input.GetKeyDown(KeyCode.Escape)) ExitPractice();
        if (state == St.Title && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) OpenMap();
        else if (state == St.Map && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) PlayStage(selected);
        if ((state == St.Over || state == St.Clear) && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) { AfterEnd(); }
        if (state == St.Choose)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Choose(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Choose(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Choose(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) Choose(3);
        }
    }

    void TickStage(float dt)
    {
        if (hitStop > 0) { hitStop -= Time.deltaTime; dt *= .12f; }
        stageT += dt;
        if (practice) TickPractice(dt);
        else if (phase != Phase.Exit) clockSec += dt * 9;
        TickPhase(dt);
        if (state != St.Stage) return;
        TickCall(dt);
        TickPlayer(dt);
        if (state != St.Stage) return;
        TickPressure(dt);
        TickCrowd(dt);
        TickFx(dt);
        TickWeaponFx(dt);
        DrawWorld(dt);
    }

    float passT; bool passDone; string passName = "";

    void TickPhase(float dt)
    {
        if (practice) { trainSpeed = 1; announced = false; return; }
        if (lobby)
        {
            phase = Phase.Ride; lobbyT -= dt; clockSec = 8 * 3600 + 60 * 60 - lobbyT;
            if (lobbyT <= 0) { GameOver("late"); return; }
            if (bossDown && pPos.x >= gateX - 1.3f) GameClear();
            return;
        }
        if (!arrived && phase != Phase.Exit && Mathf.Abs(pPos.x - targetX) < 1.5f)
        {
            arrived = true; ringing = false; onCallT = 0; Say("휴... 겨우 도착했다", 3f); Sfx.Play("pick");
            if (!finalStop) stopsLeft = 1;
            if (phase == Phase.Ride) phaseT = Mathf.Min(phaseT, 1.2f);
            else if (phase == Phase.Open && !finalStop) phaseT = Mathf.Min(phaseT, .3f);
        }
        phaseT -= dt;
        trainSpeed = Mathf.MoveTowards(trainSpeed, phase == Phase.Ride || phase == Phase.Leave ? 1 : 0, dt * .8f);
        doorOpen = Mathf.MoveTowards(doorOpen, phase == Phase.Open || phase == Phase.Exit ? 1 : 0, dt * 1.6f);
        switch (phase)
        {
            case Phase.Ride:
                // 급행: 역을 그대로 지나친다
                if (route.Express && !passDone && !arrived && phaseT > 6f && phaseT < 8.5f)
                {
                    passDone = true; passT = 1.5f;
                    passName = line.stations[(stationI + stopNo + 3) % line.stations.Length];
                    foreach (var t in stationTexts) t.GetComponent<TextMesh>().text = passName;
                    Sfx.Play("close", .3f);
                }
                if (passT > 0) passT -= dt;
                if (!announced && phaseT < 5.5f)
                {
                    announced = true;
                    bool last = stopsLeft <= 1;
                    nextName = last && stageIndex == 2 ? CurStage.name : line.stations[(stationI + stopNo + 1) % line.stations.Length];
                    stopType = last ? 2 : Random.Range(0, 3);
                    Sfx.Play("annc", .8f);
                }
                if (phaseT <= 0)
                {
                    stopsLeft--; finalStop = stopsLeft <= 0; stopNo++;
                    phase = Phase.Brake; phaseT = 3f;
                    stationName = finalStop && stageIndex == 2 ? CurStage.name : line.stations[(stationI + stopNo) % line.stations.Length];
                    foreach (var t in stationTexts) t.GetComponent<TextMesh>().text = stationName;
                    Lurch(1.5f);
                }
                break;
            case Phase.Brake:
                if (phaseT <= 0) { phase = Phase.Open; phaseT = finalStop ? 7f : 6.2f; openT = 0; boarded = false; Sfx.Play(finalStop ? "transfer" : "door"); MarkExiters(); }
                break;
            case Phase.Open:
                openT += dt;
                if (!boarded && openT > (stopType == 1 ? .5f : ExitWave)) { boarded = true; if (!finalStop && !arrived) Board(); }
                if (finalStop && Mathf.Abs(pPos.x - targetX) < 1.5f) { phase = Phase.Exit; phaseT = 1.1f; ringing = false; onCallT = 0; break; }
                if (phaseT <= 0)
                {
                    Sfx.Play("close");
                    if (finalStop) { GameOver("missed"); return; }
                    phase = Phase.Leave; phaseT = 1.1f;
                }
                break;
            case Phase.Leave:
                if (phaseT <= 0) { announced = false; passDone = false; phase = Phase.Ride; phaseT = arrived ? 1f : (route.Express ? 9.5f : 10.5f); Lurch(-1.2f); }
                break;
            case Phase.Exit:
                if (phaseT <= 0) AfterStage();
                break;
        }
    }

    // 지하철이 서거나 출발할 때 다 같이 쏠린다
    void Lurch(float v)
    {
        pVel.x += v;
        foreach (var p in crowd) p.vel.x += v / p.def.weight;
    }

    void Board()
    {
        int here = Doorstep();
        int n = Mathf.RoundToInt((4f + stopNo * .7f) * (stopType == 1 ? 1.8f : stopType == 0 ? .35f : 1f) * route.density * HardCrowd + stageIndex * .5f);
        for (int d = here - 2; d <= Mathf.Min(doorCount - 1, here + 3); d++)
        {
            if (d < 0) continue;
            for (int i = 0; i < n && crowd.Count < MaxCrowd; i++)
            {
                var p = SpawnP(RandomDef(), new Vector2(d * DoorGap + Random.Range(-.5f, .5f), FloorTop + .1f));
                p.vel.y = -2.6f;
            }
        }
    }

    // 문 근처 사람들 일부가 내리려고 문으로 몰린다
    void MarkExiters()
    {
        foreach (var p in crowd)
        {
            if (p.dying || p.isBoss) continue;
            float dx = p.pos.x - Mathf.Round(p.pos.x / DoorGap) * DoorGap;
            if (stopType != 1 && Mathf.Abs(dx) < 2.6f && Random.value < (stopType == 0 ? .65f : .4f)) p.exiting = true;
        }
    }

    bool InExitWave() { return !lobby && !arrived && stopType != 1 && phase == Phase.Open && openT < ExitWave && doorOpen > .4f; }

    // 내리는 떼는 문으로 끌고 가고, 타는 떼는 문에서 밀어낸다. 문 밖까지 끌려가면 끝.
    void DoorFlow(float dt)
    {
        sweepWarn = 0;
        if (lobby || arrived || practice || dashT > 0 || phase != Phase.Open || doorOpen < .4f) return;
        int d = Mathf.RoundToInt(pPos.x / DoorGap);
        var door = new Vector2(d * DoorGap, FloorTop);
        Vector2 to = door - pPos; float dist = to.magnitude;
        if (dist > 3.6f) return;
        float st = 1 - dist / 3.6f;
        if (InExitWave())
        {
            if (dist > .01f) { var n = to / dist; pPos += new Vector2(n.x, n.y * .55f) * (.25f + 1.5f * st) * (.72f + Diff * .035f) * Mathf.Clamp01(openT) * dt; }
            sweepWarn = st;
            if (st > .35f && playerLineT <= 0) Say("어어어?!", .8f);
            if (Mathf.Abs(to.x) < .6f && pPos.y >= FloorTop - .1f && !(finalStop && d == doorCount)) { GameOver("pushed"); }
        }
        else if (dist > .01f && openT > (stopType == 1 ? .5f : ExitWave)) pPos -= to / dist * 1.8f * st * dt;
    }

    void TickCall(float dt)
    {
        if (phase == Phase.Exit || arrived || practice) return;
        if (ringing)
        {
            ringT -= dt; ringBeep -= dt;
            if (ringBeep <= 0) { ringBeep = .55f; Sfx.Play("close", .35f); }
            if (k.accept) AnswerCall(); else if (k.reject || ringT <= 0) RejectCall();
            return;
        }
        if (onCallT > 0) { onCallT -= dt; return; }
        callTimer -= dt;
        foreach (var c in calls) c.timer -= dt;
        if (callTimer > 0) return;
        foreach (var c in calls)
            if (c.timer <= 0)
            {
                curCall = c; caller = c.who; callTimer = 5;
                ringing = true; ringT = 2.8f; ringBeep = 0;
                break;
            }
    }

    // 받으면 그 사람 부재중만 사라진다. 많이 쌓였을수록 오래 붙잡힌다
    public void AnswerCall()
    {
        if (!ringing) return;
        ringing = false; onCallT = 2.8f + curCall.missed * .7f;
        Say(curCall.missed >= 2 ? "죄송합니다... 네... 죄송합니다..." : caller[0] == "회사" ? "네! 네! 네에!" : "아 지금 지하철인데요...", 2.6f);
        curCall.missed = 0; curCall.interval = BaseCall * 2.2f; curCall.timer = curCall.interval;
    }

    // 거절한 사람은 점점 더 빨리 다시 건다
    public void RejectCall()
    {
        if (!ringing) return;
        ringing = false; curCall.missed++;
        curCall.interval = Mathf.Max(9f, curCall.interval * .6f); curCall.timer = curCall.interval;
        Sfx.Play("ouch", .3f);
    }

    void Say(string s, float t) { playerLine = s; playerLineT = t; }

    void TickPlayer(float dt)
    {
        atkCd -= dt; skillCd -= dt; hurtCd -= dt; pFlash -= dt; selfStun -= dt; dizzy -= dt; latched -= dt; noAttack -= dt; extT -= dt; atkAnim -= dt; playerLineT -= dt;
        if (extT <= 0) extend = 0;
        sighT -= dt;
        if (sighT <= 0) { sighT = Random.Range(9f, 16f); if (playerLineT <= 0 && !ringing && onCallT <= 0) Say(Sighs[Random.Range(0, Sighs.Length)], 2f); }
        var w = weapons[Mathf.Clamp(curWeapon, 0, weapons.Count - 1)];

        if (k.swapDown && weapons.Count > 1) { curWeapon = (curWeapon + 1) % weapons.Count; Sfx.Play("pick", .3f); w = weapons[curWeapon]; }

        if (atkWind > 0) { atkWind -= dt; if (atkWind <= 0 && windW != null) Attack(windW); }
        bool frozen = ringing || onCallT > 0 || selfStun > 0 || phase == Phase.Exit || arrived || atkWind > 0;
        if (phase == Phase.Exit)
        {
            pPos = Vector2.MoveTowards(pPos, new Vector2(targetX, FloorTop + .35f), dt * 3f);
            pRig.SetColor(new Color(1, 1, 1, Mathf.Clamp01(phaseT)));
            wSr.color = Color.clear; pMoving = 1; pAnim += dt * 9;
            pVel = Vector2.zero;
            return;
        }

        Vector2 mv = Vector2.zero;
        if (!frozen)
        {
            mv = new Vector2(k.mx, k.my);
            if (mv.sqrMagnitude > 1) mv.Normalize();
            float sp = BaseSpeed * mSpeed;
            if (HasBeadBag()) sp *= Mathf.Max(.55f, 1 - beads * .025f);
            sp *= 1 - WL(w) * UpOf(w.kind).slow;
            if (dizzy > 0) sp *= .65f;
            if (latched > 0) sp *= .45f;
            sp *= Mathf.Max(InExitWave() ? .8f : .3f, 1 / (1 + pressLoad * .17f));
            mv.x *= sp; mv.y *= sp * .8f;
            if (!k.atk && k.mx != 0)
            {
                int nf = k.mx > 0 ? 1 : -1;
                bool turned = nf != facing;
                facing = nf;
                if (turned && w.kind == "back" && !frontpack && atkCd <= 0 && noAttack <= 0) Attack(w);
            }
        }
        if (dashT > 0)
        {
            dashT -= dt; mv = new Vector2(facing * dashSpeed, 0);
            if (dashRam)
                foreach (var p in crowd)
                    if (!p.dying && !dashHit.Contains(p) && (p.pos - pPos).magnitude < 1.1f) { dashHit.Add(p); Damage(p, w.damage * mDamage * (1 + WL(w) * UpOf(w.kind).dmg), w.knock * facing * mKnock, dashHit.Count == 1 ? w.hit : null); p.vel.y += (p.pos.y >= pPos.y ? 1 : -1) * 4; }
            if (dashT <= 0)
            {
                if (tripNext) { selfStun = .8f; tripNext = false; Say("아 신발끈!", 1f); }
                if (dashRam) tunnels.Add(new TunnelDef { x0 = Mathf.Min(ramStartX, pPos.x) - .4f, x1 = Mathf.Max(ramStartX, pPos.x) + .4f, y = pPos.y, life = 2f + WL(w) * .5f });
                dashRam = false;
            }
        }

        foreach (var s in spills)
            if (Mathf.Abs(pPos.x - s.pos.x) < s.r * 1.6f && Mathf.Abs(pPos.y - s.pos.y) < .5f) mv.x += facing * 2.4f;

        pVel = Vector2.Lerp(pVel, Vector2.zero, dt * 6);
        pPos += (mv + pVel) * dt;
        DoorFlow(dt);
        if (state != St.Stage) return;

        if (k.atk && atkCd <= 0 && noAttack <= 0 && !ringing && atkWind <= 0 && dashT <= 0) { Attack(w); if (w.kind == "back" && !frontpack) atkCd = .8f * mCooldown; }
        if (k.skillDown && skillCd <= 0 && skill != null && !ringing && onCallT <= 0) UseSkill();

        float maxX = lobby ? (bossDown ? gateX + 1 : gateX - 1.3f) : targetX + 8;
        pPos.x = Mathf.Clamp(pPos.x, -6, maxX);
        pPos.y = Mathf.Clamp(pPos.y, FloorBottom, FloorTop);
        pMoving = Mathf.MoveTowards(pMoving, mv.sqrMagnitude > .01f ? 1 : 0, dt * 8);
        pAnim += dt * (4 + mv.magnitude * 3.2f);
        if (mental <= 0) GameOver("mental");
    }

    bool HasBeadBag() { foreach (var w in weapons) if (w.kind == "sling") return true; return false; }

    void Damage(Passenger p, float dmg, float knock, string hitText)
    {
        p.hp -= godMode ? dmg * 8 : dmg; foreach (var sp in spills) if (Mathf.Abs(p.pos.x - sp.pos.x) < sp.r * 1.6f && Mathf.Abs(p.pos.y - sp.pos.y) < .5f) { knock *= 2; break; }
        if (p.def.heal && !p.dying) { mental = Mathf.Min(maxMental, mental + 2); Float("떡!", p.pos + new Vector2(.4f, 2f), new Color(1, .75f, .85f)); }
        if (p.def.smell) foreach (var o in crowd) if (o != p && !o.dying && (o.pos - p.pos).magnitude < 1.2f) o.stun = Mathf.Max(o.stun, .5f);
        p.vel.x += knock * 1.5f / p.def.weight; p.vel.y += Random.Range(-.6f, .6f); p.flash = .1f; p.hurtT = .35f; p.press = 1;
        float force = Mathf.Abs(knock) + dmg * .1f;
        p.hitLean = Mathf.Clamp(14 + force * 3.5f, 14, 42);
        AddSpark(p.pos + new Vector2(0, 1.2f));
        if (force > 4) { AddSpark(p.pos + new Vector2(0, 1.2f)); AddRing(p.pos + new Vector2(0, 1.1f), .9f, Color.white); }
        if (hitText != null)
        {
            Float(hitText, p.pos + new Vector2(0, 2.1f), new Color(1, .95f, .3f));
            if (floaters.Count > 0) floaters[floaters.Count - 1].big = force > 4;
        }
        if (p.hp <= 0)
        {
            p.dying = true; p.dieT = .8f; p.vel = new Vector2(knock >= 0 ? 4 : -4, 0); kills++;
            if (kills % 3 == 0) beads++;
            Speak(p, p.isBoss ? "자네 내일 면담 좀..." : GameData.DeadQuips[Random.Range(0, GameData.DeadQuips.Length)], 1.1f);
            if (p.isBoss) { bossDown = true; Sfx.Play("transfer"); }
        }
        else if (Random.value < .2f) Speak(p, p.def.quips[Random.Range(0, p.def.quips.Length)], 1.2f);
    }

    void Speak(Passenger p, string text, float ttl)
    {
        if (bubbles.Count >= 6) bubbles.RemoveAt(0);
        bubbles.Add(new Bubble { text = text, who = p, ttl = ttl });
    }

    void Float(string text, Vector2 pos, Color c) { if (floaters.Count < 28) floaters.Add(new Floater { text = text, pos = pos, ttl = .75f, c = c }); }

    void UseSkill()
    {
        if (skill.id == "badge") { UseBadge(); return; }
        if (skill.id == "dash")
        {
            skillCd = SkillCdMax; dashT = .34f; dashRam = false; dashSpeed = 9.5f; dashHit.Clear(); tripNext = Random.value < .12f;
            Say(skill.line, 1f); Dust(); Sfx.Play("후다닥!", .6f);
            return;
        }
        skillCd = SkillCdMax;
        Sfx.Play(skill.id == "speaker" ? "trot" : "skill", .55f);
        Say(skill.line, 1.8f);
        float dur = skill.duration * (1 + skLv * .25f);
                if (skill.id == "perfume") dizzy = 2f;
        if (skill.id == "phone") noAttack = dur;
        if (skill.id == "coffee")
        {
            var pos = pPos + new Vector2(facing * 2f, 0);
            var g = Spr("spill", Art.Puddle(), pos, -650); g.transform.localScale = new Vector3(1.05f, .85f, 1);
            spills.Add(new Spill { go = g, pos = pos, life = dur });
            return;
        }
        AddRing(pPos + new Vector2(0, 1), skill.radius, skill.id == "cough" ? new Color(.8f, .85f, .85f) : new Color(1, .6f, .85f));
        foreach (var p in crowd)
        {
            if (p.dying || (p.pos - pPos).magnitude > skill.radius) continue;
            p.stun = Mathf.Max(p.stun, p.isBoss ? dur * .5f : dur);
            if (skill.id == "speaker") p.angry = dur + 4;
            if (Random.value < .5f) Float(skill.id == "speaker" ? "♪" : "멈칫!", p.pos + new Vector2(0, 2.1f), new Color(.6f, .85f, 1f));
        }
    }

    void AddRing(Vector2 p, float size, Color c)
    {
        var g = Spr("ring", Art.Ring(), p, 9000);
        fxs.Add(new Fx { sr = g.GetComponent<SpriteRenderer>(), dur = .6f, size = size, c = c });
    }

    void AddSlash(WeaponDef w, string kind, int dir, float range)
    {
        Color col = kind == "back" ? new Color(1, .55f, .3f) : kind == "spin" ? new Color(1, .35f, .4f) : kind == "sling" ? new Color(1, .6f, .85f) : kind == "long" || kind == "extend" ? new Color(.55f, .85f, 1f) : new Color(1, .95f, .55f);
        if (kind == "spin" || kind == "wide") { AddRing(pPos + new Vector2(0, 1), range * 1.2f, col); return; }
        if (kind == "long" || kind == "extend" || kind == "stab" || kind == "single")
        {
            // 찌르는 무기는 곧은 선만 남긴다
            var b = Spr("beam", Art.Pixel(), pPos + new Vector2(dir * (range / 2 + .3f), 1.05f), 9000);
            b.transform.localScale = new Vector3(range, kind == "single" ? .3f : .1f, 1);
            fxs.Add(new Fx { sr = b.GetComponent<SpriteRenderer>(), dur = .13f, c = col, beam = true });
            return;
        }
        if (kind == "stomp") { AddSpark(pPos + new Vector2(dir * .7f, .15f)); return; }
        var g = Spr("slash", Art.Slash(), pPos + new Vector2(dir * .35f, 1.05f), 9000);
        fxs.Add(new Fx { sr = g.GetComponent<SpriteRenderer>(), dur = .16f, size = .55f + range * .42f, c = col, flip = dir });
    }

    void AddSpark(Vector2 p)
    {
        var g = Spr("spark", Art.Spark(), p + Random.insideUnitCircle * .2f, 9000); g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 90));
        fxs.Add(new Fx { sr = g.GetComponent<SpriteRenderer>(), dur = .16f, size = 2.2f, c = new Color(1, .95f, .55f) });
    }

    int pFacingOf(Passenger p) { return (pRig != null ? pPos.x : targetX / 2) < p.pos.x ? -1 : 1; }

    void Hurt(float amount, Passenger from)
    {
        if (hurtCd > 0 || phase == Phase.Exit || arrived || dashT > 0 || practice) return;
        hurtCd = 1.4f; mental -= godMode ? 0 : amount * .34f * mTaken; pFlash = .15f; Sfx.Play("ouch", .4f);
        if (Random.value < .35f) Speak(from, from.def.quips[Random.Range(0, from.def.quips.Length)], 1.2f);
        if (from.def.latch) latched = .9f;
    }

    void TickCrowd(float dt)
    {
        for (int i = crowd.Count - 1; i >= 0; i--)
        {
            var p = crowd[i];
            p.flash -= dt;
            if (p.dying)
            {
                p.dieT -= dt; p.pos += p.vel * dt; p.rig.root.position = p.pos + new Vector2(0, (0.8f - p.dieT) * 2.2f);
                p.rig.root.Rotate(0, 0, (p.vel.x > 0 ? -1 : 1) * 420 * dt);
                p.rig.SetColor(new Color(1, 1, 1, Mathf.Clamp01(p.dieT * 1.6f)));
                p.rig.SetFace(Art.Face.Dizzy); p.rig.HideShadow();
                if (p.dieT <= 0) { Destroy(p.rig.go); crowd.RemoveAt(i); bubbles.RemoveAll(b => b.who == p); }
                continue;
            }
            p.stun -= dt; p.angry -= dt; p.attackT -= dt; p.hurtT -= dt; p.hitLean = Mathf.MoveTowards(p.hitLean, 0, dt * 90); p.phase += dt * 2; p.slow -= dt; p.fall -= dt; p.posing -= dt;
            if (p.flail > 0)
            {
                p.flail -= dt; p.flailT -= dt; p.stun = Mathf.Max(p.stun, .1f);
                if (p.flailT <= 0)
                {
                    p.flailT = .28f;
                    foreach (var o in crowd)
                        if (o != p && !o.dying) { var away = o.pos - p.pos; if (away.magnitude < 1.25f) { o.vel += away.normalized * 3.4f / o.def.weight; o.press = 1; } }
                }
            }
            if (p.bowl > 0)
            {
                p.bowl -= dt; p.vel.x = p.bowlDir * 7.5f; p.stun = Mathf.Max(p.stun, .15f);
                foreach (var o in crowd)
                    if (o != p && !o.dying && Mathf.Abs(o.pos.x - p.pos.x) < .75f && Mathf.Abs(o.pos.y - p.pos.y) < .75f && o.fall <= 0)
                    {
                        o.vel += new Vector2(p.bowlDir * 3, (o.pos.y >= p.pos.y ? 1 : -1) * 5) / Mathf.Max(1, o.def.weight * .7f);
                        Trip(o, .7f, 0, p.bowlDir); AddSpark(o.pos + new Vector2(0, 1));
                    }
            }
            if (p.exiting)
            {
                if (phase != Phase.Open) p.exiting = false;
                else
                {
                    float dx = Mathf.Round(p.pos.x / DoorGap) * DoorGap - p.pos.x;
                    p.vel = Vector2.Lerp(p.vel, new Vector2(dx, FloorTop + .3f - p.pos.y).normalized * 1.9f, dt * 6);
                    p.pos += p.vel * dt; p.anim += dt * 7; p.moving = 1;
                    if (Mathf.Abs(dx) < .75f && p.pos.y >= FloorTop - .08f) { Destroy(p.rig.go); crowd.RemoveAt(i); bubbles.RemoveAll(b => b.who == p); }
                    continue;
                }
            }
            foreach (var s in spills)
                if (Mathf.Abs(p.pos.x - s.pos.x) < s.r * 1.7f && Mathf.Abs(p.pos.y - s.pos.y) < .6f) { p.stun = Mathf.Max(p.stun, .3f); p.vel.x += Mathf.Sin(Time.time * 5 + p.phase) * dt * 6; }
            Vector2 mv = Vector2.zero;
            Vector2 to = pPos - p.pos; float dist = to.magnitude;
            if (p.stun <= 0 && p.fall <= 0 && dist < 11f && !(phase == Phase.Exit))
            {
                float sp = p.def.speed * (p.isBoss ? 1f : .7f) * (p.angry > 0 ? 1.4f : 1f) * (p.slow > 0 ? .4f : 1f);
                mv = to.normalized * sp;
                // 떨어진 인형이 가까우면 그쪽으로 몰린다
                if (!p.isBoss) foreach (var l in lures) { var tl = l.pos - p.pos; if (tl.magnitude < 3.6f) { mv = tl.normalized * Mathf.Max(sp, .9f); break; } }
                if (p.def.drunk) mv += new Vector2(Mathf.Sin(Time.time * 2 + p.phase), Mathf.Cos(Time.time * 3 + p.phase)) * .9f;
                if (dist < p.Radius + PlayerR + .18f && p.attackT <= 0) { p.attackT = Random.Range(1.1f, 1.7f); Hurt(p.def.power * (1 + stageIndex * .05f) * HardHit, p); }
            }
            p.vel = Vector2.Lerp(p.vel, Vector2.zero, dt * 5);
            p.pos += (mv + p.vel) * dt;
            p.anim += dt * (3 + mv.magnitude * 6); p.moving = Mathf.MoveTowards(p.moving, mv.sqrMagnitude > .01f ? 1 : 0, dt * 6);
        }
        // 밀치락달치락: 서로 겹치면 무거운 쪽이 덜 밀린다
        pressLoad = 0;
        bool pushing = Mathf.Abs(k.mx) > .1f && !ringing && onCallT <= 0 && !arrived;
        for (int i = 0; i < crowd.Count; i++)
        {
            var a = crowd[i]; if (a.dying) continue;
            for (int j = i + 1; j < crowd.Count; j++)
            {
                var b = crowd[j]; if (b.dying) continue;
                Vector2 d = a.pos - b.pos; float min = (a.Radius + b.Radius) * .62f, m = d.magnitude;
                if (m >= min) continue;
                if (m < .001f) { d = new Vector2(Random.value - .5f, Random.value - .5f); m = d.magnitude; }
                float over = (min - m) * .5f, wa = b.def.weight / (a.def.weight + b.def.weight);
                a.pos += d / m * over * wa * 2; b.pos -= d / m * over * (1 - wa) * 2;
                a.press = Mathf.Max(a.press, over * 5); b.press = Mathf.Max(b.press, over * 5);
            }
            Vector2 dp = a.pos - pPos; float mp = dp.magnitude, minP = a.Radius + PlayerR;
            if (mp < minP && dashT <= 0 && phase != Phase.Exit && !a.exiting)
            {
                if (mp < .001f) { dp = Vector2.right; mp = 1; }
                float over = minP - mp, pw = pushing ? 7f : 1.3f, wp = pw / (pw + a.def.weight);
                a.press = 1;
                if (pushing && dp.x * k.mx > 0) pressLoad += a.def.weight;
                // 앞사람은 옆으로 비켜나 땅굴이 생긴다
                if (pushing) a.pos.y += (dp.y >= 0 ? 1 : -1) * dt * 2.4f;
                a.pos += dp / mp * over * wp; if (!arrived) pPos -= dp / mp * over * (1 - wp);
            }
        }
        foreach (var p in crowd)
        {
            if (p.dying) continue;
            p.pos.y = Mathf.Clamp(p.pos.y, FloorBottom, FloorTop);
            p.pos.x = Mathf.Clamp(p.pos.x, -7, targetX + 9);
            p.press = Mathf.MoveTowards(p.press, 0, dt * 3);
            float q = Mathf.Clamp01(p.press);
            p.rig.root.localScale = new Vector3(p.size * (1 + q * .2f), p.size * (1 - q * .16f), 1);
            float pLean = p.fall > 0 ? 72 * pFacingOf(p) * p.fallDir : p.hitLean > 0 ? -p.hitLean : (p.stun > 0 ? Mathf.Sin(p.phase * 3) * 5 : 0);
            float pArm = p.flail > 0 ? Mathf.Sin(p.phase * 22) * 130 : p.posing > 0 ? 170 : p.def.look == "phone" || p.def.look == "makeup" ? 50 : 0;
            if (p.flail > 0 && Random.value < .03f) Float("앗따가!", p.pos + new Vector2(0, 2.1f), Color.white);
            p.rig.Pose(p.bowl > 0 ? p.pos + new Vector2(0, .75f) : p.pos, pFacingOf(p), p.anim, p.moving, pLean, pArm, p.phase);
            p.rig.root.rotation = p.bowl > 0 ? Quaternion.Euler(0, 0, -p.bowlDir * p.bowl * 1100) : Quaternion.identity;
            p.rig.SetOrder(Order(p.pos.y));
            p.rig.SetFace(p.hurtT > 0 ? Art.Face.Hurt : (p.stun > 0 ? Art.Face.Dizzy : Art.Face.Normal));
            p.rig.SetColor(p.flash > 0 ? new Color(1, .72f, .72f) : (p.slow > 0 ? new Color(.82f, .72f, 1f) : p.stun > 0 && p.posing <= 0 && p.flail <= 0 && p.fall <= 0 ? new Color(.55f, .78f, 1f) : Color.white));
        }
    }

    static int Order(float y) { return 5000 - (int)(y * 1000); }

    void TickFx(float dt)
    {
        for (int i = fxs.Count - 1; i >= 0; i--)
        {
            var f = fxs[i]; f.t += dt; float u = f.t / f.dur;
            if (!f.beam) f.sr.transform.localScale = new Vector3(f.flip, 1, 1) * Mathf.Lerp(.2f, f.size, Mathf.Sqrt(u));
            var c = f.c; c.a = 1 - u; f.sr.color = c;
            if (u >= 1) { Destroy(f.sr.gameObject); fxs.RemoveAt(i); }
        }
        for (int i = spills.Count - 1; i >= 0; i--)
        {
            var s = spills[i]; s.life -= dt;
            if (s.life <= 0) { Destroy(s.go); spills.RemoveAt(i); }
        }
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var s = shots[i]; s.life -= dt; s.pos.x += s.dir * s.speed * dt; s.pos.y = Mathf.Clamp(s.pos.y + s.vy * dt, FloorBottom, FloorTop);
            s.go.transform.position = s.pos + new Vector2(0, 1.1f); if (!s.bead) s.go.transform.Rotate(0, 0, 900 * dt);
            foreach (var p in crowd)
                if (!p.dying && !s.hit.Contains(p) && Mathf.Abs(p.pos.x - s.pos.x) < p.Radius + .2f && Mathf.Abs(p.pos.y - s.pos.y) < .5f)
                {
                    s.hit.Add(p); Damage(p, s.dmg, s.knock * s.dir, s.bead ? null : s.hit.Count == 1 ? "슉!" : null);
                    Sfx.Hit("thudL", s.bead ? .18f : .28f, .2f);
                    if (s.bead) { s.life = 0; break; }
                    if (s.bomb)
                    {
                        AddRing(p.pos + new Vector2(0, 1), 1.9f, new Color(1, .6f, .2f));
                        foreach (var o in crowd)
                            if (!o.dying && o != p && (o.pos - p.pos).magnitude < 1.9f) Damage(o, s.dmg * .6f, s.knock * Mathf.Sign(o.pos.x - p.pos.x), null);
                        s.life = 0; Sfx.Play("쾅!", .6f); hitStop = Mathf.Max(hitStop, .07f);
                        if ((pPos - p.pos).magnitude < 2.2f) { pVel.x += Mathf.Sign(pPos.x - p.pos.x) * 3; pFlash = .15f; }
                        break;
                    }
                    if (s.read) { p.stun = Mathf.Max(p.stun, p.isBoss ? .2f : .7f); if (Random.value < .25f) Speak(p, "이런 사람이셨군요", 1f); }
                    if (!s.bomb && s.hit.Count >= 2) { s.life = 0; break; }
                }
            if (s.life <= 0) { Destroy(s.go); shots.RemoveAt(i); }
        }
        for (int i = bubbles.Count - 1; i >= 0; i--) { bubbles[i].ttl -= dt; if (bubbles[i].ttl <= 0) bubbles.RemoveAt(i); }
        for (int i = floaters.Count - 1; i >= 0; i--) { var f = floaters[i]; f.ttl -= dt; f.pos.y += dt * 1.2f; if (f.ttl <= 0) floaters.RemoveAt(i); }
    }

    void DrawWorld(float dt)
    {
        var w = weapons[Mathf.Clamp(curWeapon, 0, weapons.Count - 1)];
        bool back = w.kind == "back" && !frontpack;
        float armAngle = arrived ? 175 : onCallT > 0 ? 140 : (ringing ? 70 : 25);
        bool shoving = pressLoad > 0;
        float lean = shoving ? 15 : 0;
        if (atkAnim <= 0 && shoving && !ringing && onCallT <= 0) armAngle = 95;
        int shown = facing;
        wSr.sprite = Art.Icon(w.id); wSr.sortingOrder = Order(pPos.y) + 3008;
        if (phase != Phase.Exit) wSr.color = ringing || onCallT > 0 ? Color.clear : Color.white;
        DrawWeaponMotion(w, ref armAngle, ref lean, ref shown);
        pushLineT -= dt;
        if (shoving && pushLineT <= 0) { pushLineT = Random.Range(5f, 9f); if (playerLineT <= 0 && !ringing && onCallT <= 0) Say(PushLines[Random.Range(0, PushLines.Length)], 1.6f); }
        if (phase != Phase.Exit)
        {
            pRig.Pose(pPos, shown, pAnim, pMoving, lean, armAngle, stageT * 2);
            if (pendingKick < 900) pRig.Kick(pendingKick);
            pRig.SetColor(pFlash > 0 ? new Color(1, .74f, .74f) : (selfStun > 0 ? new Color(.85f, .88f, 1f) : Color.white));
            pRig.SetFace(pFlash > 0 || sweepWarn > .3f ? Art.Face.Hurt : (selfStun > 0 || dizzy > 0 ? Art.Face.Dizzy : Art.Face.Normal));
        }
        else pRig.Pose(pPos, 1, pAnim, 1, 0, 0, stageT * 2);
        pRig.SetOrder(Order(pPos.y) + 3000);

        float cx = Mathf.Lerp(cam.transform.position.x, pPos.x + 1.2f, dt * 5);
        cam.transform.position = new Vector3(cx, 0, -10);
        DrawEnv(dt);

        if (portal != null)
        {
            float pu = Mathf.Sin(Time.time * 4) * .04f;
            portal.localScale = new Vector3(1 + pu, 1 - pu, 1);
            portal.gameObject.SetActive(!lobby || bossDown);
        }
        if (arrow != null)
        {
            arrow.position = new Vector2(targetX, 2.05f + Mathf.Sin(Time.time * 5) * .1f);
            arrow.gameObject.SetActive((!lobby || bossDown) && !practice);
        }
    }

    // 창밖, 역, 문, 손잡이: 플레이어가 없어도 움직인다
    void DrawEnv(float dt)
    {
        float cx = cam.transform.position.x;
        if (vignette != null) vignette.position = new Vector3(cx, 0, 0);
        if (lobby) { Sfx.Hum(.01f); return; }
        foreach (var l in layers)
        {
            l.off += l.speed * trainSpeed * dt;
            float span = l.t.Length * l.tw;
            for (int i = 0; i < l.t.Length; i++)
            {
                float rel = Mathf.Repeat(i * l.tw - l.off, Mathf.Max(span, 24f));
                var p = l.t[i].position; p.x = cx - 12 + rel; l.t[i].position = p;
            }
        }
        if (stationRoot != null)
        {
            bool on = passT > 0 || phase == Phase.Brake || phase == Phase.Open || phase == Phase.Exit || phase == Phase.Leave;
            stationRoot.gameObject.SetActive(on);
            float sx = 0;
            if (phase == Phase.Brake) { float u = 1 - Mathf.Clamp01(phaseT / 3f); sx = 48 * (1 - u) * (1 - u); }
            else if (phase == Phase.Leave) { float u = 1 - Mathf.Clamp01(phaseT / 1.1f); sx = -48 * u * u; }
            if (passT > 0) sx = Mathf.Lerp(-48, 48, passT / 1.5f);
            stationRoot.position = new Vector3(sx, 0, 0);
        }
        for (int i = 0; i < doors.Count; i++)
        {
            float x = (i - 3) * DoorGap, o = doorOpen * .56f + .28f;
            doors[i][0].position = new Vector2(x - o, -0.7f); doors[i][1].position = new Vector2(x + o, -0.7f);
        }
        bool warn = !arrived && (announced && phase == Phase.Ride || phase == Phase.Brake || phase == Phase.Open && (stopType == 1 ? openT < 2.5f : openT < ExitWave));
        float za = warn ? .34f + Mathf.Sin(Time.time * 7) * .14f : 0;
        var zc = new Color(1, .12f, .12f, za);
        foreach (var z in zones) z.color = zc;
        float sw = Mathf.Sin(Time.time * 2.2f) * (2 + trainSpeed * 4) + (phase == Phase.Brake ? 12 : 0);
        foreach (var s in straps) s.rotation = Quaternion.Euler(0, 0, sw);
        foreach (var f in fore)
        {
            float rel = Mathf.Repeat(f.x - cx * 1.5f, 20.8f) - 10.4f;
            f.t.position = new Vector3(cx + rel, 3.2f, 0); f.t.rotation = Quaternion.Euler(0, 0, sw * 1.2f);
        }
        Sfx.Hum(.02f + trainSpeed * .05f);
    }
}
