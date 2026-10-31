using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class WeaponDef { public string id, name, kind, hit, desc; public float damage, range, depth, cooldown, knock; }
public class SkillDef { public string id, name, line, desc; public float radius, duration, cooldown; }
public class ItemDef { public string id, name, icon, desc; public float speed, damage, cooldown, knock, maxMental, taken, heal; public int grade; }

public class StageDef { public string name; public bool hell; }

public class PassengerDef
{
    public string look, name; public float hp, speed, radius, weight, power; public string[] quips; public bool drunk, latch, heal, smell, small;
    public PassengerDef(string look, string name, float hp, float speed, float radius, float weight, float power, params string[] quips)
    { this.look = look; this.name = name; this.hp = hp; this.speed = speed; this.radius = radius; this.weight = weight; this.power = power; this.quips = quips; }
}

public class LineDef
{
    public string name; public Color32 color; public string[] stations;
    public LineDef(string name, Color32 color, params string[] stations) { this.name = name; this.color = color; this.stations = stations; }
}

// 무기·방해·아이템은 Resources 폴더의 csv 표에서 읽는다. 표에 줄을 추가하면 게임에 나온다.
public static class GameData
{
    public static List<WeaponDef> Weapons = new List<WeaponDef>();
    public static List<SkillDef> Skills = new List<SkillDef>();
    public static List<ItemDef> Items = new List<ItemDef>();
    public static List<StageDef> Stages = new List<StageDef>();

    public static readonly PassengerDef[] Passengers =
    {
        new PassengerDef("phone", "폰좀비 직장인", 26, 0.75f, 0.36f, 1f, 3, "(카톡 중)", "아 잠깐만요", "...", "저기요 눈 좀"),
        new PassengerDef("school", "등교하는 고등학생", 20, 1.25f, 0.34f, 0.8f, 3, "아 지각이라고요!", "헐", "쌤한테 이를 거예요", "ㅋㅋㅋ 뭐야"),
        new PassengerDef("hike", "등산복 어르신", 48, 0.95f, 0.4f, 1.5f, 6, "젊은 사람이 말이야!", "내가 왕년에!", "관악산 가는 길이야", "어허!"),
        new PassengerDef("bundle", "보따리 할머니", 60, 0.6f, 0.5f, 2.6f, 6, "아이고 내 허리야!", "이거 다 나물이여", "총각 좀 비켜", "아이고오"),
        new PassengerDef("spread", "쩍벌 아저씨", 55, 0.55f, 0.56f, 2f, 4, "내 자리인데?", "좁으면 택시 타", "흠흠", "어딜 밀어!"),
        new PassengerDef("makeup", "화장하는 언니", 24, 0.9f, 0.36f, 0.9f, 5, "아이라인 삐졌잖아요!", "아 진짜!", "지금 그리는 중이라고요", "뭐예요?"),
        new PassengerDef("box", "택배 어르신", 44, 0.7f, 0.5f, 1.8f, 5, "파손 주의야!", "이거 오늘 도착이여", "길 좀 터줘", "조심해!"),
        new PassengerDef("sleeper", "서서 자는 분", 40, 0f, .42f, 4f, 1, "(쿨...)", "(드르렁)", "아... 내릴 때..."),
        new PassengerDef("trainer", "헬스 트레이너", 60, .8f, .5f, 3.6f, 5, "자세 좋으시네요!", "한 세트 더!", "등 펴세요!", "PT 받으세요"),
        new PassengerDef("kid", "초등학생", 12, 1.7f, .26f, .5f, 2, "와아아아!", "아저씨 뭐예요?", "엄마한테 이를 거야", "메롱") { small = true },
        new PassengerDef("ajumma", "아주머니 부대", 42, .9f, .44f, 1.6f, 5, "여기 자리 맡았어!", "언니 이쪽이야!", "어머 어머", "좀 들어가요!"),
        new PassengerDef("tourist", "관광객", 30, .4f, .44f, 1.4f, 2, "Excuse me?", "Myeongdong?", "Sorry sorry", "Wow so busy"),
        new PassengerDef("dancer", "헤드폰 춤꾼", 26, .9f, .36f, 1f, 3, "(둠칫둠칫)", "(흥얼흥얼)", "뭐라고요?") { drunk = true },
        new PassengerDef("gimbap", "김밥 먹는 분", 28, .75f, .38f, 1f, 3, "내 김밥!", "(우물우물)", "참치 김밥이에요") { smell = true },
        new PassengerDef("tteok", "떡 돌리는 분", 24, .8f, .38f, 1f, 1, "떡 드세요!", "개업했어요~", "이것 좀 드셔봐") { heal = true },
        new PassengerDef("drunk", "아직 안 깬 분", 34, 0.85f, 0.4f, 1.2f, 4, "한 잔 더어...", "여기가 어디여", "집에 가는 중인데...", "형이 사랑한다") { drunk = true },
        new PassengerDef("survey", "도를 아는 분", 30, 1.05f, 0.36f, 1f, 2, "인상이 참 좋으시네요", "잠깐 설문 하나만요", "조상님이 보고 계세요", "기운이 맑으세요") { latch = true },
    };
    public static readonly PassengerDef Boss = new PassengerDef("boss", "보스", 220, 0.95f, 0.75f, 7f, 10, "자네, 잠깐 얘기 좀 하지", "요즘 애들은 말이야", "내가 신입 때는 말이야", "주말에 뭐 해?", "이번 보고서 말인데");
    public static readonly PassengerDef Coworker = new PassengerDef("phone", "옆 팀 동료", 36, 1f, 0.36f, 1f, 4, "커피 한잔 할까요?", "그 얘기 들었어요?", "오늘 회식이래요");

