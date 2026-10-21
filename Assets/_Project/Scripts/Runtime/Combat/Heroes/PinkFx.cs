using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Octopus
{
    /// <summary>
    /// Hiệu ứng chiêu của PINK bằng chính các mảnh cắt từ tấm dáng chiêu của người dùng (Tools/art/hero_fx_pink.py →
    /// Resources/HeroFx/Pink). Mỗi chiêu chỉ dùng mảnh cắt từ CHÍNH tấm của nó, và mỗi hiệu ứng được tách thành nhiều lớp diễn
    /// riêng: lõi trắng rực (cộng sáng), thân màu, vệt đuôi, mảnh vỡ / đá, nửa trước / nửa sau quanh người (thứ tự vẽ: dải
    /// hình nhân vật -6 … 10; sau lưng &lt; -6, trước mặt &gt; 10). Hiệu ứng có nhiều pha trong tranh (vệt trăng nhỏ → lớn, vòng
    /// phép → vòng nghiêng → rồng, vòm pha 1 → pha 2) diễn nối tiếp với gốc ảnh chung nên đổi pha không bị giật.
    /// Neo: vệt chém / vòng phép bám mũi trượng thật (Func trả ctx.WeaponTip / CastPoint) trong lúc vung rồi mới trôi theo đà;
    /// hiệu ứng trên mặt đất đặt ở chân hoặc ngay dưới mũi trượng. Vật bay (rồng, tia pha lê) là "Bolt": đầu bay, thân kéo dài
    /// từ chỗ phóng tới đầu, trúng thì đầu + thân còn đọng lại một nhịp rồi tan (thấy rõ rồng trước / khi nổ).
    /// Nhẹ cho điện thoại: hiệu ứng nằm trong mảng struct cố định, SpriteRenderer / xích / vật bay dùng lại (không tạo / huỷ
    /// liên tục), không cấp phát bộ nhớ mỗi frame; xích vẽ kiểu Tiled — 6 renderer mỗi sợi. Tự tạo "Pink FX" khi dùng lần đầu.
    /// Cỡ: mọi hàm nhận size = tỉ lệ thêm lúc chạy nhân lên cỡ ảnh gốc (px / unit theo fx_manifest.json).
    /// </summary>
    public sealed class PinkFx : MonoBehaviour
    {
        const string Folder = "HeroFx/Pink/";

        /// <summary>Trước PINK (hình nhân vật người -6 … 10) và bia tập (2), sau tia lửa / vệt chém của CombatFx (14+).</summary>
        public const int FrontOrder = 12;
        /// <summary>Vụ nổ lớn: đè lên cả vệt chém.</summary>
        public const int TopOrder = 16;
        /// <summary>Sau lưng PINK (dải hiệu ứng sau lưng -17 … -10), trước sư tử (-39 … -22).</summary>
        public const int BackOrder = -11;
        /// <summary>Vòng phép nằm trên mặt đất: sau mọi thứ đứng trên nó.</summary>
        public const int GroundOrder = -16;

        const int MaxItems = 220;
        const int ChainSegments = 6; // mỗi sợi xích: 6 đoạn thẳng vẽ Tiled (đủ cong theo độ võng), không phải 40 mắt rời
        const int BoltLayers = 4;

        /// <summary>Tím pha lê chủ đạo của PINK.</summary>
        public static readonly Color Violet = new Color(0.78f, 0.42f, 1f);
        /// <summary>Tím sẫm (mép trong vệt chém).</summary>
        public static readonly Color Deep = new Color(0.42f, 0.12f, 0.85f);
        /// <summary>Trắng tím (lõi sáng, chữ cổ vòng phép).</summary>
        public static readonly Color Pale = new Color(0.93f, 0.82f, 1f);
        /// <summary>Đá tím xám (đá vụn văng).</summary>
        public static readonly Color Rock = new Color(0.36f, 0.28f, 0.5f);
        /// <summary>Bụi tím xám khi trượt dừng.</summary>
        public static readonly Color DustColor = new Color(0.62f, 0.52f, 0.72f, 0.6f);

        enum Ease { Linear, Out, OutBack }

        /// <summary>Một ảnh hiệu ứng đang diễn: hiện dần, to dần, giữ, tắt dần, bay / bám theo / bay quanh / quay quanh người.</summary>
        struct Item
        {
            public SpriteRenderer Renderer;
            public Vector2 Position, Velocity;
            public Func<Vector2> Follow;       // bám theo (mũi trượng, mục tiêu): vị trí = Follow() + Offset
            public float FollowTime;           // > 0: chỉ bám trong bấy nhiêu giây rồi bay tiếp theo Velocity từ chỗ đó
            public Vector2 Offset;
            public float Age, Delay, Life;
            public float Size, Facing;
            public Vector2 From, To, End;      // tỉ lệ x / y lúc đầu → sau Grow (phần đời) → lúc tắt hẳn
            public float Grow;
            public Ease Easing;
            public float FadeIn, Hold;         // hiện dần trong FadeIn (phần đời), giữ tới Hold rồi tắt dần
            public float Angle, Spin, Gravity, Drag;
            public Color Color;
            public int Order;
            public bool FlipY;                 // lật dọc (nửa sau của vòng = nửa trước lật lên, sau lưng)
            // bay quanh tâm Follow() theo elip (viên pha lê quanh người): trước người khi ở nửa dưới elip
            public bool Orbit;
            public float OrbitX, OrbitY, OrbitSpeed, OrbitPhase;
            // quay quanh trục dọc của người (đoạn vòng xoắn / vòng chữ cổ): bề ngang × cos, nửa quay ra trước thì vẽ trước người
            public bool Turn;
            public float TurnSpeed, TurnPhase;
            public int TurnBack;
            public Func<bool> Alive;           // trả false: tắt sớm (mục tiêu gục, chiêu bị ngắt)
            public bool Released;
        }

        /// <summary>Xích: vài đoạn Tiled nối đuôi nhau từ From() tới To() theo đường cong võng, phóng ra, rung rồi căng.</summary>
        sealed class Chain
        {
            public readonly SpriteRenderer[] Segs = new SpriteRenderer[ChainSegments];
            public Func<Vector2> From, To;
            public Func<bool> Alive;
            public float Age, Life, Phase, Sag, Size;
            public bool Released;
        }

        /// <summary>
        /// Vật bay (rồng, tia pha lê): tối đa 4 lớp cùng bay. Lớp "kéo dài" (thân, đuôi) giãn bề ngang từ chỗ phóng (Tail) tới
        /// đầu; lớp cố định (đầu, mũi) giữ cỡ. Mỗi frame hỏi Hit(đầu); trúng / hết tầm thì gọi Ended(đầu) rồi đọng lại Linger
        /// giây (thân thu dần về chỗ trúng) và tan.
        /// </summary>
        sealed class Bolt
        {
            public readonly SpriteRenderer[] Layers = new SpriteRenderer[BoltLayers];
            public readonly float[] Natural = new float[BoltLayers];   // bề ngang ảnh (unit, cỡ 1); > 0 = lớp kéo dài
            public readonly float[] MaxStretch = new float[BoltLayers];
            public readonly float[] Strength = new float[BoltLayers];
            public readonly float[] Back = new float[BoltLayers];      // gốc lớp lùi sau đầu bấy nhiêu unit (cỡ 1), như trong tranh
            public int Count;
            public Vector2 Position, Velocity, Tail;
            public float Size, Facing, Age, Range, Travelled, Linger, LingerAge;
            public Func<Vector2, bool> Hit;
            public Action<Vector2> Ended;
            public bool Done;
        }

        static PinkFx s_instance;
        static readonly Dictionary<string, Sprite> s_sprites = new Dictionary<string, Sprite>();
        static readonly List<SpriteRenderer> s_scratch = new List<SpriteRenderer>();
        static Material s_alpha, s_additive;
        static bool s_loaded;

        readonly Item[] _items = new Item[MaxItems];
        int _count;
        Item _spare;
        readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();
        readonly List<Chain> _chains = new List<Chain>();
        readonly Stack<Chain> _freeChains = new Stack<Chain>();
        readonly List<Bolt> _bolts = new List<Bolt>();
        readonly Stack<Bolt> _freeBolts = new Stack<Bolt>();
        Material _defaultMaterial;

        // Editor tắt domain reload: static còn giữ từ lần Play trước (ảnh / vật liệu vẫn dùng được, object thì đã huỷ)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        /// <summary>Bộ hiệu ứng của màn đang chơi (tạo lần đầu khi cần).</summary>
        public static PinkFx Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new GameObject("Pink FX").AddComponent<PinkFx>();
                return s_instance;
            }
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        /// <summary>Ảnh hiệu ứng tên name trong Resources/HeroFx/Pink (null nếu thiếu).</summary>
        public static Sprite Sprite(string name)
        {
            if (!s_sprites.TryGetValue(name, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(Folder + name);
                s_sprites[name] = sprite;
            }

            return sprite;
        }

        static void LoadMaterials()
        {
            if (s_loaded)
                return;
            s_loaded = true;
            s_alpha = Resources.Load<Material>(Folder + "PinkFxAlpha");
            s_additive = Resources.Load<Material>(Folder + "PinkFxAdditive");
        }

        /// <summary>
        /// Thứ tự vẽ "sau lưng mục tiêu": dưới mọi hình của mục tiêu (bia tập 2, sư tử -39 … -22) và dưới dải sau lưng PINK;
        /// nửa sau của xích quấn / lồng giam vẽ ở đây để mục tiêu nằm giữa hai nửa. Gọi một lần lúc ra chiêu.
        /// </summary>
        public static int BehindOf(Health target)
        {
            int order = BackOrder;
            if (target == null)
                return order;
            var lion = target.GetComponentInParent<LionBoss>();
            Transform root = lion != null ? lion.transform : target.transform;
            root.GetComponentsInChildren(false, s_scratch);
            foreach (SpriteRenderer r in s_scratch)
                order = Mathf.Min(order, r.sortingOrder - 1);
            s_scratch.Clear();
            return Mathf.Max(order, -900); // vẫn trước nền màn (-1000)
        }

        /// <summary>Số hiệu ứng ảnh đang diễn (kiểm thử).</summary>
        public int ActiveCount => _count;

        /// <summary>Số sợi xích đang hiện (kiểm thử).</summary>
        public int ActiveChains => _chains.Count;

        /// <summary>Số vật bay đang diễn (kiểm thử).</summary>
        public int ActiveBolts => _bolts.Count;

        /// <summary>Tổng SpriteRenderer đang dùng: ảnh + đoạn xích + lớp vật bay (kiểm thử: số renderer hợp lý).</summary>
        public int ActiveRenderers => _count + _chains.Count * ChainSegments + BoltRenderers();

        int BoltRenderers()
        {
            int n = 0;
            foreach (Bolt b in _bolts)
                n += b.Count;
            return n;
        }

        /// <summary>Chỗ (gốc ảnh, world) của ảnh tên name đang hiện đầu tiên (kiểm thử: vệt trăng có bám mũi trượng không).</summary>
        public bool TryPosition(string name, out Vector2 position)
        {
            Sprite sprite = Sprite(name);
            for (int i = 0; i < _count; i++)
            {
                if (sprite != null && _items[i].Renderer != null && _items[i].Renderer.sprite == sprite && _items[i].Age >= _items[i].Delay)
                {
                    position = _items[i].Renderer.transform.position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        /// <summary>Đang hiện ảnh tên name không (kiểm thử; tính cả lớp của vật bay).</summary>
        public bool IsShowing(string name)
        {
            Sprite sprite = Sprite(name);
            if (sprite == null)
                return false;
            for (int i = 0; i < _count; i++)
            {
                if (_items[i].Renderer != null && _items[i].Renderer.sprite == sprite && _items[i].Age >= _items[i].Delay)
                    return true;
            }

            foreach (Bolt b in _bolts)
            {
                for (int k = 0; k < b.Count; k++)
                {
                    if (b.Layers[k] != null && b.Layers[k].sprite == sprite)
                        return true;
                }
            }

            return false;
        }

        // =============================================================================================================
        // Nút 1 · Đánh thường — tấm "đánh thường": K3 vệt trăng nhỏ → K4 vệt trăng lớn (cùng gốc ở mũi trượng), K5 chớp sao
        // =============================================================================================================

        /// <summary>
        /// Vệt trăng tím ở mũi trượng: pha K3 (nhỏ) hiện lúc trượng sắp tới, pha K4 (lớn) + lõi trắng thay vào đúng lúc trúng
        /// (lead giây sau), bám mũi trượng thật (tip) thêm một nhịp rồi trôi tới và tan. strength 0.8 / 1: đòn 1 / đòn 2.
        /// </summary>
        public void Crescent(Func<Vector2> tip, float facing, float lead, float size = 1f, float strength = 1f)
        {
            float sz = size * strength;
            ref Item a = ref Spawn("crescent_small", tip(), facing, lead + 0.06f, FrontOrder);
            a.Follow = tip;
            a.Size = sz;
            a.From = new Vector2(0.6f, 0.75f);
            a.To = Vector2.one;
            a.End = new Vector2(1.25f, 1.12f); // phình ra đúng cỡ pha lớn lúc nhường chỗ
            a.Grow = 0.6f;
            a.Easing = Ease.Out;
            a.FadeIn = 0.25f;
            a.Hold = 0.55f;

            // pha lớn: bám mũi trượng 0.06 giây (trượng còn đâm tới) rồi trôi theo đà, tắt dần
            for (int i = 0; i < 2; i++)
            {
                bool core = i == 1;
                ref Item b = ref Spawn(core ? "crescent_core" : "crescent_big", tip(), facing, core ? 0.2f : 0.3f, core ? FrontOrder + 1 : FrontOrder, core);
                b.Follow = tip;
                b.FollowTime = 0.06f;
                b.Delay = lead;
                b.Size = sz;
                b.From = new Vector2(0.82f, 0.9f);
                b.To = Vector2.one;
                b.End = new Vector2(1.1f, 1.06f);
                b.Grow = 0.25f;
                b.Easing = Ease.Out;
                b.Hold = core ? 0.25f : 0.45f;
                b.Color = core ? new Color(1f, 1f, 1f, 0.8f) : Color.white;
                b.Velocity = new Vector2(facing * 4.5f * sz, 0f);
                b.Drag = 6f;
            }
        }

        /// <summary>
        /// Đòn đâm (K5): chớp sao ở mũi trượng (lõi, bám mũi trượng), cụm mảnh pha lê toả ra quanh (lớp riêng, nở + trôi tới
        /// thêm drift unit — tới tâm vùng đánh khi tầm ×2), vài mảnh pha lê rời văng xa.
        /// </summary>
        public void Sparkle(Func<Vector2> tip, float facing, float size = 1f, float drift = 0.5f)
        {
            Vector2 at = tip();
            ref Item s = ref Spawn("sparkle", at, facing, 0.32f, TopOrder, true);
            s.Follow = tip;
            s.FollowTime = 0.08f;
            s.Size = size;
            s.From = new Vector2(0.4f, 0.4f);
            s.To = Vector2.one;
            s.End = new Vector2(1.2f, 1.2f);
            s.Grow = 0.25f;
            s.Easing = Ease.OutBack;
            s.Hold = 0.35f;
            s.Spin = 90f;
            ref Item c = ref Spawn("sparkle_shards", at, facing, 0.45f, FrontOrder);
            c.Delay = 0.03f;
            c.Size = size;
            c.From = new Vector2(0.55f, 0.6f);
            c.To = Vector2.one;
            c.End = new Vector2(1.35f, 1.25f);
            c.Grow = 0.35f;
            c.Easing = Ease.Out;
            c.Hold = 0.4f;
            c.Velocity = new Vector2(facing * Mathf.Max(0.3f, drift) * 4f, 0f); // tắt dần theo Drag 4 → trôi ≈ drift unit
            c.Drag = 4f;
            Shards("shard", at, facing, 6, 5f, 0.45f, new Vector2(facing, 0.2f), size);
        }

        /// <summary>
        /// Mảnh pha lê (ảnh sprite — mỗi chiêu dùng mảnh cắt từ tấm của nó) văng ra từ at, xoay rồi rơi; bias = hướng ưu tiên.
        /// </summary>
        public void Shards(string sprite, Vector2 at, float facing, int count, float speed, float life, Vector2 bias = default, float size = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = (Random.insideUnitCircle.normalized + bias).normalized;
                ref Item p = ref Spawn(sprite, at + Random.insideUnitCircle * (0.15f * size), Random.value < 0.5f ? facing : -facing,
                    life * Random.Range(0.7f, 1.2f), FrontOrder + 1);
                p.Velocity = dir * speed * Random.Range(0.5f, 1.1f);
                p.Size = Random.Range(0.6f, 1.1f) * size;
                p.Gravity = 7f;
                p.Drag = 1.5f;
                p.Spin = Random.Range(-400f, 400f);
                p.Angle = Random.Range(0f, 360f);
                p.Hold = 0.6f;
            }
        }

        // =============================================================================================================
        // Nút 1 · ↑ Hất tung — K3 cung nhỏ → K4 cung lớn dựng đứng + lõi + pha lê toé dưới đất, K5 pha lê bay vút
        // =============================================================================================================

        /// <summary>
        /// Vệt trăng dựng đứng mọc từ mặt đất ngay dưới mũi trượng (root = x mũi trượng, y chân): cung nhỏ K3 thoáng qua rồi
        /// cung lớn K4 vươn lên theo trượng (từ dưới lên), lõi trắng cộng sáng, pha lê toé trên đất (trước chân).
        /// </summary>
        public void RiseSlash(Vector2 root, float facing, float size = 1f)
        {
            ref Item a = ref Spawn("rise_small", root, facing, 0.12f, FrontOrder);
            a.Size = size;
            a.From = new Vector2(0.9f, 0.4f);
            a.To = Vector2.one;
            a.End = new Vector2(1.3f, 1.2f);
            a.Grow = 0.5f;
            a.Easing = Ease.Out;
            a.Hold = 0.5f;
            for (int i = 0; i < 2; i++)
            {
                bool core = i == 1;
                ref Item r = ref Spawn(core ? "rise_core" : "rise_slash", root, facing, core ? 0.24f : 0.36f, core ? FrontOrder + 1 : FrontOrder, core);
                r.Delay = 0.03f;
                r.Size = size;
                r.From = new Vector2(0.9f, 0.3f);
                r.To = Vector2.one;
                r.End = new Vector2(1f, 1.06f);
                r.Grow = core ? 0.3f : 0.22f;
                r.Easing = Ease.Out;
                r.Hold = core ? 0.3f : 0.5f;
                r.Color = core ? new Color(1f, 1f, 1f, 0.75f) : Color.white;
            }

            ref Item g = ref Spawn("rise_splash", root, facing, 0.32f, FrontOrder + 1);
            g.Delay = 0.04f;
            g.Size = size;
            g.From = new Vector2(0.4f, 0.2f);
            g.To = Vector2.one;
            g.End = new Vector2(1.2f, 0.6f);
            g.Grow = 0.3f;
            g.Easing = Ease.OutBack;
            g.Hold = 0.45f;
        }

        /// <summary>Pha lê bay vút lên trước mặt (K5) + vài viên rời bay cao.</summary>
        public void RiseCrystals(Vector2 root, float facing, float size = 1f)
        {
            ref Item r = ref Spawn("rise_crystals", root, facing, 0.45f, FrontOrder);
            r.Size = size;
            r.From = new Vector2(1f, 0.6f);
            r.To = Vector2.one;
            r.End = new Vector2(0.9f, 1.2f);
            r.Grow = 0.3f;
            r.Easing = Ease.Out;
            r.FadeIn = 0.12f;
            r.Hold = 0.35f;
            r.Velocity = new Vector2(0f, 4f * size);
            r.Drag = 2.5f;
            Shards("rise_shard", root + new Vector2(facing * 0.9f * size, 0.4f * size), facing, 5, 7f, 0.5f, Vector2.up * 2f, size);
        }

        // =============================================================================================================
        // Nút 1 · ↓ Đánh thường thủ — K2 vòng chữ xoắn quanh eo (trước / sau), K3 tia đất, K4 gai pha lê quanh người
        // =============================================================================================================

        /// <summary>
        /// Vòng chữ xoắn quanh người (K2): count vòng nối nhau từ dưới lên, mỗi vòng = nửa trước (trước PINK) + nửa sau (cùng
        /// ảnh lật dọc, mờ hơn, sau lưng) — vòng ôm quanh thân thay vì vệt ngang dày vẽ đè lên người.
        /// </summary>
        public void GuardRings(Func<Vector2> waist, float facing, int count, float life, float size = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float y = (-0.45f + 0.5f * i) * Mathf.Min(size, 1.4f);
                for (int half = 0; half < 2; half++)
                {
                    bool back = half == 1;
                    ref Item r = ref Spawn("guard_ring", waist(), facing, life, back ? BackOrder : FrontOrder);
                    r.Follow = waist;
                    r.Offset = new Vector2(0f, y + (back ? 0.08f * size : 0f));
                    r.FlipY = back;
                    r.Delay = i * 0.06f;
                    r.Size = size * (1f - 0.08f * i);
                    r.Color = back ? new Color(0.85f, 0.7f, 1f, 0.6f) : Color.white;
                    r.From = new Vector2(0.45f, 0.6f);
                    r.To = Vector2.one;
                    r.End = new Vector2(1.15f, 0.8f);
                    r.Grow = 0.35f;
                    r.Easing = Ease.OutBack;
                    r.FadeIn = 0.1f;
                    r.Hold = 0.6f;
                    r.Velocity = new Vector2(0f, 0.6f);
                }
            }
        }

        /// <summary>Tia sáng toé lên từ chỗ đầu nhọn trượng cắm xuống đất (K3), root = x mũi trượng, y chân.</summary>
        public void GuardRay(Vector2 root, float facing, float size = 1f)
        {
            ref Item r = ref Spawn("guard_ray", root, facing, 0.3f, FrontOrder, true);
            r.Size = size;
            r.From = new Vector2(0.7f, 0.2f);
            r.To = Vector2.one;
            r.End = new Vector2(1.1f, 1.2f);
            r.Grow = 0.3f;
            r.Easing = Ease.Out;
            r.Hold = 0.4f;
        }

        /// <summary>
        /// Gai pha lê trồi vọt lên quanh PINK (K4) đứng life giây rồi lún xuống: cụm trái sau lưng, cụm phải trước mặt, cụm chân
        /// gai thấp trước chân (lớp trước người), cộng thêm gai lớn rời ở hai bên ra tới bán kính radius (vùng đánh trúng), xen
        /// trước / sau; mảnh pha lê văng.
        /// </summary>
        public void Spikes(Vector2 feet, float facing, float life, float size, float radius)
        {
            SpikeItem("spikes_back", feet, facing, life, size, BackOrder, 0f);
            SpikeItem("spikes_front", feet, facing, life, size, FrontOrder, 0.02f);
            SpikeItem("spikes_low", feet, facing, life, size, FrontOrder + 1, 0.01f);
            // gai lớn rời: hai bên, càng xa càng thấp, trễ dần (gai lan ra ngoài như sóng)
            float inner = 1.25f * size;
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? facing : -facing;
                float t = i < 2 ? 0f : 1f;
                float x = Mathf.Lerp(inner, Mathf.Max(inner + 0.3f, radius * 0.92f), t);
                ref Item s = ref SpikeItem("spike_tall", feet + new Vector2(side * x, 0f), side, life * 0.9f, size * (0.85f - 0.2f * t),
                    i == 1 ? BackOrder : FrontOrder, 0.03f + 0.04f * t);
                s.Angle = -side * facing * (6f + 8f * t); // nghiêng ra ngoài
            }

            Shards("guard_crystal", feet + Vector2.up * (0.5f * size), facing, 6, 5f, 0.5f, Vector2.up, size * 1.3f);
        }

        ref Item SpikeItem(string name, Vector2 at, float facing, float life, float size, int order, float delay)
        {
            ref Item s = ref Spawn(name, at, facing, life, order);
            s.Size = size;
            s.Delay = delay;
            s.From = new Vector2(0.8f, 0.05f);
            s.To = Vector2.one;
            s.End = new Vector2(1f, 0.1f);
            s.Grow = 0.14f;
            s.Easing = Ease.OutBack;
            s.Hold = 0.7f;
            return ref s;
        }

        // =============================================================================================================
        // Nút 2 · Áp sát — bóng mờ cả dáng lao (cố ý), vệt tốc độ, cung quét + dải quét sát đất, bụi trước / sau
        // =============================================================================================================

        /// <summary>Bóng mờ cả dáng lao (K3, tím đơn sắc, sau lưng) ở chân PINK lúc đó, trôi lùi nhẹ rồi tan.</summary>
        public void Afterimage(Vector2 feet, float facing, float alpha = 0.5f, float size = 1f)
        {
            ref Item a = ref Spawn("afterimage", feet, facing, 0.26f, BackOrder);
            a.Size = size;
            a.Color = new Color(1f, 1f, 1f, alpha);
            a.From = Vector2.one;
            a.To = Vector2.one;
            a.End = new Vector2(1.06f, 0.97f);
            a.Hold = 0.1f;
            a.Velocity = new Vector2(-facing * 1.2f, 0f);
        }

        /// <summary>Vệt tốc độ (K2) đứng yên ở chỗ vừa lướt qua: vệt dài sát chân, vệt ngắn ngang ngực, kéo về sau rồi tan.</summary>
        public void Streaks(Vector2 feet, Vector2 chest, float facing, float size = 1f)
        {
            for (int i = 0; i < 2; i++)
            {
                bool low = i == 0;
                Vector2 at = low ? feet + new Vector2(0f, 0.22f * size) : chest + new Vector2(0f, Random.Range(-0.25f, 0.25f) * size);
                ref Item s = ref Spawn(low ? "streak_long" : "streak_short", at, facing, 0.22f, BackOrder - 1, true);
                s.Size = size;
                s.Color = new Color(1f, 1f, 1f, 0.8f);
                s.From = new Vector2(0.5f, 1f);
                s.To = Vector2.one;
                s.End = new Vector2(1.3f, 0.5f);
                s.Grow = 0.3f;
                s.Easing = Ease.Out;
                s.Hold = 0.3f;
            }
        }

        /// <summary>
        /// Quét trượng thành lưỡi liềm (K4) ngay dưới mũi trượng (root = x mũi trượng, y chân): cung phải vươn lên + dải quét
        /// sát đất kéo dài về sau từ mũi trượng (trước chân), lõi sáng trên dải.
        /// </summary>
        public void Sweep(Vector2 root, float facing, float size = 1f)
        {
            ref Item band = ref Spawn("sweep_band", root, facing, 0.34f, FrontOrder);
            band.Size = size;
            band.From = new Vector2(0.3f, 0.8f);
            band.To = Vector2.one;
            band.End = new Vector2(1.08f, 0.6f);
            band.Grow = 0.3f;
            band.Easing = Ease.Out;
            band.FadeIn = 0.05f;
            band.Hold = 0.45f;
            ref Item arc = ref Spawn("sweep", root, facing, 0.32f, FrontOrder + 1);
            arc.Delay = 0.03f;
            arc.Size = size;
            arc.From = new Vector2(0.8f, 0.3f);
            arc.To = Vector2.one;
            arc.End = new Vector2(1.1f, 1.05f);
            arc.Grow = 0.3f;
            arc.Easing = Ease.Out;
            arc.Hold = 0.45f;
        }

        /// <summary>Bụi tung khi trượt dừng (K5): đám trái sau lưng, đám phải trước chân, cuộn ra hai bên rồi tan.</summary>
        public void Dust(Vector2 feet, float facing, float size = 1f)
        {
            for (int i = 0; i < 3; i++)
            {
                bool back = i == 1;
                float side = back ? -facing : facing;
                ref Item d = ref Spawn(i == 2 ? "dust_b" : "dust", feet + new Vector2(side * (0.25f + 0.35f * i) * size, 0f), side, 0.6f,
                    back ? BackOrder : FrontOrder);
                d.Size = Random.Range(0.9f, 1.15f) * size;
                d.Color = new Color(1f, 1f, 1f, 0.85f);
                d.From = new Vector2(0.55f, 0.5f);
                d.To = Vector2.one;
                d.End = new Vector2(1.35f, 1.2f);
                d.Grow = 0.4f;
                d.Easing = Ease.Out;
                d.FadeIn = 0.1f;
                d.Hold = 0.35f;
                d.Velocity = new Vector2(side * Random.Range(0.5f, 1.1f), 0.25f) * size;
                d.Drag = 2f;
            }
        }

        // =============================================================================================================
        // Nút 2 · ↑ Tầm xa — K2 vòng phép → K3 vòng nghiêng + mũi pha lê → K4 rồng (vòng ở trượng, thân, lõi, đầu) → nổ + đá
        // =============================================================================================================

        /// <summary>Vòng phép trước trượng (K2): bung ra ở follow(), giữ, cuối đời bóp dẹt ngang (đang nghiêng đi → pha K3).</summary>
        public void CastCircle(Func<Vector2> follow, float facing, float life, float size = 1f)
        {
            ref Item c = ref Spawn("circle_front", follow(), facing, life, FrontOrder);
            c.Follow = follow;
            c.Size = size;
            c.From = new Vector2(0.2f, 0.2f);
            c.To = Vector2.one;
            c.End = new Vector2(0.25f, 1.02f);
            c.Grow = 0.35f;
            c.Easing = Ease.OutBack;
            c.FadeIn = 0.15f;
            c.Hold = 0.6f;
        }

        /// <summary>
        /// Pha K3: vòng phép nghiêng (nhìn cạnh) ở mũi trượng + mũi pha lê nhọn phía trước, bám follow(), hiện life giây —
        /// vòng mở từ dẹt ra (nối tiếp vòng K2 đang bóp dẹt), mũi pha lê đâm tới.
        /// </summary>
        public void CastSide(Func<Vector2> follow, float facing, float life, float size = 1f)
        {
            for (int i = 0; i < 2; i++)
            {
                bool tip = i == 1;
                ref Item c = ref Spawn(tip ? "bolt_tip" : "circle_side", follow(), facing, life, tip ? FrontOrder + 1 : FrontOrder);
                c.Follow = follow;
                c.Size = size;
                c.From = tip ? new Vector2(0.3f, 0.8f) : new Vector2(0.3f, 0.9f);
                c.To = Vector2.one;
                c.End = tip ? new Vector2(1.3f, 0.9f) : new Vector2(0.8f, 1.05f);
                c.Grow = 0.35f;
                c.Easing = Ease.OutBack;
                c.FadeIn = 0.12f;
                c.Hold = 0.7f;
            }
        }

        /// <summary>
        /// Rồng pha lê bay từ from theo hướng facing: vòng nghiêng đọng ở chỗ phóng; đầu rồng đủ cỡ ngay từ đầu (đọc được ngay),
        /// thân + lõi trắng kéo dài từ chỗ phóng tới đầu. Mỗi frame hỏi hit(đầu); trúng / hết range thì gọi ended(đầu), đầu + thân
        /// đọng lại 0.2 giây ở chỗ trúng (thân thu về) rồi tan.
        /// </summary>
        public void Dragon(Vector2 from, float facing, float speed, float range, Func<Vector2, bool> hit, Action<Vector2> ended, float size = 1f)
        {
            ref Item ring = ref Spawn("dragon_ring", from, facing, 0.35f, FrontOrder);
            ring.Size = size;
            ring.From = new Vector2(0.6f, 0.7f);
            ring.To = Vector2.one;
            ring.End = new Vector2(0.4f, 1.15f);
            ring.Grow = 0.25f;
            ring.Easing = Ease.OutBack;
            ring.Hold = 0.4f;
            Bolt b = NewBolt(from, facing, speed, range, hit, ended, size, 0.2f);
            // trong tranh (K4) gốc thân / lõi (mép phải thân) nằm sau mõm rồng 86 px = 0.55 unit
            AddLayer(b, "dragon", FrontOrder, false, 1f, 1.7f, 0.55f);
            AddLayer(b, "dragon_core", FrontOrder + 1, true, 0.75f, 1.7f, 0.55f);
            AddLayer(b, "dragon_head", FrontOrder + 2, false, 1f, 0f);
        }

        /// <summary>Nổ ở đầu rồng (K4): quầng nổ (sau đầu rồng đang đọng) + đá vụn tách lớp văng ra, rơi xuống.</summary>
        public void DragonBlast(Vector2 at, float facing, float size = 1f)
        {
            ref Item b = ref Spawn("dragon_blast", at, facing, 0.45f, FrontOrder + 1);
            b.Size = size;
            b.From = new Vector2(0.35f, 0.5f);
            b.To = Vector2.one;
            b.End = new Vector2(1.12f, 1.08f);
            b.Grow = 0.25f;
            b.Easing = Ease.Out;
            b.Hold = 0.35f;
            ref Item r = ref Spawn("dragon_rocks", at, facing, 0.6f, TopOrder);
            r.Size = size;
            r.From = new Vector2(0.6f, 0.6f);
            r.To = Vector2.one;
            r.End = new Vector2(1.35f, 1.2f);
            r.Grow = 0.3f;
            r.Easing = Ease.Out;
            r.Hold = 0.5f;
            r.Velocity = new Vector2(facing * 1.2f, 1.5f) * size;
            r.Gravity = 6f;
        }

        // =============================================================================================================
        // Nút 2 · ↓ Chiêu thủ — vòng phép dưới chân (nửa trước / nửa sau), đoạn vòng xoắn quay quanh người, pha lê bay quanh
        // =============================================================================================================

        /// <summary>
        /// Vòng phép dưới chân (K3 → K6): nửa trước vẽ trước chân, nửa sau trên mặt đất sau lưng; bung ra ở feet, giữ life giây.
        /// size = cỡ ảnh (độ dày vòng); radius &gt; 0: vòng giãn ngang cho mép vòng tới đúng bán kính vùng đánh (vòng dẹt trên mặt
        /// đất, không cao thêm — thấy rõ vùng đánh mà không che người).
        /// </summary>
        public void WardCircle(Vector2 feet, float life, Func<bool> alive = null, float size = 1f, float radius = 0f)
        {
            Sprite sprite = Sprite("ward_circle");
            float wide = 1f;
            if (sprite != null && radius > 0f)
                wide = Mathf.Max(1f, radius / Mathf.Max(0.1f, sprite.bounds.extents.x * size));
            for (int i = 0; i < 2; i++)
            {
                bool back = i == 1;
                ref Item c = ref Spawn(back ? "ward_circle_back" : "ward_circle", feet, 1f, life, back ? GroundOrder : FrontOrder - 1);
                c.Size = size;
                c.From = new Vector2(0.2f * wide, 0.2f);
                c.To = new Vector2(wide, 1f);
                c.End = new Vector2(1.08f * wide, 1.08f);
                c.Grow = Mathf.Min(0.5f, 0.18f / Mathf.Max(0.2f, life));
                c.Easing = Ease.OutBack;
                c.Hold = Mathf.Max(0.5f, 1f - 0.25f / Mathf.Max(0.25f, life));
                c.Alive = alive;
            }
        }

        /// <summary>
        /// count đoạn vòng (ảnh sprite: "ward_swirl" chiêu thủ, "nova_runes" vòng chữ cổ kĩ năng đặc biệt) quay quanh trục dọc
        /// của người ở các độ cao khác nhau: bề ngang co giãn theo góc quay, nửa vòng quay ra trước vẽ trước người, nửa kia sau
        /// lưng — vòng xoắn ôm quanh thân như tranh.
        /// </summary>
        public void Swirls(string sprite, Func<Vector2> center, float facing, int count, float low, float high, float life, float turns,
            Func<bool> alive = null, float size = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float k = count > 1 ? i / (count - 1f) : 0.5f;
                ref Item s = ref Spawn(sprite, center(), facing, life, FrontOrder);
                s.Follow = center;
                s.Offset = new Vector2(0f, Mathf.Lerp(low, high, k) * size);
                s.Turn = true;
                s.TurnSpeed = turns * Mathf.PI * 2f / Mathf.Max(0.1f, life) * (i % 2 == 0 ? 1f : 0.85f);
                s.TurnPhase = i * 2.1f;
                s.TurnBack = BackOrder;
                s.Size = size * (1f - 0.1f * Mathf.Abs(k - 0.5f));
                s.Delay = i * 0.05f;
                s.From = new Vector2(0.5f, 0.5f);
                s.To = Vector2.one;
                s.End = new Vector2(1.15f, 0.9f);
                s.Grow = Mathf.Min(0.4f, 0.15f / Mathf.Max(0.15f, life));
                s.Easing = Ease.OutBack;
                s.Hold = Mathf.Max(0.5f, 1f - 0.2f / Mathf.Max(0.2f, life));
                s.Alive = alive;
            }
        }

        /// <summary>
        /// count viên pha lê bay quanh người theo elip quanh center() (dùng chiêu thủ K4 → K6): xen 2 mẫu pha lê của tấm, ở các độ
        /// cao khác nhau, đi qua trước / sau người; hiện life giây.
        /// </summary>
        public void OrbitDiamonds(Func<Vector2> center, int count, float radius, float life, Func<bool> alive = null, float size = 1f,
            string sprite = "diamond", string sprite2 = "diamond_b")
        {
            for (int i = 0; i < count; i++)
            {
                ref Item d = ref Spawn(i % 2 == 0 ? sprite : sprite2, center(), 1f, life, FrontOrder);
                d.Follow = center;
                d.Orbit = true;
                d.OrbitX = radius * Random.Range(0.85f, 1.1f);
                d.OrbitY = radius * 0.22f;
                d.OrbitSpeed = (i % 2 == 0 ? 1f : 0.8f) * 2.4f;
                d.OrbitPhase = i * Mathf.PI * 2f / count;
                d.Offset = new Vector2(0f, Mathf.Lerp(-0.6f, 1.1f, (i + 0.5f) / count) * Mathf.Min(size, 1.5f));
                d.Size = size * Random.Range(0.85f, 1.15f);
                d.Delay = i * 0.04f;
                d.From = new Vector2(0.2f, 0.2f);
                d.To = Vector2.one;
                d.End = new Vector2(0.4f, 0.4f);
                d.Grow = Mathf.Min(0.4f, 0.2f / Mathf.Max(0.2f, life));
                d.Easing = Ease.OutBack;
                d.Hold = Mathf.Max(0.5f, 1f - 0.2f / Mathf.Max(0.2f, life));
                d.Alive = alive;
            }
        }

        // =============================================================================================================
        // Nút 3 · Kĩ năng đặc biệt — vòng chữ cổ (K2), cung lớn khi bật lên (K3), vòm pha 1 → pha 2 + vòng chữ, chân vòm, đá
        // =============================================================================================================

        /// <summary>Vệt cung lớn cuộn quanh người lúc bật lên giơ trượng (K3), bám chest().</summary>
        public void NovaArc(Func<Vector2> chest, float facing, float size = 1f)
        {
            ref Item a = ref Spawn("nova_arc", chest(), facing, 0.32f, FrontOrder);
            a.Follow = chest;
            a.Size = size;
            a.From = new Vector2(0.6f, 0.5f);
            a.To = Vector2.one;
            a.End = new Vector2(1.1f, 1.1f);
            a.Grow = 0.35f;
            a.Easing = Ease.Out;
            a.FadeIn = 0.1f;
            a.Hold = 0.5f;
            a.Spin = -60f;
        }

        /// <summary>
        /// Mái vòm pha lê nổ quanh PINK (K4 → K5), gốc ở chân: pha 1 (K4) bung nhanh rồi nhường pha 2 (K5); vòng chữ cổ của vòm
        /// dâng lên sau lưng; chân vòm / gai sát đất vẽ TRƯỚC người (vòm không còn nằm trọn sau lưng); đá vụn tách lớp văng ra
        /// rơi xuống; mảnh pha lê.
        /// </summary>
        public void Dome(Vector2 feet, float facing, float size = 1f)
        {
            ref Item a = ref Spawn("nova_dome_a", feet, facing, 0.22f, BackOrder);
            a.Size = size * 0.92f;
            a.From = new Vector2(0.3f, 0.2f);
            a.To = Vector2.one;
            a.End = new Vector2(1.08f, 1.08f);
            a.Grow = 0.5f;
            a.Easing = Ease.OutBack;
            a.Hold = 0.6f;
            ref Item d = ref Spawn("nova_dome", feet, facing, 0.6f, BackOrder);
            d.Delay = 0.12f;
            d.Size = size;
            d.From = new Vector2(0.95f, 0.95f);
            d.To = Vector2.one;
            d.End = new Vector2(1.1f, 1.08f);
            d.Grow = 0.2f;
            d.Easing = Ease.Out;
            d.FadeIn = 0.08f;
            d.Hold = 0.5f;
            ref Item runes = ref Spawn("nova_rune_arc", feet, facing, 0.65f, BackOrder - 1);
            runes.Delay = 0.1f;
            runes.Size = size;
            runes.From = new Vector2(0.7f, 0.5f);
            runes.To = Vector2.one;
            runes.End = new Vector2(1.05f, 1.15f);
            runes.Grow = 0.3f;
            runes.Easing = Ease.OutBack;
            runes.FadeIn = 0.15f;
            runes.Hold = 0.6f;
            ref Item front = ref Spawn("nova_front", feet, facing, 0.6f, FrontOrder);
            front.Delay = 0.02f;
            front.Size = size;
            front.From = new Vector2(0.4f, 0.3f);
            front.To = Vector2.one;
            front.End = new Vector2(1.15f, 0.6f);
            front.Grow = 0.25f;
            front.Easing = Ease.OutBack;
            front.Hold = 0.45f;
            ref Item rocks = ref Spawn("nova_rocks", feet, facing, 0.8f, FrontOrder + 1);
            rocks.Delay = 0.1f;
            rocks.Size = size;
            rocks.From = new Vector2(0.8f, 0.75f);
            rocks.To = Vector2.one;
            rocks.End = new Vector2(1.3f, 1.15f);
            rocks.Grow = 0.25f;
            rocks.Easing = Ease.Out;
            rocks.Hold = 0.55f;
            rocks.Velocity = new Vector2(0f, 1.2f * size);
            rocks.Gravity = 5f;
            Shards("nova_shard", feet + Vector2.up * (1f * size), facing, 8, 8f, 0.6f, Vector2.up * 0.5f, size);
        }

        // =============================================================================================================
        // Nút 3 · ↑ Tầm xa — cầu lửa đầu trượng, vòng phép, tia pha lê (thân / lõi / mũi / đuôi), nổ hình sao + tia sao
        // =============================================================================================================

        /// <summary>Cầu lửa tụ ở mũi trượng (K2): bám theo follow, to dần trong life giây (lớp màu + lớp cộng sáng).</summary>
        public void Orb(Func<Vector2> follow, float facing, float life, float size = 1f)
        {
            for (int i = 0; i < 2; i++)
            {
                bool glow = i == 0;
                ref Item o = ref Spawn("orb", follow(), facing, life, glow ? FrontOrder + 1 : FrontOrder, glow);
                o.Follow = follow;
                o.Size = size * (glow ? 1.1f : 1f);
                o.Color = glow ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
                o.From = new Vector2(0.2f, 0.2f);
                o.To = Vector2.one;
                o.End = new Vector2(glow ? 1.35f : 1.25f, glow ? 1.35f : 1.25f);
                o.Grow = 0.7f;
                o.Easing = Ease.Out;
                o.FadeIn = 0.15f;
                o.Hold = 0.85f;
                o.Spin = (glow ? -60f : 90f) * facing;
            }
        }

        /// <summary>Vòng phép quanh mũi trượng (K3, tâm vẽ lại): bung ra, xoay chậm, bám follow, sau lưng trượng.</summary>
        public void LanceCircle(Func<Vector2> follow, float facing, float life, float size = 1f)
        {
            ref Item c = ref Spawn("circle_lance", follow(), facing, life, BackOrder);
            c.Follow = follow;
            c.Size = size;
            c.From = new Vector2(0.2f, 0.2f);
            c.To = Vector2.one;
            c.End = new Vector2(1.2f, 1.2f);
            c.Grow = 0.3f;
            c.Easing = Ease.OutBack;
            c.Hold = 0.7f;
            c.Spin = -40f * facing;
        }

        /// <summary>
        /// Tia pha lê bay từ from (mũi tia) theo hướng facing (K4): thân + lõi trắng (cộng sáng) + mũi pha lê bay cùng, 2 vệt
        /// đuôi kéo dài dần theo quãng bay; như Dragon (đọng lại 0.12 giây ở chỗ trúng).
        /// </summary>
        public void Lance(Vector2 from, float facing, float speed, float range, Func<Vector2, bool> hit, Action<Vector2> ended, float size = 1f)
        {
            Bolt b = NewBolt(from, facing, speed, range, hit, ended, size, 0.12f);
            // thân + lõi trồi ra từ đầu trượng (kéo dài tới đúng cỡ tranh), đuôi kéo dài tiếp theo quãng bay
            AddLayer(b, "lance_tail", FrontOrder, false, 0.9f, 1.8f);
            AddLayer(b, "lance", TopOrder, false, 1f, 1f);
            AddLayer(b, "lance_core", TopOrder + 1, true, 0.7f, 1f);
            AddLayer(b, "lance_tip", TopOrder + 2, false, 1f, 0f);
        }

        /// <summary>Nổ hình sao ở chỗ tia trúng (K5) + tia sao chéo cộng sáng (lớp riêng, loé dài rồi co) + mảnh pha lê.</summary>
        public void StarBlast(Vector2 at, float facing, float size = 1f)
        {
            ref Item s = ref Spawn("star_blast", at, facing, 0.55f, TopOrder);
            s.Size = size;
            s.From = new Vector2(0.3f, 0.3f);
            s.To = Vector2.one;
            s.End = new Vector2(1.15f, 1.15f);
            s.Grow = 0.22f;
            s.Easing = Ease.OutBack;
            s.Hold = 0.4f;
            s.Spin = 20f * facing;
            ref Item r = ref Spawn("star_rays", at, facing, 0.3f, TopOrder + 1, true);
            r.Size = size;
            r.From = new Vector2(0.2f, 0.2f);
            r.To = new Vector2(1.25f, 1.25f);
            r.End = new Vector2(0.6f, 0.6f);
            r.Grow = 0.3f;
            r.Easing = Ease.Out;
            r.Hold = 0.3f;
            Shards("lance_shard", at, facing, 8, 7f, 0.55f, default, size * 1.4f);
        }

        // =============================================================================================================
        // Nút 3 · ↓ Áp chế — xích (Tiled), xích quấn (trước + sau mục tiêu, siết), lồng (cột trước + cột giữa / quầng sau)
        // =============================================================================================================

        /// <summary>
        /// Xích phóng từ from() tới to() (K2): lao ra trong 0.12 giây, rung rồi căng, giữ life giây (alive() false thì tắt sớm).
        /// sag = độ võng giữa sợi (unit); size = cỡ mắt xích. 6 đoạn Tiled mỗi sợi.
        /// </summary>
        public void Chains(Func<Vector2> from, Func<Vector2> to, float life, Func<bool> alive = null, float sag = 0f, float size = 1f)
        {
            Sprite link = Sprite("chain");
            if (link == null)
                return;
            Chain c = _freeChains.Count > 0 ? _freeChains.Pop() : new Chain();
            for (int i = 0; i < ChainSegments; i++)
            {
                SpriteRenderer r = Rent(link, FrontOrder, false);
                r.drawMode = SpriteDrawMode.Tiled;
                r.tileMode = SpriteTileMode.Continuous;
                c.Segs[i] = r;
            }

            c.From = from;
            c.To = to;
            c.Alive = alive;
            c.Age = 0f;
            c.Life = life;
            c.Phase = Random.value * 10f;
            c.Sag = sag;
            c.Size = size;
            c.Released = false;
            _chains.Add(c);
            UpdateChain(c);
        }

        /// <summary>
        /// Xích quấn quanh người mục tiêu (K3, không còn hình nộm): bám chân feet(), cao height, phủ ngang width unit. Lớp trước
        /// (đè lên mục tiêu) + lớp sau (cùng ảnh lật ngang, tối hơn, vẽ ở behind — sau mục tiêu) như vòng xích quấn quanh thân;
        /// siết lại (bóp ngang) lúc đầu rồi rung nhẹ.
        /// </summary>
        public void ChainWrap(Func<Vector2> feet, float height, float width, float life, int behind, Func<bool> alive = null)
        {
            float grow = Mathf.Min(0.5f, 0.15f / Mathf.Max(0.15f, life));
            float hold = Mathf.Max(0.5f, 1f - 0.25f / Mathf.Max(0.25f, life));
            Tile("chain_wrap", feet, height, width, life, alive, new Vector2(1.4f, 0.7f), new Vector2(1.12f, 1f), grow, 0f, hold, 0.04f,
                FrontOrder, Color.white, false);
            Tile("chain_wrap", feet, height, width, life, alive, new Vector2(1.4f, 0.7f), new Vector2(1.12f, 1f), grow, 0f, hold, 0.04f,
                behind, new Color(0.75f, 0.6f, 0.95f, 0.75f), true);
        }

        /// <summary>
        /// Lồng xích giam mục tiêu ở feet() (K4 → K6, không còn hình nộm): cột giáo trước + nửa trước vòng đáy (đè lên mục tiêu),
        /// cột giáo giữa + quầng tím + nửa sau vòng đáy (behind — sau mục tiêu); trồi lên từ mặt đất, cao height, phủ ngang
        /// width (boss to ngang: nhiều lồng xếp cạnh nhau), giữ life giây; hạt pha lê tan bay lên.
        /// </summary>
        public void Cage(Func<Vector2> feet, float height, float width, float life, int behind, Func<bool> alive = null)
        {
            float grow = Mathf.Min(0.5f, 0.2f / Mathf.Max(0.2f, life));
            float fade = Mathf.Min(0.3f, 0.06f / Mathf.Max(0.2f, life));
            float hold = Mathf.Max(0.5f, 1f - 0.3f / Mathf.Max(0.3f, life));
            Tile("cage_back", feet, height, width, life, alive, new Vector2(0.9f, 0.05f), new Vector2(1.1f, 1.05f), grow, fade, hold, 0.06f,
                behind, Color.white, false);
            Tile("cage", feet, height, width, life, alive, new Vector2(0.9f, 0.05f), new Vector2(1.15f, 1.05f), grow, fade, hold, 0.06f,
                FrontOrder, Color.white, false);
            Vector2 at = feet();
            for (int i = 0; i < 6; i++)
            {
                ref Item p = ref Spawn("seal_shard", at + new Vector2(Random.Range(-0.5f, 0.5f) * width, Random.Range(0.1f, 0.8f) * height),
                    Random.value < 0.5f ? 1f : -1f, Mathf.Min(1.2f, life) * Random.Range(0.6f, 1f), FrontOrder + 1);
                p.Delay = Random.Range(0.1f, 0.5f);
                p.Size = Random.Range(0.7f, 1.1f);
                p.Velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.2f));
                p.Drag = 0.5f;
                p.FadeIn = 0.2f;
                p.Hold = 0.4f;
                p.From = new Vector2(0.5f, 0.5f);
                p.To = Vector2.one;
                p.End = new Vector2(0.3f, 0.3f);
                p.Grow = 0.3f;
                p.Alive = alive;
            }
        }

        // Ảnh name cao height, xếp n tấm cạnh nhau phủ width (mỗi tấm giãn ngang tối đa 1.3 lần), bám theo feet()
        void Tile(string name, Func<Vector2> feet, float height, float width, float life, Func<bool> alive, Vector2 from, Vector2 end,
            float grow, float fadeIn, float hold, float stagger, int order, Color color, bool flipX)
        {
            Sprite sprite = Sprite(name);
            if (sprite == null)
                return;
            float size = height / Mathf.Max(0.1f, sprite.bounds.size.y);
            float natural = Mathf.Max(0.1f, sprite.bounds.size.x * size);
            int n = Mathf.Clamp(Mathf.CeilToInt(width / (natural * 1.3f)), 1, 5);
            float wide = Mathf.Max(1f, width / (n * natural));
            float step = n > 1 ? (width - natural * wide) / (n - 1) : 0f;
            for (int i = 0; i < n; i++)
            {
                ref Item c = ref Spawn(name, feet(), flipX ? -1f : 1f, life, order);
                c.Follow = feet;
                c.Offset = new Vector2((i - (n - 1) * 0.5f) * step, 0f);
                c.Size = size;
                c.Color = color;
                c.Delay = Mathf.Abs(i - (n - 1) * 0.5f) * stagger;
                c.From = new Vector2(from.x * wide, from.y);
                c.To = new Vector2(wide, 1f);
                c.End = new Vector2(end.x * wide, end.y);
                c.Grow = grow;
                c.Easing = Ease.OutBack;
                c.FadeIn = fadeIn;
                c.Hold = hold;
                c.Alive = alive;
            }
        }

        // =============================================================================================================
        // Chạy
        // =============================================================================================================

        // additive: cộng sáng (lõi trắng, quầng sáng, cầu sáng) thay vì hoà trộn thường
        ref Item Spawn(string name, Vector2 at, float facing, float life, int order, bool additive = false)
        {
            Sprite sprite = Sprite(name);
            if (sprite == null || _count >= _items.Length)
            {
                _spare = default;
                return ref _spare;
            }

            ref Item p = ref _items[_count++];
            p = default;
            p.Renderer = Rent(sprite, order, additive);
            p.Position = at;
            p.Life = Mathf.Max(0.02f, life);
            p.Size = 1f;
            p.Facing = facing < 0f ? -1f : 1f;
            p.From = p.To = p.End = Vector2.one;
            p.Hold = 0.6f;
            p.Color = Color.white;
            p.Order = order;
            return ref p;
        }

        Bolt NewBolt(Vector2 from, float facing, float speed, float range, Func<Vector2, bool> hit, Action<Vector2> ended, float size, float linger)
        {
            Bolt b = _freeBolts.Count > 0 ? _freeBolts.Pop() : new Bolt();
            b.Count = 0;
            b.Position = from;
            b.Tail = from;
            b.Facing = facing < 0f ? -1f : 1f;
            b.Velocity = new Vector2(b.Facing * speed, 0f);
            b.Range = range;
            b.Travelled = 0f;
            b.Size = size;
            b.Age = 0f;
            b.Linger = linger;
            b.LingerAge = 0f;
            b.Hit = hit;
            b.Ended = ended;
            b.Done = false;
            _bolts.Add(b);
            return b;
        }

        // lớp của vật bay: maxStretch > 0 = lớp kéo dài từ chỗ phóng tới đầu (bề ngang tối đa maxStretch lần ảnh gốc)
        void AddLayer(Bolt b, string name, int order, bool additive, float strength, float maxStretch, float back = 0f)
        {
            Sprite sprite = Sprite(name);
            if (sprite == null || b.Count >= BoltLayers)
                return;
            int k = b.Count++;
            b.Layers[k] = Rent(sprite, order, additive);
            b.Natural[k] = maxStretch > 0f ? Mathf.Max(0.05f, sprite.bounds.size.x) : 0f;
            b.MaxStretch[k] = maxStretch;
            b.Strength[k] = strength;
            b.Back[k] = back;
            UpdateBolt(b, 0f);
        }

        // Sau Update và coroutine của chiêu: hiệu ứng vừa gọi trong frame này được đặt đúng chỗ trước khi vẽ
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _count;)
            {
                if (Step(ref _items[i], dt))
                {
                    i++;
                    continue;
                }

                Return(_items[i].Renderer);
                _items[i] = _items[--_count];
                _items[_count] = default;
            }

            for (int i = _chains.Count - 1; i >= 0; i--)
            {
                Chain c = _chains[i];
                c.Age += dt;
                if (!UpdateChain(c))
                {
                    for (int k = 0; k < ChainSegments; k++)
                    {
                        Return(c.Segs[k]);
                        c.Segs[k] = null;
                    }

                    c.From = c.To = null;
                    c.Alive = null;
                    _chains.RemoveAt(i);
                    _freeChains.Push(c);
                }
            }

            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = _bolts[i];
                if (UpdateBolt(b, dt))
                    continue;
                for (int k = 0; k < b.Count; k++)
                {
                    Return(b.Layers[k]);
                    b.Layers[k] = null;
                }

                b.Count = 0;
                b.Hit = null;
                b.Ended = null;
                _bolts.RemoveAt(i);
                _freeBolts.Push(b);
            }
        }

        bool Step(ref Item p, float dt)
        {
            p.Age += dt;
            float t = p.Age - p.Delay;
            SpriteRenderer r = p.Renderer;
            if (t < 0f)
            {
                r.transform.localScale = Vector3.zero;
                if (p.Follow != null)
                    p.Position = p.Follow() + p.Offset;
                return true;
            }

            // Bị ngắt sớm (mục tiêu gục / chiêu dừng): tắt dần trong 0.2 giây kể từ bây giờ
            if (!p.Released && p.Alive != null && !p.Alive())
            {
                p.Released = true;
                float left = 0.2f;
                float k0 = t / p.Life;
                if (k0 < p.Hold || p.Life - t > left)
                {
                    p.Life = t + left;
                    p.Hold = Mathf.Clamp01(t / p.Life);
                }
            }

            float k = t / p.Life;
            if (k >= 1f)
                return false;

            // Bay / rơi / bám theo / bay quanh
            Vector2 position;
            int order = p.Order;
            float turn = 1f;
            bool following = p.Follow != null && (p.FollowTime <= 0f || t < p.FollowTime);
            if (following)
            {
                Vector2 center = p.Follow() + p.Offset;
                if (p.Orbit)
                {
                    float a = p.OrbitPhase + p.OrbitSpeed * t;
                    float sin = Mathf.Sin(a);
                    position = center + new Vector2(Mathf.Cos(a) * p.OrbitX, sin * p.OrbitY);
                    order = sin < 0f ? FrontOrder : BackOrder; // nửa dưới elip là phía trước người
                }
                else
                    position = center;

                p.Position = position;
            }
            else
            {
                p.Velocity.y -= p.Gravity * dt;
                if (p.Drag > 0f)
                    p.Velocity *= 1f / (1f + p.Drag * dt);
                p.Position += p.Velocity * dt;
                position = p.Position;
            }

            if (p.Turn)
            {
                // quay quanh trục dọc: bề ngang × cos (âm = sang phía bên kia), nửa quay ra trước (sin > 0) vẽ trước người
                float a = p.TurnPhase + p.TurnSpeed * t;
                turn = Mathf.Cos(a);
                order = Mathf.Sin(a) > 0f ? p.Order : p.TurnBack;
            }

            // Cỡ: From → To (trong Grow phần đời) → End (lúc tắt dần); độ trong: hiện dần, giữ, tắt dần
            float g = p.Grow > 0f ? Mathf.Clamp01(k / p.Grow) : 1f;
            g = p.Easing == Ease.OutBack ? EaseOutBack(g) : p.Easing == Ease.Out ? 1f - (1f - g) * (1f - g) : g;
            Vector2 scale = Vector2.LerpUnclamped(p.From, p.To, g);
            float alpha = p.FadeIn > 0f ? Mathf.Clamp01(k / p.FadeIn) : 1f;
            if (k > p.Hold)
            {
                float f = (k - p.Hold) / Mathf.Max(1e-4f, 1f - p.Hold);
                scale = Vector2.Lerp(scale, p.End, f);
                alpha *= 1f - f;
            }

            Transform tr = r.transform;
            tr.position = position;
            tr.rotation = Quaternion.Euler(0f, 0f, (p.Angle + p.Spin * t) * p.Facing);
            tr.localScale = new Vector3(scale.x * p.Size * p.Facing * turn, scale.y * p.Size * (p.FlipY ? -1f : 1f), 1f);
            Color c = p.Color;
            c.a *= alpha;
            r.color = c;
            r.sortingOrder = order;
            return true;
        }

        bool UpdateBolt(Bolt b, float dt)
        {
            b.Age += dt;
            float alpha = 1f;
            if (!b.Done)
            {
                Vector2 step = b.Velocity * dt;
                b.Position += step;
                b.Travelled += step.magnitude;
                if (dt > 0f && (b.Travelled >= b.Range || (b.Hit != null && b.Hit(b.Position))))
                {
                    b.Done = true;
                    b.Ended?.Invoke(b.Position);
                }
            }
            else
            {
                // đọng lại ở chỗ trúng: thân thu dần về đầu, cả vật tan
                b.LingerAge += dt;
                float k = b.LingerAge / Mathf.Max(0.01f, b.Linger);
                if (k >= 1f)
                    return false;
                b.Tail = Vector2.Lerp(b.Tail, b.Position, Mathf.Clamp01(dt * 10f));
                alpha = 1f - k * k;
            }

            float pop = Mathf.Clamp01(b.Age / 0.05f); // hiện bật ra trong 0.05 giây (đầu đủ cỡ gần như ngay lập tức)
            for (int i = 0; i < b.Count; i++)
            {
                SpriteRenderer r = b.Layers[i];
                Vector2 at = b.Position - new Vector2(b.Facing * b.Back[i] * b.Size, 0f);
                float sx = b.Size;
                float show = 1f;
                if (b.Natural[i] > 0f)
                {
                    // lớp kéo dài: còn ngắn (vừa phóng) thì mờ theo độ dài — không lộ ảnh bị bóp dẹt thành mép thẳng
                    float length = Mathf.Max(0f, (at.x - b.Tail.x) * b.Facing);
                    float ratio = length / (b.Natural[i] * b.Size);
                    sx = Mathf.Clamp(ratio, 0.12f, b.MaxStretch[i]) * b.Size;
                    show = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, ratio));
                }

                Transform tr = r.transform;
                tr.position = at;
                tr.rotation = Quaternion.identity;
                tr.localScale = new Vector3(sx * b.Facing, b.Size * Mathf.Lerp(0.6f, 1f, pop), 1f);
                r.color = new Color(1f, 1f, 1f, alpha * show * b.Strength[i] * Mathf.Lerp(0.5f, 1f, pop));
            }

            return true;
        }

        bool UpdateChain(Chain c)
        {
            if (!c.Released && c.Alive != null && !c.Alive())
            {
                c.Released = true;
                c.Life = Mathf.Min(c.Life, c.Age + 0.15f);
            }

            if (c.Age >= c.Life)
                return false;

            Vector2 a = c.From();
            Vector2 d = c.To() - a;
            float length = d.magnitude;
            if (length < 1e-3f)
                return true;
            Vector2 dir = d / length;
            Vector2 n = new Vector2(-dir.y, dir.x);
            float reach = Mathf.Clamp01(c.Age / 0.12f);
            float alpha = Mathf.Clamp01((c.Life - c.Age) / 0.15f);
            float wobble = Mathf.Exp(-c.Age * 6f) * Mathf.Sin(c.Age * 40f + c.Phase) * Mathf.Min(0.3f, length * 0.05f);
            float bendAmp = wobble - c.Sag;
            Sprite link = c.Segs[0] != null ? c.Segs[0].sprite : null;
            float thick = link != null ? link.bounds.size.y : 0.1f;
            var color = new Color(1f, 1f, 1f, alpha);
            // đường cong võng y = sin(π u) × bendAmp; sợi phóng ra tới phần reach, chia đều ChainSegments đoạn thẳng nối nhau
            Vector2 prev = a;
            for (int i = 0; i < ChainSegments; i++)
            {
                float u1 = reach * (i + 1) / ChainSegments;
                Vector2 next = a + dir * (length * u1) + n * (Mathf.Sin(Mathf.PI * u1) * bendAmp);
                Vector2 seg = next - prev;
                float segLen = seg.magnitude;
                SpriteRenderer r = c.Segs[i];
                Transform tr = r.transform;
                if (segLen < 1e-3f)
                {
                    tr.localScale = Vector3.zero;
                    prev = next;
                    continue;
                }

                tr.position = (prev + next) * 0.5f;
                tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg);
                tr.localScale = new Vector3(c.Size, c.Size, 1f);
                r.size = new Vector2((segLen + 0.02f) / c.Size, thick); // chồng 0.02 unit cho liền chỗ nối
                r.color = color;
                prev = next;
            }

            return true;
        }

        SpriteRenderer Rent(Sprite sprite, int order, bool additive)
        {
            LoadMaterials();
            SpriteRenderer r = _free.Count > 0 ? _free.Pop() : NewSprite();
            r.drawMode = SpriteDrawMode.Simple;
            r.sprite = sprite;
            Material material = additive ? s_additive : s_alpha;
            r.sharedMaterial = material != null ? material : _defaultMaterial;
            r.sortingOrder = order;
            r.color = Color.white;
            r.enabled = true;
            Transform t = r.transform;
            t.localScale = Vector3.zero; // chưa đặt chỗ thì chưa hiện
            t.rotation = Quaternion.identity;
            return r;
        }

        SpriteRenderer NewSprite()
        {
            var r = new GameObject("Pink Fx").AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            if (_defaultMaterial == null)
                _defaultMaterial = r.sharedMaterial;
            return r;
        }

        void Return(SpriteRenderer r)
        {
            if (r == null)
                return;
            r.enabled = false;
            r.drawMode = SpriteDrawMode.Simple;
            r.sprite = null;
            _free.Push(r);
        }

        static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            return 1f + 2.70158f * x * x * x + 1.70158f * x * x;
        }
    }
}
