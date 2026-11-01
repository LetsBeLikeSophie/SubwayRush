using System.Collections.Generic;
using UnityEngine;

// 판을 넘어 남는 것: 직급(올라갈수록 어려움), 월급, 복지몰 강화, 무기 해금
public partial class Game
{
    static readonly string[] Ranks = { "인턴", "사원", "대리", "과장", "차장", "부장", "이사", "사장" };
    static readonly string[] Bosses = { "사원님", "대리님", "과장님", "차장님", "부장님", "이사님", "사장님", "회장님" };
    static readonly string[] StarterWeapons = { "backpack", "tumbler", "keyring", "umbrella", "card", "selfie", "lanyard", "keybag" };
    const int WeaponsPerRank = 1, StarterSkills = 6;

    int botRank; int earned; string promoted; readonly List<WeaponDef> unlockedNow = new List<WeaponDef>();

    int selected; bool perkPending;
    public int Cleared => botOn ? botRank : PlayerPrefs.GetInt("cleared", 0);
    public int Level => botOn ? Mathf.Min(botRank, GameData.Stages.Count - 1) : selected;
    StageDef CurStage => GameData.Stages[Level];
    // 난이도: 단계가 오를수록, 헬게이트면 크게 뛴다
    float Diff => Level + (CurStage.hell ? 4 : 0);
    public int Rank => Cleared / 2;
    public int Salary => PlayerPrefs.GetInt("salary", 0);
    string RankName => Ranks[Mathf.Min(Rank, Ranks.Length - 1)] + (Rank >= Ranks.Length ? " " + (Rank - Ranks.Length + 2) + "년차" : "");
    string PName => "김" + Ranks[Mathf.Min(Rank, Ranks.Length - 1)];
    string BossName => Bosses[Mathf.Min(Rank, Bosses.Length - 1)];
    int Lv(string id) { return botOn ? 0 : PlayerPrefs.GetInt("up_" + id, 0); }

    float HardHp => .65f + Diff * .1f;
    float HardHit => .8f + Diff * .06f;
    float HardCrowd => .85f + Diff * .09f + (CurStage.hell ? .2f : 0);

    List<WeaponDef> OrderedWeapons()
    {
        var starters = GameData.Weapons.FindAll(w => System.Array.IndexOf(StarterWeapons, w.id) >= 0);
        starters.AddRange(GameData.Weapons.FindAll(w => System.Array.IndexOf(StarterWeapons, w.id) < 0));
        return starters;
    }

    List<WeaponDef> WeaponPool() { var o = OrderedWeapons(); return o.GetRange(0, Mathf.Min(o.Count, StarterWeapons.Length + Rank * WeaponsPerRank)); }
    List<SkillDef> SkillPool() { return GameData.Skills.GetRange(0, Mathf.Min(GameData.Skills.Count, StarterSkills + Rank)); }

    // 판이 끝나면 월급을 받고, 성공했으면 승진해서 새 무기가 풀린다
    void EndRun(bool won)
    {
        earned = Mathf.RoundToInt((kills / 6f + stageIndex * 25 + (won ? 80 : 0) + expressCount * 40) * (1 + Rank * .2f));
        promoted = null; unlockedNow.Clear();
        if (botOn) return;
        PlayerPrefs.SetInt("salary", Salary + earned);
        if (won)
        {
            PlayerPrefs.SetString("rec_" + Level, string.Join(",", weapons.ConvertAll(x => x.id)) + (skill != null ? "," + skill.id : ""));
            if (Level == Cleared)
            {
                var before = WeaponPool();
                string old = RankName; int oldRank = Rank;
                PlayerPrefs.SetInt("cleared", Cleared + 1);
                perkPending = true;
                if (Rank != oldRank) promoted = old + " → " + RankName;
                foreach (var w in WeaponPool()) if (!before.Contains(w)) unlockedNow.Add(w);
            }
        }
        PlayerPrefs.Save();
    }

