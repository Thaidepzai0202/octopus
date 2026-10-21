using System;
using System.Collections.Generic;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hiệu ứng riêng của BLUE, dùng đúng các mảng cắt từ tấm chiêu của người dùng (Resources/HeroFx/Blue, cắt bằng
    /// Tools/art/hero_fx_blue.py). Mỗi chiêu lấy hình từ CHÍNH tấm của nó, tách nhiều lớp: thân cung / vòng (màu chính), lõi
    /// trắng (_Core, vẽ cộng sáng, loé ngắn), lớp TRƯỚC / SAU quanh người (_Front vẽ trước nhân vật, _Back sau lưng), đuôi, tàn
    /// lửa, bụi, đá, nứt đất. Mỗi ảnh có điểm neo (pivot, importer đặt theo manifest.json) và các điểm mốc (ngực, mũi lưỡi hái,
    /// chân… — PointOf) để chiêu đặt hiệu ứng theo đường lưỡi hái thật, không theo tâm người + khoảng cách.
    ///
    /// Mỗi lần phát (Play / PlayAdd) là một Fx: ảnh + cách hiện (nảy to ra, mọc lên từ chân), trôi, xoay, mờ dần, nhấp nháy,
    /// quầng cộng sáng (Glow), bám theo mũi vũ khí (Track: xoay cho mũi lưỡi hái trên tấm trùng mũi lưỡi hái thật trong lúc
    /// vung). Chiêu giữ Fx (qua FxRef: Fx đã trả kho thì ref hết hiệu lực) để dời theo mỗi frame rồi Stop(). Object "Blue FX" tự
    /// tạo trong màn đang chạy khi cần; SpriteRenderer và Fx đều dùng lại từ kho, không tạo / huỷ liên tục, không cấp phát bộ
    /// nhớ mỗi frame.
    /// </summary>
    public class BlueFx : MonoBehaviour
    {
        // Tên ảnh trong Resources/HeroFx/Blue (mỗi nhóm cắt từ tấm của đúng chiêu đó; K = khung trên tấm, trái → phải)
        // #0 Đánh thường
        public const string ReapArcA = "ReapArcA";     // K3: cung quét ngang dài (ngực = điểm neo, mốc "tip" = mũi hái)
        public const string ReapArcB = "ReapArcB";     // K4: lưỡi liềm lớn + dải thấp
        public const string ReapTip = "ReapTip";       // K1/K6: mũi vệt sát đất
        public const string Ember1 = "Ember1", Ember2 = "Ember2", Ember3 = "Ember3"; // tàn lửa
        // #1 Đánh thường hất tung
        public const string RiseLow = "RiseLow";       // K2: quét thấp sát đất
        public const string RiseArc1 = "RiseArc1";     // K3: cung hất (neo chân, chung với Rise)
        public const string Rise = "Rise";             // K4: cung hất dựng đứng lớn
        public const string Pillar = "Pillar";         // K5: lửa hai bên + chân cột
        public const string PillarBeam = "PillarBeam"; // K5: tia sáng giữa cột (kéo cao)
        // #2 Đánh thường áp sát
        public const string DashKick = "DashKick";     // K2: tia lửa đạp đất sau gót
        public const string DashStreak = "DashStreak"; // K3: dải lửa tốc độ kéo sau lưng
        public const string DashArc = "DashArc";       // K4: lưỡi liềm cuối đường lướt
        public const string DashDust = "DashDust";     // K5: bụi + đá lúc trượt dừng
        // #3 Dùng chiêu
        public const string WhirlFlameL = "WhirlFlameL", WhirlFlameR = "WhirlFlameR"; // K2: lửa hai bên chân
        public const string WhirlRing3 = "WhirlRing3"; // K3: vòng lốc (_Front / _Back)
        public const string WhirlRing4 = "WhirlRing4"; // K4: lốc nhiều tầng (_Front / _Back)
        public const string WhirlFinish = "WhirlFinish"; // K5: cung quét mạnh tới trước
        // #4 Dùng chiêu tầm xa
        public const string Circle = "Circle";         // K2: cầu sáng + vòng phép
        public const string WaveSweep = "WaveSweep";   // K3: cung quét chuyển sang sóng bay
        public const string Wave = "Wave";             // K4: sóng lưỡi liềm bay
        public const string Burst = "Burst";           // K5: vụ nổ tinh thể
        // #5 Dùng chiêu thủ
        public const string Seal = "Seal";             // K3: vòng phép dưới chân (_Front / _Back)
        public const string GuardSwirl = "GuardSwirl"; // K4: dải lửa xoắn quanh người (_Front / _Back)
        public const string GuardFlameL = "GuardFlameL", GuardFlameR = "GuardFlameR"; // K3: lưỡi lửa đứng hai bên
        public const string Skull = "Skull";           // K6 / K3 / K5: đầu lâu lửa (5 biến thể)
        public const string SkullA = "SkullA", SkullB = "SkullB", SkullC = "SkullC", SkullD = "SkullD";
        public const string CrystalA = "CrystalA";     // K5: tinh thể bay
        public const string CrystalB = "CrystalB";
        // #6 Kĩ năng đặc biệt
        public const string StormFlameL = "StormFlameL", StormFlameR = "StormFlameR"; // K2: lửa chân
        public const string StormRim = "StormRim";     // K3: vành xoáy
        public const string StormSwirl = "StormSwirl"; // K4: dải xoáy + đá bay (_Front / _Back)
        public const string StormPillarL = "StormPillarL", StormPillarR = "StormPillarR"; // K5: cột tinh thể hai bên
        public const string StormBeam = "StormBeam";   // K5: tia giáng giữa
        public const string StormCrack = "StormCrack"; // K5: nứt đất + bậc đá
        // #7 Kĩ năng đặc biệt tầm xa
        public const string Vortex = "Vortex";         // K2: cầu lửa xoáy
        public const string GroundWave = "GroundWave"; // K3–4: thân luồng lửa (3 biến thể nối liền: GroundWave, B, C)
        public const string GroundWaveB = "GroundWaveB", GroundWaveC = "GroundWaveC";
        public const string TideHead = "TideHead";     // đầu luồng nhọn
        public const string TideStart = "TideStart";   // chỗ luồng bắt đầu (trước lưỡi hái)
        public const string TideSmokeA = "TideSmokeA", TideSmokeB = "TideSmokeB"; // khói trên luồng
        public const string TideRocks = "TideRocks";   // đá vụn dưới luồng
        public const string Eruption = "Eruption";     // K5: tinh thể trồi nổ cuối luồng
        // #8 Kĩ năng đặc biệt áp chế
        public const string Chain = "Chain";           // K2: đoạn xích
        public const string ChainWrap = "ChainWrap";   // K3: xích quấn quanh mục tiêu (_Front / _Back)
        public const string Cage = "Cage";             // K4: lồng xích (_Front: cột giáo, chóp, đế · _Back: xích treo, ánh sáng)

        // Lớp trước / sau người (sẵn tên, khỏi ghép chuỗi lúc chạy)
        public const string WhirlRing3Front = "WhirlRing3_Front", WhirlRing3Back = "WhirlRing3_Back";
        public const string WhirlRing4Front = "WhirlRing4_Front", WhirlRing4Back = "WhirlRing4_Back";
        public const string SealFront = "Seal_Front", SealBack = "Seal_Back";
        public const string GuardSwirlFront = "GuardSwirl_Front", GuardSwirlBack = "GuardSwirl_Back";
        public const string StormSwirlFront = "StormSwirl_Front", StormSwirlBack = "StormSwirl_Back";
        public const string ChainWrapFront = "ChainWrap_Front", ChainWrapBack = "ChainWrap_Back";
        public const string CageFront = "Cage_Front", CageBack = "Cage_Back";

        static readonly Dictionary<string, string> s_cores = new Dictionary<string, string>();

        /// <summary>Tên lớp lõi trắng (_Core) của ảnh name (nhớ sẵn, không ghép chuỗi mỗi lần); null nếu ảnh không có lõi.</summary>
        public static string CoreOf(string name)
        {
            if (!s_cores.TryGetValue(name, out string core))
            {
                core = name + "_Core";
                if (SpriteOf(core) == null)
                    core = null;
                s_cores[name] = core;
            }

            return core;
        }

        /// <summary>Số px ảnh cho 1 unit (Editor/HeroFxImporter_Blue.cs nhập ảnh với cùng số).</summary>
        public const float PixelsPerUnit = 134f;

        /// <summary>Thứ tự vẽ: trước nhân vật / bia tập / boss, và sau lưng nhân vật (sau mọi phần nhân vật -6…10, trước nền).</summary>
        public const int Front = 15, Behind = -14;

        const string Folder = "HeroFx/Blue/";

        /// <summary>Một hiệu ứng ảnh đang phát. Đổi field bất cứ lúc nào trong đời của nó; LateUpdate đặt lại hình.</summary>
        public sealed class Fx
        {
            internal SpriteRenderer Renderer, GlowRenderer;
            internal int Serial;
            internal bool Active;

            /// <summary>
            /// Điểm đặt (world). Mặc định điểm neo (pivot) của ảnh nằm ở đây; gán Anchor = điểm nào của ảnh (0..1 theo khung
            /// ảnh) nằm ở đây thay cho pivot: (0.5, 0.5) giữa, (0.5, 0) chân.
            /// </summary>
            public Vector2 Position;
            public Vector2 Anchor
            {
                get => _anchor;
                set
                {
                    _anchor = value;
                    AtPivot = false;
                }
            }

            Vector2 _anchor;
            internal bool AtPivot;
            /// <summary>Trôi (unit/giây); Drag hãm dần.</summary>
            public Vector2 Velocity;
            public float Drag;
            /// <summary>Bám theo transform này (+ Offset) thay vì Position.</summary>
            public Transform Follow;
            public Vector2 Offset;

            /// <summary>Cỡ (1 = như trên tấm chiêu); Stretch nhân riêng x / y (x âm = lật ngang theo hướng mặt).</summary>
            public float Size;
            public Vector2 Stretch;
            public float Angle, Spin;
            /// <summary>z (world): cùng thứ tự vẽ thì z nhỏ hơn (gần camera) vẽ đè lên — đoạn luồng lửa sau đè đoạn trước.</summary>
            public float Depth;

            /// <summary>Đời (giây), chờ Delay giây mới hiện; hiện dần FadeIn, tắt dần FadeOut giây cuối.</summary>
            public float Age, Life, Delay, FadeIn, FadeOut;
            /// <summary>Lúc đầu cỡ GrowFrom phần, lớn tới đủ trong GrowTime giây (Pop: nảy quá rồi về).</summary>
            public float GrowFrom, GrowTime;
            public bool Pop;
            /// <summary>&gt; 0: mọc lên từ chân trong bấy nhiêu giây (bề cao 0 → đủ, chân đứng yên).</summary>
            public float RiseTime;
            /// <summary>Cỡ lúc hết đời (phần), đổi đều theo đời.</summary>
            public float EndSize;
            /// <summary>Màu nhân (alpha = độ đậm); Glow &gt; 0: thêm một lớp cộng sáng cùng hình, đậm bấy nhiêu.</summary>
            public Color Color;
            public float Glow;
            /// <summary>Nhấp nháy độ đậm (0..1), như lửa.</summary>
            public float Flicker;
            /// <summary>Đập đều độ đậm: giảm tới Pulse phần, PulseRate lần / giây, lệch pha PulsePhase (0..1) — hai lớp lệch pha
            /// nửa vòng thay nhau sáng (lốc xoáy cuộn).</summary>
            public float Pulse, PulseRate, PulsePhase;
            public int Order;
            /// <summary>
            /// Bám mũi vũ khí: trong TrackTime giây đầu, xoay ảnh để điểm mốc TrackPoint của ảnh (unit, so với pivot, ở cỡ 1 chưa
            /// lật) nằm đúng hướng từ chỗ đặt tới Weapon.WeaponTip; lệch khỏi góc ban đầu tối đa TrackLimit độ.
            /// </summary>
            public IHeroVisual Weapon;
            public Vector2 TrackPoint;
            public float TrackTime, TrackLimit = 40f;
            internal float BaseAngle;
            internal bool Additive;

            /// <summary>Tắt dần trong fade giây (từ bây giờ).</summary>
            public void Stop(float fade = 0.15f)
            {
                if (!Active)
                    return;
                float t = Mathf.Max(0f, Age - Delay);
                Life = Mathf.Min(Life, t + fade);
                FadeOut = Mathf.Max(0.0001f, fade);
            }
        }

        /// <summary>Tham chiếu tới một Fx đang phát; hết hiệu lực khi Fx đã tắt và trả về kho (kho dùng lại cho hiệu ứng khác).</summary>
        public readonly struct FxRef
        {
            readonly Fx _fx;
            readonly int _serial;

            public FxRef(Fx fx)
            {
                _fx = fx;
                _serial = fx != null ? fx.Serial : -1;
            }

            public bool Valid => _fx != null && _fx.Active && _fx.Serial == _serial;

            /// <summary>Fx (null nếu đã hết hiệu lực).</summary>
            public Fx Get => Valid ? _fx : null;

            public void Stop(float fade = 0.15f)
            {
                if (Valid)
                    _fx.Stop(fade);
            }
        }

        static BlueFx s_instance;
        static readonly Dictionary<string, Sprite> s_sprites = new Dictionary<string, Sprite>();
        static Material s_alpha, s_additive;
        static bool s_materialsLoaded;

        readonly List<Fx> _live = new List<Fx>(64);
        readonly Stack<Fx> _freeFx = new Stack<Fx>(64);
        readonly Stack<SpriteRenderer> _freeAlpha = new Stack<SpriteRenderer>(64);
        readonly Stack<SpriteRenderer> _freeAdd = new Stack<SpriteRenderer>(32);
        int _serial;

        // Editor tắt domain reload nên static còn từ lần Play trước
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_sprites.Clear();
            s_points.Clear();
            s_cores.Clear();
            s_manifestLoaded = false;
            s_alpha = s_additive = null;
            s_materialsLoaded = false;
        }

        /// <summary>Bộ hiệu ứng của màn đang chạy (tạo khi cần).</summary>
        public static BlueFx Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("Blue FX");
                    s_instance = go.AddComponent<BlueFx>();
                }

                return s_instance;
            }
        }

        /// <summary>Ảnh hiệu ứng theo tên (null nếu thiếu ảnh).</summary>
        public static Sprite SpriteOf(string name)
        {
            if (!s_sprites.TryGetValue(name, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(Folder + name);
                s_sprites[name] = sprite;
            }

            return sprite;
        }

        [Serializable]
        class ManifestPoint
        {
            public string name;
            public float x, y;
        }

        [Serializable]
        class ManifestPiece
        {
            public string name;
            public ManifestPoint[] points;
        }

        [Serializable]
        class Manifest
        {
            public ManifestPiece[] pieces;
        }

        static readonly Dictionary<string, Vector2> s_points = new Dictionary<string, Vector2>();
        static bool s_manifestLoaded;

        /// <summary>
        /// Điểm mốc point của ảnh name (unit, so với điểm neo, ở cỡ 1, chưa lật / xoay): "chest" ngực nhân vật, "tip" mũi lưỡi
        /// hái, "feet" chân, "top" đỉnh, "front" mép trước… theo manifest.json do hero_fx_blue.py ghi. Thiếu thì Vector2.zero.
        /// </summary>
        public static Vector2 PointOf(string name, string point)
        {
            if (!s_manifestLoaded)
            {
                s_manifestLoaded = true;
                var text = Resources.Load<TextAsset>(Folder + "manifest");
                Manifest m = text != null ? JsonUtility.FromJson<Manifest>(text.text) : null;
                if (m?.pieces != null)
                    foreach (ManifestPiece p in m.pieces)
                        if (p.points != null)
                            foreach (ManifestPoint q in p.points)
                                s_points[p.name + "/" + q.name] = new Vector2(q.x, q.y);
            }

            return s_points.TryGetValue(name + "/" + point, out Vector2 v) ? v : Vector2.zero;
        }

        /// <summary>Bề ngang / cao (unit) của ảnh ở cỡ 1.</summary>
        public static Vector2 SizeOf(string name)
        {
            Sprite s = SpriteOf(name);
            return s != null ? (Vector2)s.bounds.size : Vector2.one;
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        /// <summary>
        /// Phát ảnh name ở at, cỡ size (1 = như trên tấm), sống life giây. Trả về Fx để chỉnh thêm (Stretch, Pop, Glow…).
        /// </summary>
        public Fx Play(string name, Vector2 at, float size, float life, int order = Front) => Play(name, at, size, life, order, false);

        /// <summary>Như Play nhưng vẽ cộng sáng (lớp lõi trắng _Core, chớp sáng): sáng lên nền, không che.</summary>
        public Fx PlayAdd(string name, Vector2 at, float size, float life, int order = Front) => Play(name, at, size, life, order, true);

        Fx Play(string name, Vector2 at, float size, float life, int order, bool additive)
        {
            Sprite sprite = SpriteOf(name);
            Fx fx = _freeFx.Count > 0 ? _freeFx.Pop() : new Fx();
            fx.Serial = ++_serial;
            fx.Active = true;
            fx.Position = at;
            fx.Anchor = new Vector2(0.5f, 0.5f);
            fx.AtPivot = true;
            fx.Velocity = Vector2.zero;
            fx.Drag = 0f;
            fx.Follow = null;
            fx.Offset = Vector2.zero;
            fx.Size = size;
            fx.Stretch = Vector2.one;
            fx.Angle = fx.Spin = 0f;
            fx.Depth = 0f;
            fx.Age = fx.Delay = 0f;
            fx.Life = life;
            fx.FadeIn = 0.04f;
            fx.FadeOut = Mathf.Min(0.25f, life * 0.4f);
            fx.GrowFrom = 1f;
            fx.GrowTime = 0f;
            fx.Pop = false;
            fx.RiseTime = 0f;
            fx.EndSize = 1f;
            fx.Color = Color.white;
            fx.Glow = 0f;
            fx.Flicker = 0f;
            fx.Pulse = fx.PulseRate = fx.PulsePhase = 0f;
            fx.Order = order;
            fx.Weapon = null;
            fx.TrackPoint = Vector2.zero;
            fx.TrackTime = 0f;
            fx.TrackLimit = 40f;
            fx.BaseAngle = float.NaN;
            fx.Additive = additive;
            fx.Renderer = Rent(additive ? _freeAdd : _freeAlpha, additive);
            fx.Renderer.sprite = sprite;
            fx.GlowRenderer = null;
            _live.Add(fx);
            return fx;
        }

        SpriteRenderer Rent(Stack<SpriteRenderer> pool, bool additive)
        {
            SpriteRenderer r;
            if (pool.Count > 0)
                r = pool.Pop();
            else
            {
                LoadMaterials();
                var go = new GameObject(additive ? "Blue FX +" : "Blue FX");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                Material m = additive ? s_additive : s_alpha;
                if (m != null)
                    r.sharedMaterial = m;
            }

            r.enabled = false;
            return r;
        }

        static void LoadMaterials()
        {
            if (s_materialsLoaded)
                return;
            s_materialsLoaded = true;
            s_alpha = Resources.Load<Material>(Folder + "BlueFxAlpha");
            s_additive = Resources.Load<Material>(Folder + "BlueFxAdd");
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Fx fx = _live[i];
                fx.Age += dt;
                if (!Draw(fx, dt))
                {
                    Release(fx);
                    _live[i] = _live[_live.Count - 1];
                    _live.RemoveAt(_live.Count - 1);
                }
            }
        }

        void Release(Fx fx)
        {
            fx.Active = false;
            fx.Follow = null;
            fx.Weapon = null;
            fx.Renderer.enabled = false;
            fx.Renderer.sprite = null;
            (fx.Additive ? _freeAdd : _freeAlpha).Push(fx.Renderer);
            if (fx.GlowRenderer != null)
            {
                fx.GlowRenderer.enabled = false;
                fx.GlowRenderer.sprite = null;
                _freeAdd.Push(fx.GlowRenderer);
            }

            fx.Renderer = fx.GlowRenderer = null;
            _freeFx.Push(fx);
        }

        bool Draw(Fx fx, float dt)
        {
            float t = fx.Age - fx.Delay;
            if (t >= fx.Life || fx.Renderer.sprite == null)
                return false;
            if (t < 0f)
            {
                fx.Renderer.enabled = false;
                if (fx.GlowRenderer != null)
                    fx.GlowRenderer.enabled = false;
                return true;
            }

            // Trôi
            if (fx.Follow == null)
            {
                if (fx.Drag > 0f)
                    fx.Velocity *= Mathf.Exp(-fx.Drag * dt);
                fx.Position += fx.Velocity * dt;
            }

            float k = fx.Life > 0f ? t / fx.Life : 1f;
            float grow = 1f;
            if (fx.GrowTime > 0f && t < fx.GrowTime)
            {
                float g = t / fx.GrowTime;
                grow = Mathf.LerpUnclamped(fx.GrowFrom, 1f, fx.Pop ? BackOut(g) : 1f - (1f - g) * (1f - g));
            }

            float size = fx.Size * grow * Mathf.Lerp(1f, fx.EndSize, k);
            var scale = new Vector3(size * fx.Stretch.x, size * fx.Stretch.y, 1f);
            if (fx.RiseTime > 0f && t < fx.RiseTime)
                scale.y *= BackOut(t / fx.RiseTime);

            float alpha = fx.Color.a;
            if (fx.FadeIn > 0f)
                alpha *= Mathf.Clamp01(t / fx.FadeIn);
            if (fx.FadeOut > 0f)
                alpha *= Mathf.Clamp01((fx.Life - t) / fx.FadeOut);
            if (fx.Flicker > 0f)
                alpha *= 1f - fx.Flicker * (0.5f + 0.5f * Mathf.Sin(t * 31f + fx.Serial * 1.7f)) * (0.6f + 0.4f * Mathf.Sin(t * 13f + fx.Serial));
            if (fx.Pulse > 0f)
                alpha *= 1f - fx.Pulse * (0.5f + 0.5f * Mathf.Sin((t * fx.PulseRate + fx.PulsePhase) * 2f * Mathf.PI));

            Vector2 at = fx.Follow != null ? (Vector2)fx.Follow.position + fx.Offset : fx.Position;
            // Bám mũi vũ khí trong lúc vung: xoay cho mốc TrackPoint (đã lật / co giãn) chỉ đúng hướng mũi vũ khí thật
            if (fx.Weapon != null && t <= fx.TrackTime)
            {
                if (float.IsNaN(fx.BaseAngle))
                    fx.BaseAngle = fx.Angle;
                Vector2 tip = fx.Weapon.WeaponTip - at;
                var mark = new Vector2(fx.TrackPoint.x * scale.x, fx.TrackPoint.y * scale.y);
                if (tip.sqrMagnitude > 1e-4f && mark.sqrMagnitude > 1e-6f)
                {
                    float want = Mathf.Atan2(tip.y, tip.x) * Mathf.Rad2Deg - Mathf.Atan2(mark.y, mark.x) * Mathf.Rad2Deg;
                    fx.Angle = fx.BaseAngle + Mathf.Clamp(Mathf.DeltaAngle(fx.BaseAngle, want), -fx.TrackLimit, fx.TrackLimit);
                }
            }

            // Điểm neo của ảnh (pivot, hoặc Anchor theo khung ảnh) đặt đúng vào chỗ đặt, tính cả cỡ và góc quay
            Vector2 local = Vector2.zero;
            if (!fx.AtPivot)
            {
                Rect rect = fx.Renderer.sprite.rect;
                Vector2 pivot = fx.Renderer.sprite.pivot; // px từ góc dưới trái
                float ppu = fx.Renderer.sprite.pixelsPerUnit;
                local = new Vector2((fx.Anchor.x * rect.width - pivot.x) / ppu, (fx.Anchor.y * rect.height - pivot.y) / ppu);
            }

            float angle = fx.Angle + fx.Spin * t;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 offset = rotation * new Vector3(local.x * scale.x, local.y * scale.y, 0f);
            var position = new Vector3(at.x - offset.x, at.y - offset.y, fx.Depth);

            Place(fx.Renderer, position, rotation, scale, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha), fx.Order);
            if (fx.Additive)
                return true;
            if (fx.Glow > 0f)
            {
                if (fx.GlowRenderer == null)
                {
                    fx.GlowRenderer = Rent(_freeAdd, true);
                    fx.GlowRenderer.sprite = fx.Renderer.sprite;
                }

                Place(fx.GlowRenderer, position, rotation, scale, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha * fx.Glow), fx.Order + 1);
            }
            else if (fx.GlowRenderer != null)
                fx.GlowRenderer.enabled = false;

            return true;
        }

        static void Place(SpriteRenderer r, Vector3 position, Quaternion rotation, Vector3 scale, Color color, int order)
        {
            Transform t = r.transform;
            t.SetPositionAndRotation(position, rotation);
            t.localScale = scale;
            r.color = color;
            r.sortingOrder = order;
            r.enabled = true;
        }

        static float BackOut(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            const float s = 1.6f;
            return 1f + x * x * ((s + 1f) * x + s);
        }
    }
}
