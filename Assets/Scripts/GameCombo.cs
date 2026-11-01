using System.Collections.Generic;
using UnityEngine;

// 숨은 조합: 무기 + 방해. 고를 때 힌트 없이, 써 보면 이름이 뜬다
public partial class Game
{
    class ComboDef { public string weapon, skill, name, desc; public ComboDef(string w, string s, string n, string d) { weapon = w; skill = s; name = n; desc = d; } }

    static readonly ComboDef[] Combos =
    {
        new ComboDef("heel", "perfume", "도도한 출근", "넋 나가서 피함"),
        new ComboDef("tumbler", "coffee", "리필이요", "큰 웅덩이 · 웅덩이 위 피해 +50%"),
        new ComboDef("beadbag", "speaker", "비즈 댄스", "비즈 2배 · 안 화남"),
        new ComboDef("keyring", "cough", "파상풍 공포", "다들 허우적거림"),
        new ComboDef("keybag", "cry", "인형 안고 엉엉", "인형 우르르"),
        new ComboDef("umbrella", "dash", "우산 펜싱", "찌르며 돌진 · 길 열림"),
        new ComboDef("selfie", "speaker", "틱톡 촬영", "더 오래 멈춤 · 안 화남"),
        new ComboDef("lanyard", "latte", "사원증 부심", "오래 느려짐"),
        new ComboDef("card", "phone", "네 대리님!", "통화 중 명함 더 뿌림"),
        new ComboDef("laptop", "garlic", "야근 회식", "노트북 빨라짐"),
        new ComboDef("suitcase", "seatcall", "자리 쟁탈전", "외치며 바로 돌진"),
        new ComboDef("hook", "badge", "배에 쇼핑백", "도착지 주변 튕김"),
    };

    // 조합을 잡으면 높은 단계도 해볼 만해진다
    const float ComboDmg = 1.3f, ComboTaken = .75f;
    float comboT; ComboDef comboSeen;

    static ComboDef ComboWith(string weapon, string skillId) { return System.Array.Find(Combos, c => c.weapon == weapon && c.skill == skillId); }
    static ComboDef PartnerOf(string weapon) { return System.Array.Find(Combos, c => c.weapon == weapon); }

    bool Has(string weapon, string skillId) { return skill != null && skill.id == skillId && weapons.Exists(w => w.id == weapon); }

    ComboDef ActiveCombo
    {
        get
        {
            if (skill == null) return null;
            foreach (var w in weapons) { var c = ComboWith(w.id, skill.id); if (c != null) return c; }
            return null;
        }
    }

    List<SkillDef> FirstSkills(WeaponDef w)
    {
        var list = Pick(SkillPool(), 3);
        // 시험용: 조합 봇에게만 짝을 보여 준다
        var c = botCombo ? PartnerOf(w.id) : null;
        if (c == null || list.Exists(s => s.id == c.skill)) return list;
        list[Random.Range(0, list.Count)] = GameData.Skills.Find(s => s.id == c.skill);
        return list;
    }

    void ComboOnSkill(float dur)
    {
        var c = ActiveCombo;
        if (c == null) return;
        comboT = dur * 1.5f; comboSeen = c;
        AddRing(pPos + new Vector2(0, 1), 2.2f, new Color(1, .82f, .25f));
        Float(c.name, pPos + new Vector2(0, 2.7f), new Color(1, .85f, .3f));
        if (floaters.Count > 0) floaters[floaters.Count - 1].big = true;
        if (c.skill == "cry") for (int i = 0; i < 3; i++) MakeLure();
        if (c.skill == "seatcall")
        {
            var w = weapons[Mathf.Clamp(curWeapon, 0, weapons.Count - 1)];
            if (w.kind == "ram") { atkCd = 0; Attack(w); }
        }
    }

    // 방해에 걸린 사람에게 조합 효과를 얹는다
    void ComboOnHit(Passenger p, float dur)
    {
        if (Has("heel", "perfume")) Daze(p, dur + 2.5f);
        else if (Has("keyring", "cough")) p.flail = Mathf.Max(p.flail, dur * .8f);
        else if (Has("lanyard", "latte")) p.slow = Mathf.Max(p.slow, dur + 5);
    }

    // 넋이 나가면 나를 피해 슬금슬금 물러난다. 부장은 안 넘어간다
    void Daze(Passenger p, float t)
    {
        if (p.isBoss) return;
        p.stun = Mathf.Min(p.stun, .3f); p.daze = Mathf.Max(p.daze, t);
        if (Random.value < .3f) Float("넋...", p.pos + new Vector2(0, 2.1f), new Color(1, .75f, .9f));
    }

    // 향수 뿌린 하이힐: 찍을 때마다 향이 번진다
    void HeelScent(Passenger from)
    {
        foreach (var o in crowd)
            if (!o.dying && o != from && (o.pos - from.pos).magnitude < 1.7f) Daze(o, 2.5f);
    }
}
