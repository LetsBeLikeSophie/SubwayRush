using UnityEngine;

// 사람 한 명: 머리·몸·팔·다리를 따로 움직인다
public class Rig
{
    public GameObject go; public Transform root;
    readonly string look;
    readonly Transform body, head, armF, armB, legF, legB, shadow;
    readonly SpriteRenderer headR;
    readonly SpriteRenderer[] parts;
    readonly int[] orders;
    Art.Face face = Art.Face.Normal;
    readonly float wide;

    public Rig(string look, Transform parent, int outfit = 0, int tint = 0)
    {
        this.look = look;
        wide = (look == "spread" || look == "boss" ? 1.3f : 1f) * (outfit == Weather.Padding ? 1.15f : 1);
        go = new GameObject(look); root = go.transform; root.SetParent(parent);
        var sh = new GameObject("shadow"); shadow = sh.transform; shadow.SetParent(root, false); shadow.localPosition = new Vector3(0, .02f, 0); shadow.localScale = new Vector3(1.1f * wide, 1f, 1);
        var sr = sh.AddComponent<SpriteRenderer>(); sr.sprite = Art.Shadow(); sr.sortingOrder = -600;

        body = new GameObject("body").transform; body.SetParent(root, false); body.localPosition = new Vector3(0, .68f, 0);
        var back = Art.BackPart(look);
        SpriteRenderer backR = null;
        if (back != null) { backR = Part("back", back, body, new Vector3(look == "bundle" ? -.28f : -.26f, look == "bundle" ? .05f : .02f, 0)); }
        var aB = Part("armB", Art.Arm(look, false, outfit, tint), body, new Vector3(-.26f * wide, .68f, 0)); armB = aB.transform;
        var lB = Part("legB", Art.Leg(look), root, new Vector3(-.13f * wide, .74f, 0)); legB = lB.transform;
        var lF = Part("legF", Art.Leg(look), root, new Vector3(.13f * wide, .74f, 0)); legF = lF.transform;
        var t = Part("torso", Art.Torso(look, outfit, tint), body, Vector3.zero);
        headR = Part("head", Art.Head(look, face), body, new Vector3(.02f, .66f, 0)); head = headR.transform;
        var aF = Part("armF", Art.Arm(look, true, outfit, tint), body, new Vector3(.27f * wide, .68f, 0)); armF = aF.transform;
        var front = Art.FrontPart(look);
        SpriteRenderer frontR = null;
        if (front != null) { frontR = Part("front", front, body, new Vector3(.12f, .36f, 0)); }
        parts = new[] { backR, aB, lB, lF, t, aF, headR, frontR };
        orders = new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
    }

    SpriteRenderer Part(string n, Sprite s, Transform parent, Vector3 pos)
    {
        var g = new GameObject(n); g.transform.SetParent(parent, false); g.transform.localPosition = pos;
        var r = g.AddComponent<SpriteRenderer>(); r.sprite = s;
        return r;
    }

    public void SetOrder(int b) { for (int i = 0; i < parts.Length; i++) if (parts[i] != null) parts[i].sortingOrder = b + orders[i]; }
    public void SetColor(Color c) { foreach (var p in parts) if (p != null) p.color = c; }
    public void SetFace(Art.Face f) { if (f == face) return; face = f; headR.sprite = Art.Head(look, f); }
    public void Kick(float a) { legF.localRotation = Quaternion.Euler(0, 0, a); }
    public void HideShadow() { shadow.gameObject.SetActive(false); }

    // walk: 걸음 진행값, moving: 0~1 걷는 정도, lean: 몸 기울기(도), arm: 앞팔 들기(도)
    public void Pose(Vector2 pos, int facing, float walk, float moving, float lean, float arm, float idlePhase)
    {
        root.position = pos;
        var s = root.localScale; root.localScale = new Vector3(Mathf.Abs(s.x) * (facing < 0 ? -1 : 1), s.y, 1);
        float sw = Mathf.Sin(walk) * moving;
        legF.localRotation = Quaternion.Euler(0, 0, sw * 30); legB.localRotation = Quaternion.Euler(0, 0, -sw * 30);
        armB.localRotation = Quaternion.Euler(0, 0, sw * 24);
        armF.localRotation = Quaternion.Euler(0, 0, -sw * 24 + arm);
        float breathe = Mathf.Sin(idlePhase) * .012f;
        float bob = Mathf.Abs(Mathf.Cos(walk)) * .05f * moving + breathe;
        body.localPosition = new Vector3(0, .68f + bob, 0);
        body.localRotation = Quaternion.Euler(0, 0, -lean + (moving > .1f ? -3 : 0));
        head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(idlePhase * .7f) * 2 + lean * .3f);
    }
}