    // 처음 깬 역마다 고르는 영구 강화. 다음 단계를 버틸 힘이 된다
    static readonly string[][] Perks =
    {
        new[]{"vit","퇴근 후 숙면","최대 멘탈 +5","cup"}, new[]{"gym","주말 헬스","위력 +3%","gear"}, new[]{"run","편한 구두","이속 +2%","dash"},
        new[]{"push","어깨 넓어짐","밀치기 +4%","backpack"}, new[]{"guard","무뎌진 마음","받는 피해 -3%","badge"}, new[]{"cool","손이 빨라짐","쿨타임 -2%","tumbler"},
    };

    public void AfterEnd()
    {
        if (!perkPending) { OpenMap(); return; }
        perkPending = false;
        var picks = Pick(new List<string[]>(Perks), 3);
        ShowChoices("이번 역에서 얻은 것", "", picks.ConvertAll(x => new Choice
        {
            icon = x[3], title = x[1], sub = x[2], tint = new Color(1, .95f, .75f),
            act = () => { PlayerPrefs.SetInt("up_" + x[0], Lv(x[0]) + 1); PlayerPrefs.Save(); OpenMap(); }
        }));
    }

    public void OpenMap() { practice = false; state = St.Map; selected = Mathf.Min(Cleared, GameData.Stages.Count - 1); }

    public void PlayStage(int i) { selected = i; StartRun(); }

    // 임산부 배지: 분홍 자리 근처에서 쓰면 다음 분홍 자리로 순간이동
    bool NearSeat(out float seatX)
    {
        seatX = 0; float best = 1.7f; bool found = false;
        if (lobby || platform) return false; // 회사 로비와 승강장엔 분홍 자리가 없다
        int d = Mathf.RoundToInt(pPos.x / DoorGap);
        for (int i = d - 1; i <= d + 1; i++)
            foreach (float side in new[] { -.87f, .87f })
            {
                float x = i * DoorGap + side, gap = Mathf.Abs(x - pPos.x);
                if (gap < best) { best = gap; seatX = x; found = true; }
            }
        return found;
    }

    bool UseBadge()
    {
        if (!NearSeat(out float x)) { Sfx.Play("ouch", .4f); Float(lobby || platform ? "자리가 없다" : "분홍 자리!", pPos + new Vector2(0, 2.3f), new Color(1, .6f, .8f)); return false; }
        float nx = Mathf.Min(x + DoorGap, targetX + .87f);
        AddRing(pPos + new Vector2(0, 1), 1.4f, new Color(1, .55f, .75f));
        pPos = new Vector2(nx, FloorTop); pVel = Vector2.zero;
        AddRing(pPos + new Vector2(0, 1), 2.4f, new Color(1, .55f, .75f));
        foreach (var p in crowd)
            if (!p.dying && (p.pos - pPos).magnitude < (Has("hook", "badge") ? 4f : 2.6f)) { p.stun = Mathf.Max(p.stun, p.isBoss ? 1 : Has("hook", "badge") ? 3.5f : 2.2f); p.vel += (p.pos - pPos).normalized * (Has("hook", "badge") ? 6 : 3); }
        Say(skill.line, 1.8f); Sfx.Play("skill", .55f); ComboOnSkill(1);
        skillCd = SkillCdMax;
        return true;
    }

    public void OpenShop()
    {
        var list = new List<Choice>();
        list.Add(ShopItem("vit", "영양제", "최대 멘탈 +5", "cup"));
        list.Add(ShopItem("gym", "헬스 PT", "위력 +3%", "gear"));
        list.Add(ShopItem("run", "편한 구두", "이속 +2%", "dash"));
        list.Add(new Choice { icon = "backpack", title = "출근", sub = "", act = OpenMap });
        ShowChoices("복지몰 · 월급 " + Salary, "", list);
    }

    Choice ShopItem(string id, string name, string effect, string icon)
    {
        int cost = 100 * (Lv(id) + 1);
        bool can = Salary >= cost;
        return new Choice
        {
            icon = icon, title = name + " Lv." + Lv(id), sub = effect + "\n" + cost, tint = can ? new Color(1, .95f, .8f) : new Color(.6f, .6f, .62f),
            act = () =>
            {
                if (can) { PlayerPrefs.SetInt("salary", Salary - cost); PlayerPrefs.SetInt("up_" + id, Lv(id) + 1); PlayerPrefs.Save(); }
                else Sfx.Play("ouch");
                OpenShop();
            }
        };
    }
}