    public static readonly LineDef[] Lines =
    {
        new LineDef("1호선", new Color32(38, 60, 150, 255), "온수", "구로", "신도림", "영등포", "노량진", "용산", "서울역", "시청", "종각", "종로3가"),
        new LineDef("2호선", new Color32(46, 160, 70, 255), "신림", "서울대입구", "낙성대", "사당", "방배", "서초", "교대", "강남", "역삼", "선릉", "삼성"),
        new LineDef("3호선", new Color32(232, 120, 30, 255), "대화", "연신내", "독립문", "경복궁", "안국", "종로3가", "충무로", "약수", "옥수", "압구정", "고속터미널"),
        new LineDef("4호선", new Color32(50, 150, 215, 255), "노원", "창동", "혜화", "동대문", "명동", "회현", "숙대입구", "삼각지", "이촌", "동작", "사당"),
        new LineDef("5호선", new Color32(135, 75, 190, 255), "까치산", "목동", "여의도", "마포", "공덕", "충정로", "광화문", "을지로4가", "왕십리", "천호"),
        new LineDef("7호선", new Color32(110, 125, 40, 255), "가산디지털단지", "대림", "신풍", "장승배기", "이수", "고속터미널", "논현", "학동", "강남구청", "건대입구"),
        new LineDef("9호선", new Color32(190, 150, 60, 255), "김포공항", "가양", "염창", "당산", "여의도", "노량진", "동작", "고속터미널", "신논현", "선정릉"),
        new LineDef("신분당선", new Color32(200, 30, 60, 255), "광교", "판교", "청계산입구", "양재", "강남", "신논현"),
    };

    public static readonly string[] Ads = { "임플란트\n1+1", "모발이식\n상담 무료", "영어회화\n3개월 완성", "쌍꺼풀\n강남역 3번", "대출 비교\n1분 조회", "치킨 앱\n첫 주문", "코인은\n이제 그만", "퇴사학교\n수강생 모집", "로또\n1등 배출점", "다이어트\n한약" };
    public static readonly string[] DeadQuips = { "아 진짜!", "민원 넣을 거예요!", "다음 칸으로 갈게요...", "뭐 저런 사람이!", "오늘 일진 왜 이래", "커뮤니티에 올린다!", "내 출근길...", "안내센터 어디야!" };

    static bool loaded;
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        foreach (var c in Rows("weapons"))
            Weapons.Add(new WeaponDef { id = c[0], name = c[1], kind = c[2], damage = F(c[3]), range = F(c[4]), depth = F(c[5]), cooldown = F(c[6]), knock = F(c[7]), hit = c[8], desc = c[9] });
        foreach (var c in Rows("skills"))
            Skills.Add(new SkillDef { id = c[0], name = c[1], radius = F(c[2]), duration = F(c[3]), cooldown = F(c[4]), line = c[5], desc = c[6] });
        foreach (var c in Rows("stages")) Stages.Add(new StageDef { name = c[0], hell = c[1] == "1" });
        foreach (var c in Rows("items"))
            Items.Add(new ItemDef { id = c[0], name = c[1], icon = c[2], speed = F(c[3]), damage = F(c[4]), cooldown = F(c[5]), knock = F(c[6]), maxMental = F(c[7]), taken = F(c[8]), heal = F(c[9]), desc = c[10], grade = (int)F(c[11]) });
        foreach (var it in Items) if (it.icon == "gear") it.icon = IconFor(it);
    }

    // 아이템 그림은 가장 크게 바뀌는 능력치로 정한다
    static string IconFor(ItemDef i)
    {
        string best = "i_heart"; float top = Mathf.Abs(i.maxMental) / 40f + Mathf.Abs(i.heal) / 40f;
        void Try(float v, string id) { if (Mathf.Abs(v) > top) { top = Mathf.Abs(v); best = id; } }
        Try(i.damage, "i_power"); Try(i.speed, "dash"); Try(i.taken, "i_guard"); Try(i.cooldown, "i_cool"); Try(i.knock, "i_push");
        return best;
    }

    static float F(string s) { float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v); return v; }

    static IEnumerable<string[]> Rows(string file)
    {
        var text = Resources.Load<TextAsset>(file).text.TrimStart('﻿').Replace("\r", "");
        var lines = text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
            if (lines[i].Trim().Length > 0) yield return Split(lines[i]);
    }

    // 엑셀이 저장하는 형식(따옴표로 감싼 칸)까지 읽는다
    static string[] Split(string line)
    {
        var cells = new List<string>(); var sb = new StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (q)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') q = false;
                else sb.Append(ch);
            }
            else if (ch == '"') q = true;
            else if (ch == ',') { cells.Add(sb.ToString()); sb.Length = 0; }
            else sb.Append(ch);
        }
        cells.Add(sb.ToString());
        return cells.ToArray();
    }
}
