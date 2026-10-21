using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Octopus
{
    /// <summary>
    /// Hiệu ứng riêng của RED, cắt thẳng từ các tấm chiêu của người dùng (Tools/art/hero_fx_red.py →
    /// Resources/HeroFx/Red). Mỗi chiêu dùng hình cắt từ CHÍNH tấm của nó, tách lớp để diễn riêng từng lớp:
    ///  - vệt chém theo pha (Đánh thường: chém ngang / chém ngược / bổ + nổ đất; hất tung: 4 pha quệt đất → quét lên → vượt đầu
    ///    → thu vệt; áp sát: chém vòng ngang + dải tốc độ; đặc biệt: cung lớn → cung thứ hai → hai nhánh X + mảnh vỡ). Vệt có
    ///    lõi trắng riêng (ảnh *_core, cộng sáng, tắt trước) để thân đỏ còn lại không chói, không chồng quầng;
    ///  - vòng quanh thân tách nửa SAU (vẽ sau người) / nửa TRƯỚC (vẽ trước người): vòng chém xoay, vòng nổ tinh thể, vòng phép,
    ///    lồng xích (vòng xích trên đầu, vòng quấn thân lát nhiều tầng, gai, vòng phép dưới chân);
    ///  - sóng bay có ĐẦU và ĐUÔI riêng (đuôi dài dần theo quãng bay), bung ra từ mũi kiếm thật rồi mới lên đường bay.
    /// Ảnh vẽ cùng tỉ lệ với nhân vật (PPU 150) nên cỡ 1 = to đúng như trong tranh. Dùng chung với CombatFx (tia lửa, sóng tròn,
    /// đá văng…). Nhẹ cho điện thoại: SpriteRenderer và mục hiệu ứng đều dùng lại (kho), không cấp phát mỗi frame. Tự tạo khi
    /// cần (object "Red Fx" trong màn đang chơi, mất theo màn).
    /// </summary>
    public sealed class RedFx : MonoBehaviour
    {
        // ---- Tên ảnh trong Resources/HeroFx/Red (xem Tools/art/hero_fx_red.py: tấm / khung của từng ảnh) ----
        // #0 Đánh thường
        public const string ComboSlashSprite = "combo_h", ComboSlashCore = "combo_h_core";
        public const string ComboBackSprite = "combo_back", ComboBackCore = "combo_back_core";
        public const string ComboSlamSprite = "combo_slam", ComboSlamCore = "combo_slam_core", ComboSlamHitSprite = "combo_slam_hit";
        // #1 Đánh thường hất tung (4 pha, cùng mốc ngực)
        public const string RiseSprite0 = "rise_0", RiseSprite1 = "rise_1", RiseCore1 = "rise_1_core";
        public const string RisingSlashSprite = "slash_v", RisingSlashCore = "slash_v_core", RiseSprite3 = "rise_3";
        // #2 Đánh thường áp sát
        public const string DashSlashSprite = "dash_h", DashSlashCore = "dash_h_core", DashLinesSprite = "dash_lines";
        public const string DustSprite = "dust";
        // #3 Kĩ năng
        public const string SpinBoltSprite = "spin_bolt";
        public const string SpinRingBack = "spin_ring1_back", SpinRingFront = "spin_ring1_front";
        public const string SpinRingsBack = "spin_rings_back", SpinRingsFront = "spin_rings_front";
        public const string CrystalRingBack = "crystal_ring_back", CrystalRingFront = "crystal_ring_front";
        // #4 Kĩ năng tầm xa
        public const string WaveLaunchSprite = "wave_launch", WaveLaunchCore = "wave_launch_core";
        public const string WaveSprite = "wave", WaveTailSprite = "wave_tail", WaveBurstSprite = "wave_burst";
        // #5 Kĩ năng thủ
        public const string MagicCircleBack = "magic_circle_back", MagicCircleFront = "magic_circle_front";
        public const string GuardCracksSprite = "guard_cracks";
        public const string PillarSprite = "pillar", CrystalSprite = "crystal", SmallCrystalSprite = "crystal_small";
        // #6 Kĩ năng đặc biệt
        public const string BigSlashSprite = "slash_big", BigSlashCore = "slash_big_core";
        public const string SecondArcSprite = "db_arc2", SecondArcCore = "db_arc2_core";
        public const string XArmASprite = "x_arm_a", XArmBSprite = "x_arm_b", XDebrisSprite = "x_debris";
        // #7 Kĩ năng đặc biệt tầm xa
        public const string BigWaveLaunchSprite = "wave_big_launch", BigWaveLaunchCore = "wave_big_launch_core";
        public const string BigWaveSprite = "wave_big", BigWaveTailSprite = "wave_big_tail", BigBurstSprite = "burst_big";
        // #8 Kĩ năng đặc biệt áp chế
        public const string ChainSprite = "chain", PunchFlashSprite = "punch_flash";
        public const string CageTopBack = "cage_top_back", CageTopFront = "cage_top_front", CageWrapSprite = "cage_wrap";
        public const string CageSpikesSprite = "cage_spikes", CageBaseBack = "cage_base_back", CageBaseFront = "cage_base_front";

        /// <summary>Mọi ảnh hiệu ứng của RED (test kiểm tra nạp đủ).</summary>
        public static readonly string[] SpriteNames =
        {
            ComboSlashSprite, ComboSlashCore, ComboBackSprite, ComboBackCore, ComboSlamSprite, ComboSlamCore, ComboSlamHitSprite,
            RiseSprite0, RiseSprite1, RiseCore1, RisingSlashSprite, RisingSlashCore, RiseSprite3,
            DashSlashSprite, DashSlashCore, DashLinesSprite, DustSprite,
            SpinBoltSprite, SpinRingBack, SpinRingFront, SpinRingsBack, SpinRingsFront, CrystalRingBack, CrystalRingFront,
            WaveLaunchSprite, WaveLaunchCore, WaveSprite, WaveTailSprite, WaveBurstSprite,
            MagicCircleBack, MagicCircleFront, GuardCracksSprite, PillarSprite, CrystalSprite, SmallCrystalSprite,
            BigSlashSprite, BigSlashCore, SecondArcSprite, SecondArcCore, XArmASprite, XArmBSprite, XDebrisSprite,
            BigWaveLaunchSprite, BigWaveLaunchCore, BigWaveSprite, BigWaveTailSprite, BigBurstSprite,
            ChainSprite, PunchFlashSprite, CageTopBack, CageTopFront, CageWrapSprite, CageSpikesSprite, CageBaseBack, CageBaseFront,
        };

        /// <summary>
        /// Thứ tự vẽ: trước nhân vật (cùng dải hiệu ứng CombatFx; dải nhân vật -6..10), sau lưng nhân vật (&lt; -6), nằm trên
        /// mặt đất.
        /// </summary>
        public const int FrontOrder = 15, BackOrder = -13, GroundOrder = -38;

        const string Folder = "HeroFx/Red/";

        enum Kind : byte { Pop, Wave, Chain, Cage }

        sealed class Item
        {
            public Kind Kind;
            public SpriteRenderer Main, Glow, Tail;
            public string Name;
            public Vector2 Pos, Vel;
            public float Age, Life, Delay;
            public float Facing, Angle, Spin, Sweep;
            public float Scale0, Scale1, Grow, SquashY, StretchX;
            public bool Rise;
            public float FadeIn, FadeOut, GlowPeak, GlowHold, GlowFade, Pulse;
            public Color Tint;
            public Func<Vector2> Follow;
            // sóng bay
            public float Range, Travelled, TailMax;
            public Vector2 Launch;
            public Color Trail;
            public Func<Vector2, bool> Hit;
            public Action<Vector2> End;
            // xích, lồng
            public Func<Vector2> From, To;
            public Func<bool> Alive;
            public Func<Bounds> Target;
            public float Width, Height, Drop;
            public bool Ending, Root, Wrap;

            public void Clear()
            {
                Main = Glow = Tail = null;
                Name = null;
                Follow = From = To = null;
                Hit = null;
                End = null;
                Alive = null;
                Target = null;
                Ending = Root = Wrap = false;
            }
        }

        static RedFx s_instance;

        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        readonly List<Item> _items = new List<Item>();
        readonly Stack<Item> _freeItems = new Stack<Item>();
        readonly Stack<SpriteRenderer> _freeRenderers = new Stack<SpriteRenderer>();
        Material _alpha, _additive;
        bool _ownAlpha, _ownAdditive;

        // Editor tắt domain reload: static còn giữ từ lần Play trước
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        /// <summary>Bộ hiệu ứng của RED trong màn đang chơi (chưa có thì tạo).</summary>
        public static RedFx Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new GameObject("Red Fx").AddComponent<RedFx>();
                return s_instance;
            }
        }

        /// <summary>Số hiệu ứng đang chạy (test).</summary>
        public int ActiveCount => _items.Count;

        /// <summary>Số lồng xích đang giam (test; mỗi lồng gồm nhiều lớp nhưng đếm 1).</summary>
        public int CageCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _items.Count; i++)
                    if (_items[i].Kind == Kind.Cage && _items[i].Root)
                        n++;
                return n;
            }
        }

        /// <summary>Số SpriteRenderer đang bật (test: giữ số renderer hợp lý cho điện thoại).</summary>
        public int RendererCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _items.Count; i++)
                {
                    Item it = _items[i];
                    n += (it.Main != null ? 1 : 0) + (it.Glow != null ? 1 : 0) + (it.Tail != null ? 1 : 0);
                }

                return n;
            }
        }

        /// <summary>
        /// Test: hiệu ứng đang hiện dùng ảnh sprite (đã qua thời gian chờ) — vị trí điểm neo (pivot) của nó trên thế giới.
        /// </summary>
        public bool TryGetActive(string sprite, out Vector2 position) => TryGetActive(sprite, out position, out _);

        /// <summary>Như trên, kèm thứ tự vẽ của lớp chính (test: lớp sau / trước người).</summary>
        public bool TryGetActive(string sprite, out Vector2 position, out int order)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                if (it.Name == sprite && it.Delay <= 0f && it.Main != null && it.Main.transform.localScale.sqrMagnitude > 0f)
                {
                    position = it.Main.transform.position;
                    order = it.Main.sortingOrder;
                    return true;
                }
            }

            position = default;
            order = 0;
            return false;
        }

        /// <summary>Ảnh hiệu ứng theo tên (null nếu thiếu).</summary>
        public Sprite Sprite(string name) => name != null && _sprites.TryGetValue(name, out Sprite s) ? s : null;

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            foreach (string name in SpriteNames)
            {
                Sprite s = Resources.Load<Sprite>(Folder + name);
                if (s != null)
                    _sprites[name] = s;
            }

            // Vật liệu cùng loại với CombatFx (sprite không chịu ánh sáng + cộng sáng), để trong Resources cho chắc có shader khi
            // build; thiếu thì tạo từ shader
            _alpha = Resources.Load<Material>(Folder + "RedFxAlpha");
            _additive = Resources.Load<Material>(Folder + "RedFxAdditive");
            if (_alpha == null)
            {
                Shader alpha = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (alpha == null)
                    alpha = Shader.Find("Sprites/Default");
                if (alpha != null)
                {
                    _alpha = new Material(alpha) { name = "Red Fx Alpha" };
                    _ownAlpha = true;
                }
            }

            if (_additive == null)
            {
                Shader additive = Shader.Find("Legacy Shaders/Particles/Additive");
                if (additive == null)
                    additive = Shader.Find("Mobile/Particles/Additive");
                if (additive != null)
                {
                    _additive = new Material(additive) { name = "Red Fx Additive" };
                    if (_additive.HasProperty("_TintColor"))
                        _additive.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // shader nhân đôi: 0.5 = giữ nguyên màu
                    _ownAdditive = true;
                }
            }
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
            if (_ownAlpha)
                Destroy(_alpha);
            if (_ownAdditive)
                Destroy(_additive);
        }

        // =============================================================================================================
        // Hiệu ứng
        // =============================================================================================================

        /// <summary>
        /// Một ảnh bung ra ở at (theo pivot của ảnh) rồi tắt: lớn từ scale0 → 1 (× scale) trong grow giây (bật nảy), hiện trong
        /// fadeIn, tắt dần trong fadeOut cuối. rise: mọc cao từ gốc (nổ đất, cột sáng). facing = 1 / -1 (lật ngang theo hướng
        /// nhìn). follow: bám theo một điểm (thân người, mục tiêu). stretchX: kéo ngang thêm (vệt chém dài tới trước).
        /// sweep: lúc đầu xoay lệch bấy nhiêu độ rồi quét về angle trong grow giây (vệt đi theo đường kiếm).
        /// Lớp cộng sáng: core = ảnh lõi trắng riêng (null = chính ảnh này), sáng glow lúc đầu, giữ 0.04 giây rồi tắt trong
        /// coreFade giây (lõi tắt trước, thân đỏ còn lại).
        /// </summary>
        public void Pop(string sprite, Vector2 at, float scale, float life, float facing = 1f, int order = FrontOrder,
            float grow = 0.12f, float scale0 = 0.55f, bool rise = false, float glow = 0.7f, float fadeIn = 0.03f,
            float fadeOut = 0.35f, float angle = 0f, float spin = 0f, Vector2 velocity = default, Func<Vector2> follow = null,
            float delay = 0f, float squashY = 1f, Color? tint = null, float pulse = 0f, string core = null, float sweep = 0f,
            float stretchX = 1f, float coreFade = 0.25f)
        {
            Sprite s = Sprite(sprite);
            if (s == null)
                return;
            Sprite glowSprite = core != null ? Sprite(core) : s;
            Item it = Rent(Kind.Pop, sprite, s, order, glow > 0f ? glowSprite : null);
            it.Pos = at;
            it.Vel = velocity;
            it.Life = Mathf.Max(0.05f, life);
            it.Delay = delay;
            it.Facing = facing < 0f ? -1f : 1f;
            it.Angle = angle;
            it.Spin = spin;
            it.Sweep = sweep;
            it.Scale0 = scale * scale0;
            it.Scale1 = scale;
            it.Grow = Mathf.Max(0.01f, grow);
            it.SquashY = squashY;
            it.StretchX = stretchX;
            it.Rise = rise;
            it.FadeIn = fadeIn;
            it.FadeOut = Mathf.Min(fadeOut, it.Life * 0.9f);
            it.GlowPeak = glow;
            it.GlowHold = core != null ? 0.04f : 0.06f;
            it.GlowFade = Mathf.Max(0.02f, coreFade);
            it.Pulse = pulse;
            it.Tint = tint ?? Color.white;
            it.Follow = follow;
            Draw(it, 0f);
        }

        /// <summary>
        /// Sóng lưỡi liềm bay (ảnh có đầu sóng ở pivot) từ from theo hướng dir (±1) với tốc độ speed tối đa range unit; mỗi
        /// frame gọi hit(chỗ đầu sóng) — true là trúng. Trúng hoặc hết tầm: end(chỗ đầu sóng) (nổ), sóng tắt. tail = ảnh đuôi
        /// (pivot cũng ở đầu sóng): dài dần tới tailMax lần theo quãng đã bay. launch = chỗ HÌNH sóng bung ra (mũi kiếm): hình
        /// đi từ đó lên đường bay trong 0.14 giây đầu (chỗ dò trúng vẫn là đường bay from).
        /// </summary>
        public void Wave(string sprite, string tail, Vector2 from, float dir, float speed, float range, float scale, Color trail,
            Func<Vector2, bool> hit, Action<Vector2> end, Vector2? launch = null, float tailMax = 1.7f)
        {
            Sprite s = Sprite(sprite);
            if (s == null)
            {
                end?.Invoke(from);
                return;
            }

            Item it = Rent(Kind.Wave, sprite, s, FrontOrder, s);
            Sprite t = Sprite(tail);
            if (t != null)
                it.Tail = RentRenderer(t, FrontOrder - 1, false);
            it.Pos = from;
            it.Vel = new Vector2((dir < 0f ? -1f : 1f) * speed, 0f);
            it.Facing = dir < 0f ? -1f : 1f;
            it.Range = range;
            it.Scale0 = scale * 0.45f;
            it.Scale1 = scale;
            it.Grow = 0.12f;
            it.SquashY = 1f;
            it.FadeIn = 0.04f;
            it.GlowPeak = 0.35f;
            it.Tint = Color.white;
            it.Trail = trail;
            it.Hit = hit;
            it.End = end;
            it.TailMax = tailMax;
            it.Launch = launch.HasValue ? launch.Value - from : Vector2.zero;
            it.Life = range / Mathf.Max(0.1f, speed) + 1f;
            DrawWave(it, 0f, false); // chưa dò trúng ở frame bắn ra
        }

        /// <summary>
        /// Sợi xích đỏ (ảnh mắt xích lát nối tiếp) từ from() tới to(): phóng ra trong 0.1 giây, căng suốt life giây (alive() trả
        /// false thì thu về sớm). width = bề dày sợi xích (unit).
        /// </summary>
        public void Chain(Func<Vector2> from, Func<Vector2> to, float width, float life, Func<bool> alive = null, float delay = 0f,
            int order = FrontOrder - 1)
        {
            Sprite s = Sprite(ChainSprite);
            if (s == null)
                return;
            Item it = Rent(Kind.Chain, ChainSprite, s, order, s);
            it.Main.drawMode = SpriteDrawMode.Tiled;
            it.Main.tileMode = SpriteTileMode.Continuous;
            it.Glow.drawMode = SpriteDrawMode.Tiled;
            it.Glow.tileMode = SpriteTileMode.Continuous;
            it.From = from;
            it.To = to;
            it.Alive = alive;
            it.Width = width;
            it.Life = life;
            it.Delay = delay;
            it.GlowPeak = 0.4f;
            it.Tint = Color.white;
            Draw(it, 0f);
        }

        /// <summary>
        /// Lồng xích giam mục tiêu có khung target(), tách lớp như tấm (khung 5): vòng phép dưới chân (nửa sau / trước), gai pha
        /// lê hai bên, 3 vòng xích quấn thân (nửa trước vẽ trước mục tiêu, nửa sau = chính nó lật dọc, tối hơn, vẽ sau), vòng xích
        /// + gai đỉnh trên đầu (nửa sau / trước). Lớp mọc lần lượt (vòng phép → gai → xích siết vào → vòng đầu hạ xuống), rực theo
        /// nhịp, tắt sau life giây hoặc khi alive() trả false. Cỡ theo thân mục tiêu (xích không bị kéo méo quá 1.4 lần).
        /// backOrder: thứ tự vẽ các lớp phía sau (phải nhỏ hơn mọi phần hình của mục tiêu).
        /// </summary>
        public void Cage(Func<Bounds> target, float life, Func<bool> alive = null, int backOrder = BackOrder)
        {
            if (Sprite(CageBaseBack) == null)
                return;
            CagePart(CageBaseBack, target, life, alive, 0f, backOrder, 0.3f, 0f, true, true);
            CagePart(CageBaseFront, target, life, alive, 0f, FrontOrder - 3, 0.3f, 0f, false, true);
            CagePart(CageSpikesSprite, target, life, alive, 0.02f, FrontOrder - 4, 0.6f, 0.08f, false, true);
            for (int i = 0; i < 3; i++)
            {
                float v = 0.3f + i * 0.24f;
                CagePart(CageWrapSprite, target, life, alive, v, backOrder + 1, 1.5f, 0.06f + i * 0.05f, false, false, wrap: true,
                    flipY: true, shade: 0.5f);
                CagePart(CageWrapSprite, target, life, alive, v, FrontOrder - 2, 1.5f, 0.06f + i * 0.05f, false, true, wrap: true);
            }

            CagePart(CageTopBack, target, life, alive, 1.04f, backOrder, 1.1f, 0.16f, false, true, drop: 0.5f);
            CagePart(CageTopFront, target, life, alive, 1.04f, FrontOrder - 2, 1.1f, 0.16f, false, true, drop: 0.5f);
        }

        void CagePart(string sprite, Func<Bounds> target, float life, Func<bool> alive, float height, int order, float scale0,
            float delay, bool root, bool glow, bool wrap = false, bool flipY = false, float shade = 1f, float drop = 0f)
        {
            Sprite s = Sprite(sprite);
            if (s == null)
                return;
            Item it = Rent(Kind.Cage, sprite, s, order, glow ? s : null);
            it.Target = target;
            it.Alive = alive;
            it.Life = Mathf.Max(0.1f, life - delay); // tuổi chỉ tính sau khi hết chờ: các lớp tắt cùng lúc
            it.Delay = delay;
            it.Root = root;
            it.Wrap = wrap;
            it.Height = height;
            it.Drop = drop;
            it.Scale0 = scale0;
            it.Grow = 0.22f;
            it.GlowPeak = 0.45f;
            it.Tint = new Color(shade, shade, shade, 1f);
            it.Main.flipY = flipY;
            if (it.Glow != null)
                it.Glow.flipY = flipY;
            Draw(it, 0f);
        }

        /// <summary>
        /// Vòng phép đỏ nằm trên mặt đất ở ground, rộng width unit: nửa sau vẽ sau người (sát đất), nửa trước (mũi gai trước)
        /// vẽ trước chân; xoè ra rồi sáng nhịp nhàng life giây.
        /// </summary>
        public void Circle(Vector2 ground, float width, float life, float delay = 0f, Func<Vector2> follow = null)
        {
            Sprite s = Sprite(MagicCircleBack);
            if (s == null)
                return;
            float scale = width / s.bounds.size.x;
            Pop(MagicCircleBack, ground, scale, life, 1f, GroundOrder, 0.22f, 0.25f, false, 0.6f, 0.05f, 0.3f, follow: follow,
                delay: delay, pulse: 0.3f);
            Pop(MagicCircleFront, ground, scale, life, 1f, FrontOrder - 4, 0.22f, 0.25f, false, 0.6f, 0.05f, 0.3f, follow: follow,
                delay: delay, pulse: 0.3f);
        }

        /// <summary>Cột sáng mảnh vụt lên từ ground cao height rồi tắt.</summary>
        public void Pillar(Vector2 ground, float height, float life, float delay = 0f, int order = BackOrder)
        {
            Sprite s = Sprite(PillarSprite);
            if (s == null)
                return;
            Pop(PillarSprite, ground, height / s.bounds.size.y, life, 1f, order, 0.12f, 0.1f, true, 0.7f, 0.02f, life * 0.6f, delay: delay);
        }

        /// <summary>Pha lê hình thoi hiện ra ở at rồi bay theo velocity, xoay nhẹ, tắt dần.</summary>
        public void Crystal(Vector2 at, float height, Vector2 velocity, float life, float delay = 0f, bool small = false,
            int order = FrontOrder)
        {
            string name = small ? SmallCrystalSprite : CrystalSprite;
            Sprite s = Sprite(name);
            if (s == null)
                return;
            Pop(name, at, height / s.bounds.size.y, life, 1f, order, 0.1f, 0.2f, false, 0.7f, 0.05f, life * 0.5f,
                Random.Range(-8f, 8f), Random.Range(-30f, 30f), velocity, delay: delay);
        }

        // =============================================================================================================
        // Chạy
        // =============================================================================================================

        // Sau coroutine của chiêu: hiệu ứng gọi trong frame này được đặt đúng chỗ trước khi vẽ
        void LateUpdate()
        {
            float frame = Time.deltaTime;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Item it = _items[i];
                float dt = frame;
                if (it.Delay > 0f)
                {
                    it.Delay -= dt;
                    if (it.Delay > 0f)
                    {
                        Hide(it);
                        continue;
                    }

                    dt = -it.Delay;
                    it.Delay = 0f;
                }

                it.Age += dt;
                if (!Draw(it, dt))
                    Finish(i, it);
            }
        }

        bool Draw(Item it, float dt)
        {
            if (it.Delay > 0f)
            {
                Hide(it);
                return true;
            }

            switch (it.Kind)
            {
                case Kind.Wave: return DrawWave(it, dt, true);
                case Kind.Chain: return DrawChain(it);
                case Kind.Cage: return DrawCage(it);
                default: return DrawPop(it);
            }
        }

        bool DrawPop(Item it)
        {
            if (it.Age >= it.Life)
                return false;
            float grow = EaseOutBack(it.Age / it.Grow);
            float scale = Mathf.LerpUnclamped(it.Scale0, it.Scale1, grow);
            float alpha = Envelope(it);
            Vector2 at = it.Follow != null ? it.Follow() : it.Pos + it.Vel * it.Age;
            float sy = it.Rise ? scale * Mathf.Clamp01(grow) : scale;
            float pulse = it.Pulse > 0f ? 1f + it.Pulse * Mathf.Sin(it.Age * 9f) : 1f;
            float sweep = 0f;
            if (it.Sweep != 0f)
            {
                float k = 1f - Mathf.Clamp01(it.Age / it.Grow);
                sweep = it.Sweep * k * k;
            }

            Place(it, at, new Vector2(scale * it.Facing * it.StretchX, sy * it.SquashY), it.Angle + sweep + it.Spin * it.Age, alpha, pulse);
            return true;
        }

        bool DrawWave(Item it, float dt, bool probe)
        {
            Vector2 step = it.Vel * dt;
            it.Pos += step;
            it.Travelled += step.magnitude;
            float grow = EaseOutBack(it.Age / it.Grow);
            float scale = Mathf.LerpUnclamped(it.Scale0, it.Scale1, grow);
            // hình bung từ mũi kiếm rồi lên đường bay (chỗ dò trúng luôn là đường bay)
            float launch = Mathf.Clamp01(it.Age / 0.14f);
            Vector2 at = it.Pos + it.Launch * ((1f - launch) * (1f - launch));
            // nhịp phập phồng nhẹ dọc thân sóng
            float breathe = 1f + 0.05f * Mathf.Sin(it.Age * 30f);
            float alpha = Mathf.Clamp01(it.Age / it.FadeIn);
            Place(it, at, new Vector2(scale * it.Facing * breathe, scale / breathe), 0f, alpha, 1f);
            if (it.Tail != null)
            {
                // đuôi dài dần theo quãng bay (mục tiêu gần thì đuôi ngắn: đọc được đường bay mà không thành tấm ảnh dài)
                float stretch = Mathf.Lerp(0.35f, it.TailMax, Mathf.Clamp01(it.Travelled / (it.Range * 0.3f + 0.01f)));
                Transform t = it.Tail.transform;
                t.position = at;
                t.rotation = Quaternion.identity;
                t.localScale = new Vector3(scale * it.Facing * stretch, scale * (0.92f + 0.08f * Mathf.Sin(it.Age * 23f)), 1f);
                it.Tail.color = new Color(1f, 1f, 1f, alpha * 0.9f);
            }

            CombatFx fx = CombatFx.Instance;
            if (fx != null && dt > 0f)
                fx.SpeedLines(at - new Vector2(it.Facing * scale * 0.6f, 0f), it.Vel, it.Trail);
            if (!probe)
                return true;
            bool hit = it.Hit != null && it.Hit(it.Pos);
            if (hit || it.Travelled >= it.Range)
            {
                it.End?.Invoke(it.Pos);
                return false;
            }

            return true;
        }

        bool DrawChain(Item it)
        {
            if (!it.Ending && (it.Age >= it.Life || (it.Alive != null && !it.Alive())))
            {
                it.Ending = true;
                it.Life = it.Age + 0.12f; // thu về
            }

            if (it.Age >= it.Life)
                return false;
            Vector2 a = it.From(), b = it.To();
            Vector2 d = b - a;
            float full = d.magnitude;
            float reach = it.Ending ? Mathf.Clamp01((it.Life - it.Age) / 0.12f) : Mathf.Clamp01(it.Age / 0.1f);
            reach = 1f - (1f - reach) * (1f - reach);
            float length = Mathf.Max(0.01f, full * reach);
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Sprite s = it.Main.sprite;
            // ảnh mắt xích: chiều cao gốc → bề dày width; lát theo chiều dài (kích thước tính theo đơn vị ảnh gốc)
            float k = it.Width / Mathf.Max(1e-4f, s.bounds.size.y);
            var size = new Vector2(length / k, s.bounds.size.y);
            it.Main.size = size;
            it.Glow.size = size;
            float shake = it.Age < 0.25f ? Mathf.Sin(it.Age * 70f) * 0.04f * (1f - it.Age / 0.25f) : 0f; // căng giật lúc mới bắn
            Transform t = it.Main.transform;
            t.position = a + new Vector2(-d.y, d.x).normalized * shake;
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = new Vector3(k, k, 1f);
            Transform g = it.Glow.transform;
            g.position = t.position;
            g.rotation = t.rotation;
            g.localScale = t.localScale;
            float alpha = it.Ending ? Mathf.Clamp01((it.Life - it.Age) / 0.12f) : 1f;
            it.Main.color = new Color(1f, 1f, 1f, alpha);
            it.Glow.color = new Color(1f, 0.35f, 0.3f, alpha * (0.22f + 0.15f * Mathf.Sin(it.Age * 14f) + (it.Age < 0.15f ? it.GlowPeak : 0f)));
            return true;
        }

        bool DrawCage(Item it)
        {
            if (!it.Ending && (it.Age >= it.Life || (it.Alive != null && !it.Alive())))
            {
                it.Ending = true;
                it.Life = it.Age + 0.3f;
            }

            if (it.Age >= it.Life)
                return false;
            Bounds b = it.Target();
            Vector2 native = it.Main.sprite.bounds.size;
            // cỡ chung theo thân mục tiêu (tấm: hình nộm cao ~2 unit, rộng ~1.3 unit gồm cả xích): bia tập ≈ 1, sư tử to hơn
            float k = Mathf.Clamp(Mathf.Max(b.size.y / 2f, b.size.x / 1.3f), 0.9f, 2.4f);
            float g = EaseOutBack(it.Age / it.Grow);
            float s = Mathf.LerpUnclamped(it.Scale0, 1f, g);
            float sx, sy;
            if (it.Wrap)
            {
                // vòng quấn: ôm vừa bề ngang thân (siết từ rộng vào), không kéo méo mắt xích quá 1.4 lần
                sy = k * 0.9f;
                sx = Mathf.Clamp(b.size.x * 1.12f / native.x, sy * 0.8f, sy * 1.4f) * s;
            }
            else
                sx = sy = k * s;

            float y = b.min.y + it.Height * b.size.y + it.Drop * k * (1f - Mathf.Clamp01(g));
            float alpha = Mathf.Clamp01(it.Age / 0.08f);
            if (it.Ending)
                alpha *= Mathf.Clamp01((it.Life - it.Age) / 0.3f);
            float pulse = 1f + 0.25f * Mathf.Sin(it.Age * 8f);
            Place(it, new Vector2(b.center.x, y), new Vector2(sx, sy), 0f, alpha, pulse);
            return true;
        }

        // Đặt ảnh chính + lớp cộng sáng (loé lúc đầu, giữ GlowHold rồi tắt trong GlowFade; pulse nhân thêm)
        void Place(Item it, Vector2 at, Vector2 scale, float angle, float alpha, float pulse)
        {
            Transform t = it.Main.transform;
            t.position = at;
            t.rotation = Quaternion.Euler(0f, 0f, angle * it.Facing);
            t.localScale = new Vector3(scale.x, scale.y, 1f);
            Color c = it.Tint;
            c.a *= alpha;
            it.Main.color = c;
            if (it.Glow == null)
                return;
            Transform g = it.Glow.transform;
            g.position = at;
            g.rotation = t.rotation;
            g.localScale = t.localScale;
            float flash = it.Age < it.GlowHold ? 1f : Mathf.Clamp01(1f - (it.Age - it.GlowHold) / it.GlowFade);
            float glow = it.GlowPeak * flash + (it.Pulse > 0f || it.Kind == Kind.Cage ? 0.3f * (pulse - 0.7f) : 0f);
            it.Glow.color = new Color(it.Tint.r, it.Tint.g, it.Tint.b, Mathf.Clamp01(glow) * alpha);
        }

        static float Envelope(Item it)
        {
            float a = it.FadeIn > 0f ? Mathf.Clamp01(it.Age / it.FadeIn) : 1f;
            float left = it.Life - it.Age;
            if (it.FadeOut > 0f && left < it.FadeOut)
                a *= Mathf.Clamp01(left / it.FadeOut);
            return a;
        }

        static void Hide(Item it)
        {
            it.Main.transform.localScale = Vector3.zero;
            if (it.Glow != null)
                it.Glow.transform.localScale = Vector3.zero;
            if (it.Tail != null)
                it.Tail.transform.localScale = Vector3.zero;
        }

        // =============================================================================================================
        // Dùng lại đối tượng
        // =============================================================================================================

        Item Rent(Kind kind, string name, Sprite sprite, int order, Sprite glow)
        {
            Item it = _freeItems.Count > 0 ? _freeItems.Pop() : new Item();
            it.Kind = kind;
            it.Name = name;
            it.Main = RentRenderer(sprite, order, false);
            it.Glow = glow != null && _additive != null ? RentRenderer(glow, order + 1, true) : null;
            it.Tail = null;
            it.Pos = it.Vel = it.Launch = Vector2.zero;
            it.Age = it.Delay = it.Angle = it.Spin = it.Sweep = it.Travelled = it.Range = it.Drop = it.Height = 0f;
            it.Facing = 1f;
            it.Scale0 = it.Scale1 = it.StretchX = 1f;
            it.Grow = 0.1f;
            it.SquashY = 1f;
            it.Rise = false;
            it.FadeIn = it.FadeOut = it.GlowHold = it.Pulse = 0f;
            it.GlowFade = 0.25f;
            it.Life = 1f;
            it.Ending = it.Root = it.Wrap = false;
            _items.Add(it);
            return it;
        }

        void Finish(int index, Item it)
        {
            ReturnRenderer(it.Main);
            if (it.Glow != null)
                ReturnRenderer(it.Glow);
            if (it.Tail != null)
                ReturnRenderer(it.Tail);
            it.Clear();
            int last = _items.Count - 1;
            _items[index] = _items[last];
            _items.RemoveAt(last);
            _freeItems.Push(it);
        }

        SpriteRenderer RentRenderer(Sprite sprite, int order, bool additive)
        {
            SpriteRenderer r;
            if (_freeRenderers.Count > 0)
                r = _freeRenderers.Pop();
            else
            {
                r = new GameObject("Red Fx Sprite").AddComponent<SpriteRenderer>();
                r.transform.SetParent(transform, false);
                if (_alpha == null)
                    _alpha = r.sharedMaterial;
            }

            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Simple;
            r.flipX = r.flipY = false;
            Material material = additive ? _additive : _alpha;
            if (material != null)
                r.sharedMaterial = material;
            r.sortingOrder = order;
            r.color = Color.white;
            r.enabled = true;
            r.transform.localScale = Vector3.zero; // chưa đặt chỗ thì chưa hiện
            return r;
        }

        void ReturnRenderer(SpriteRenderer r)
        {
            if (r == null)
                return;
            r.enabled = false;
            r.drawMode = SpriteDrawMode.Simple;
            r.flipY = false;
            _freeRenderers.Push(r);
        }

        static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            return 1f + 2.70158f * x * x * x + 1.70158f * x * x;
        }
    }
}
