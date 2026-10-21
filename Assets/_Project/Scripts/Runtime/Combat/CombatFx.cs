using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Octopus
{
    /// <summary>
    /// Hiệu ứng hình cho các chiêu: vệt chém lưỡi liềm (đầu vệt quét theo cung rồi đuôi thon lại, tắt dần), vết cào 3 vạch,
    /// tia lửa + sao chớp chỗ trúng đòn, sóng tròn / sóng dẹt trên mặt đất, vết nứt sáng + đá văng + cọc đá + lửa tím (nện
    /// đất, kraken), mây mực tím có bọt, xoáy nước, vũng độc, lưới tơ, sợi tơ, xúc tu khổng lồ, đạn tơ, nhện con.
    /// Ảnh là ảnh xám / trắng tô màu lúc chạy (Tools/art/fx_textures.py), gán trong Inspector nên thay được.
    /// Nhẹ cho điện thoại: hạt nằm trong một mảng struct cố định; SpriteRenderer / LineRenderer / Mesh đều dùng lại (không
    /// tạo / huỷ liên tục); không cấp phát bộ nhớ mỗi frame.
    /// </summary>
    public class CombatFx : MonoBehaviour
    {
        [Header("Vật liệu")]
        [Tooltip("Hoà trộn thường (đá, mây mực, tơ, vũng độc)")]
        [SerializeField] Material alphaMaterial;
        [Tooltip("Cộng sáng (tia lửa, chớp, vòng sóng, lửa, vết nứt sáng)")]
        [SerializeField] Material additiveMaterial;
        [Tooltip("Vệt chém lưỡi liềm dày: hoà trộn thường + ảnh vệt vẽ tay trải phẳng (FxSlashPaint)")]
        [SerializeField] Material slashMaterial;
        [Tooltip("Lõi trắng rực của vệt chém dày: cộng sáng, cùng ảnh")]
        [SerializeField] Material slashCoreMaterial;
        [Tooltip("Vệt chém mảnh (vệt phụ, vệt nước của xoáy): ảnh FxSlashThin")]
        [SerializeField] Material thinSlashMaterial;
        [Tooltip("Quầng mờ phía sau vệt chém: ảnh mặt cắt mềm (FxSlash)")]
        [SerializeField] Material haloMaterial;
        [Tooltip("3 vết cào trên một dải (ảnh FxClawPaint), hoà trộn thường")]
        [SerializeField] Material clawMaterial;
        [Tooltip("Lõi trắng rực của vết cào: cộng sáng, cùng ảnh")]
        [SerializeField] Material clawCoreMaterial;
        [Tooltip("Nét trơn (lõi sợi tơ)")]
        [SerializeField] Material plainLine;
        [Tooltip("Nét sáng mềm hai mép, cộng sáng (quầng sợi tơ)")]
        [SerializeField] Material glowLine;

        [Header("Ánh sáng")]
        [SerializeField] Sprite glow;
        [Tooltip("Sao 4 cánh")]
        [SerializeField] Sprite flare;
        [Tooltip("Sao toé nhiều tia: chớp chỗ trúng đòn")]
        [SerializeField] Sprite star;
        [Tooltip("Vệt tia lửa dài (đầu bên phải), kéo dãn theo hướng bay")]
        [SerializeField] Sprite spark;
        [SerializeField] Sprite ring;
        [Tooltip("Chớp tròn có tia (Flash)")]
        [SerializeField] Sprite impact;
        [Tooltip("Sóng dẹt có tia bắn lên, đã dẹt sẵn (tâm ảnh = tâm vòng)")]
        [SerializeField] Sprite shock;

        [Header("Đất đá")]
        [Tooltip("Vết nứt toả ra trên mặt đất (đã dẹt theo phối cảnh)")]
        [SerializeField] Sprite crack;
        [Tooltip("Nền nứt vỡ + mảnh đá bay, nhìn ngang (gốc ở chỗ nổ trên mặt đất)")]
        [SerializeField] Sprite crackBurst;
        [SerializeField] Sprite[] rocks;
        [Tooltip("Cụm cọc đá (gốc ở đáy ảnh)")]
        [SerializeField] Sprite spikes;
        [Tooltip("Lưỡi lửa (gốc ở đáy ảnh)")]
        [SerializeField] Sprite flame;

        [Header("Mây, nước, độc")]
        [Tooltip("Cụm mây cuộn: lõi tối, mép sáng")]
        [SerializeField] Sprite puff;
        [SerializeField] Sprite bubble;
        [Tooltip("Giọt (đầu tròn bên phải, đuôi thon)")]
        [SerializeField] Sprite drop;
        [SerializeField] Sprite swirl;
        [SerializeField] Sprite splat;

        [Header("Tơ nhện")]
        [SerializeField] Sprite web;
        [SerializeField] Sprite cocoon;

        [Header("Khác")]
        [Tooltip("Xúc tu khổng lồ trồi lên (chiêu cuối bạch tuộc)")]
        [SerializeField] Sprite tentacle;
        [Tooltip("Nhện con (chiêu cuối nhện): nhìn ngang, mặt hướng phải, chân chạm đáy ảnh")]
        [SerializeField] Sprite spiderling;
        [Tooltip("Nhện con, dáng bước chân kia: bò thì đổi qua lại với ảnh trên. Trống thì chỉ dùng một ảnh")]
        [SerializeField] Sprite spiderlingStep;
        [Tooltip("Chỗ cụm mắt trên ảnh nhện con (quầng mắt hồng), tính từ tâm ảnh theo bề ngang ảnh; x dương = phía mặt nhìn")]
        [SerializeField] Vector2 spiderlingEyes = new Vector2(0.2f, 0f);

        [Header("Thứ tự vẽ, giới hạn")]
        [SerializeField] int sortingOrder = 14;
        [Tooltip("Hiệu ứng vẽ sau lưng nhân vật (xoáy nước, lửa tím lúc rống, khói độc, quầng sáng sau lưng): sau mọi phần của " +
            "bạch tuộc / nhện (từ -6 trở lên) và bia tập (2), trước nền đá (≈ -90). Dùng từ backOrder - 5 tới backOrder + 2")]
        [SerializeField] int backOrder = -12;
        [Tooltip("Vết nứt nằm trên mặt đất: sau nhân vật, trước nền đá")]
        [SerializeField] int groundOrder = -40;
        [Tooltip("Số hạt tối đa cùng lúc (hết chỗ thì hạt mới bị bỏ)")]
        [SerializeField] int maxParticles = 400;

        const int RibbonColumns = 24;
        const int RibbonRows = 5;
        const int ThreadPoints = 10;
        static readonly Color Dust = new Color(0.5f, 0.42f, 0.62f, 0.5f);
        static readonly Color Violet = new Color(0.45f, 0.25f, 1f);
        static readonly Color Pink = new Color(1f, 0.45f, 0.9f);
        static readonly Color TrailDust = new Color(0.74f, 0.66f, 0.86f, 0.55f);
        static int[] s_ribbonTriangles;
        static Vector2[] s_ribbonUv;

        /// <summary>Một hạt: bay, quay hoặc kéo dài theo hướng bay, đổi cỡ / màu, hiện dần rồi tắt dần.</summary>
        struct Particle
        {
            public SpriteRenderer Renderer;
            public Vector2 Position, Velocity;
            public float Age, Life;
            public float Size, EndSize; // bề ngang (unit) lúc đầu / lúc cuối, đổi nhanh rồi chậm dần
            public float Squash;        // cao / rộng (sóng dẹt trên mặt đất)
            public float Angle, Spin;
            public float Gravity, Drag;
            public float Stretch;       // > 0: quay theo hướng bay, dài thêm theo tốc độ
            public float FadeIn, Hold;  // hiện dần trong FadeIn (phần đời), giữ tới Hold rồi tắt dần
            public Color Color, EndColor;
            public float Unit;          // 1 / bề ngang ảnh (unit)
        }

        /// <summary>Dải sáng (vệt chém, vết cào, vệt thẳng) dựng thành mesh: cột dọc vệt, hàng ngang từ mép trong ra mép ngoài.</summary>
        sealed class Ribbon
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public readonly Vector3[] Vertices = new Vector3[RibbonColumns * RibbonRows];
            public readonly Color[] Colors = new Color[RibbonColumns * RibbonRows];
            public Vector2 Center, End;
            public bool Straight, Claw;
            public float Radius, From, To, Width, Squash;
            public float Age, Life, Sweep, Delay;
            public Color Core, Main, Edge;
            public float Sparks, SparkDebt;
        }

        /// <summary>Ảnh quay trong mặt phẳng rồi bị ép dẹt (xoáy nước nằm trên mặt đất).</summary>
        sealed class Spinner
        {
            public Transform Holder;
            public SpriteRenderer Renderer;
        }

        // Hiệu ứng nhiều phần: hàm nhận tuổi (giây), trả false khi xong
        readonly List<Func<float, bool>> _steps = new List<Func<float, bool>>();
        readonly List<float> _ages = new List<float>();
        readonly Stack<SpriteRenderer> _sprites = new Stack<SpriteRenderer>();
        readonly Stack<LineRenderer> _lines = new Stack<LineRenderer>();
        readonly List<Ribbon> _ribbons = new List<Ribbon>();
        readonly Stack<Ribbon> _freeRibbons = new Stack<Ribbon>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly Stack<Spinner> _spinners = new Stack<Spinner>();
        Particle[] _particles;
        int _particleCount;
        Particle _spare; // nhận thông số khi kho hạt đã đầy (không vẽ)
        Material _defaultSpriteMaterial;

        public static CombatFx Instance { get; private set; }

        public Sprite WebSprite => web;
        public Sprite CocoonSprite => cocoon;

        void Awake() => _particles = new Particle[Mathf.Max(32, maxParticles)];

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in _meshes)
                Destroy(mesh);
            _meshes.Clear();
        }

        // Sau Update và coroutine của chiêu: hiệu ứng vừa gọi trong frame này được đặt đúng chỗ trước khi vẽ
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = _steps.Count - 1; i >= 0; i--)
            {
                _ages[i] += dt;
                if (!_steps[i](_ages[i]))
                {
                    _steps.RemoveAt(i);
                    _ages.RemoveAt(i);
                }
            }

            for (int i = _ribbons.Count - 1; i >= 0; i--)
            {
                Ribbon r = _ribbons[i];
                r.Age += dt;
                if (!DrawRibbon(r, dt))
                {
                    r.Renderer.enabled = false;
                    _freeRibbons.Push(r);
                    _ribbons.RemoveAt(i);
                }
            }

            TickParticles(dt);
        }

        void Add(Func<float, bool> step)
        {
            if (!step(0f))
                return; // xong ngay frame đầu (vd đạn trúng liền): đối tượng đã trả lại kho
            _steps.Add(step);
            _ages.Add(0f);
        }

        // =============================================================================================================
        // Đòn đánh
        // =============================================================================================================

        /// <summary>Vệt chém lưỡi liềm quanh center: đầu vệt quét từ fromDeg tới toDeg (độ, world), đuôi đuổi theo, thon lại rồi tắt.</summary>
        public void Slash(Vector2 center, float radius, float fromDeg, float toDeg, Color color, float width = 0.3f, float life = 0.22f) =>
            Slash(center, radius, fromDeg, toDeg, color, EdgeOf(color), width, life);

        /// <summary>
        /// Vệt chém lưỡi liềm: mép ngoài trắng rực, giữa màu color, mép trong ngả sang edge. width = độ dày chỗ dày nhất (unit);
        /// squash &lt; 1 ép dẹt cung (vòng quanh người nhìn nghiêng); delay = chờ bấy nhiêu giây mới hiện.
        /// </summary>
        public void Slash(Vector2 center, float radius, float fromDeg, float toDeg, Color color, Color edge, float width, float life,
            float squash = 1f, float delay = 0f)
        {
            bool thin = width < 0.3f; // vệt mảnh dùng ảnh vệt mảnh, không cần lõi cộng sáng
            Halo(center, radius, fromDeg, toDeg, color, edge, width, life, squash, delay, false);
            Ribbon r = Arc(center, radius, fromDeg, toDeg, HotOf(color), color, edge, width, life, squash, thin ? thinSlashMaterial : slashMaterial);
            r.Delay = delay;
            r.Sparks = 50f;
            DrawRibbon(r, 0f);
            if (thin || slashCoreMaterial == null)
                return;
            Ribbon core = Arc(center, radius, fromDeg, toDeg, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.2f), new Color(1f, 1f, 1f, 0f),
                width, life, squash, slashCoreMaterial);
            core.Delay = delay;
            core.Renderer.sortingOrder = sortingOrder + 1;
            DrawRibbon(core, 0f);
        }

        /// <summary>Vết cào: marks vạch mảnh song song (thon hai đầu) quét theo cung, vạch sau chậm hơn vạch trước một chút.</summary>
        public void Claw(Vector2 center, float radius, float fromDeg, float toDeg, Color color, Color edge, float width = 0.15f,
            float life = 0.24f, int marks = 3, float gap = 0.17f)
        {
            if (clawMaterial != null)
            {
                // Ảnh có sẵn 3 vết song song: một dải phủ cả cụm vết, tâm dải ở đúng bán kính radius
                float band = gap * (marks - 1) + width * 1.8f;
                Color tint = Color.Lerp(color, edge, 0.2f);
                Ribbon r = Arc(center, radius + band * 0.4f, fromDeg, toDeg, Color.Lerp(color, Color.white, 0.3f), tint, tint, band, life, 1f, clawMaterial);
                r.Claw = true;
                r.Sparks = 35f;
                DrawRibbon(r, 0f);
                if (clawCoreMaterial == null)
                    return;
                var white = new Color(1f, 1f, 1f, 0.55f);
                Ribbon core = Arc(center, radius + band * 0.4f, fromDeg, toDeg, white, white, white, band, life, 1f, clawCoreMaterial);
                core.Claw = true;
                core.Renderer.sortingOrder = sortingOrder + 1;
                DrawRibbon(core, 0f);
                return;
            }

            for (int i = 0; i < marks; i++)
            {
                float offset = (i - (marks - 1) * 0.5f) * gap;
                Halo(center, radius + offset, fromDeg, toDeg, color, edge, width, life, 1f, i * 0.02f, true);
                Ribbon r = Arc(center, radius + offset, fromDeg, toDeg, HotOf(color), color, edge, width, life, 1f);
                r.Claw = true;
                r.Delay = i * 0.02f;
                r.Sparks = i == marks / 2 ? 35f : 0f;
                DrawRibbon(r, 0f);
            }
        }

        // Quầng màu mờ, rộng hơn, nằm sau vệt chính: vệt như phát sáng
        void Halo(Vector2 center, float radius, float fromDeg, float toDeg, Color color, Color edge, float width, float life, float squash,
            float delay, bool claw)
        {
            Ribbon halo = Arc(center, radius + width * 0.1f, fromDeg, toDeg, WithAlpha(color, color.a * 0.45f), WithAlpha(color, color.a * 0.35f),
                WithAlpha(edge, 0f), width * (claw ? 2.2f : 1.7f), life, squash, haloMaterial);
            halo.Delay = delay;
            halo.Claw = claw;
            halo.Renderer.sortingOrder = sortingOrder - 1;
            DrawRibbon(halo, 0f);
        }

        /// <summary>Vệt sáng thẳng from → to (xúc tu phóng, giật kéo): đầu vệt lao tới rồi đuôi đuổi theo.</summary>
        public void Streak(Vector2 from, Vector2 to, Color color, Color edge, float width, float life)
        {
            Ribbon r = RentRibbon(from, HotOf(color), color, edge, width, life, thinSlashMaterial);
            r.Straight = true;
            r.End = to;
            r.Sweep = Mathf.Min(0.08f, life * 0.5f);
            r.Sparks = 30f;
            DrawRibbon(r, 0f);
        }

        /// <summary>
        /// Tia lửa toé ra từ at. Màu sáng: vệt tia lửa kéo dài theo hướng bay + sao chớp ở tâm (chỗ trúng đòn); màu tối: mảnh
        /// đá văng; gravity &lt; 0: bọt sáng nổi lên.
        /// </summary>
        public void Burst(Vector2 at, Color color, int count = 10, float speed = 4f, float size = 0.14f, float life = 0.35f,
            float gravity = 6f, Vector2 bias = default)
        {
            if (gravity < 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 v = (Random.insideUnitCircle + bias) * speed;
                    ref Particle p = ref Emit(bubble, true, sortingOrder + 1, at + Random.insideUnitCircle * 0.2f, v, size * Random.Range(1.2f, 2f),
                        life * Random.Range(0.7f, 1.2f), color);
                    p.Gravity = gravity;
                    p.Drag = 1.5f;
                    p.FadeIn = 0.2f;
                    p.Hold = 0.5f;
                }

                return;
            }

            if (color.maxColorComponent < 0.6f)
            {
                Debris(at, color, count, speed, size * 1.6f, gravity, bias);
                return;
            }

            Color hot = WithAlpha(Color.Lerp(color, Color.white, 0.5f), 1f);
            for (int i = 0; i < count; i++)
            {
                Vector2 v = (Random.insideUnitCircle.normalized + bias).normalized * speed * Random.Range(0.55f, 1.25f);
                ref Particle p = ref Emit(spark, false, sortingOrder + 2, at, v, size * Random.Range(2.4f, 4f), life * Random.Range(0.6f, 1.1f), hot);
                p.EndColor = color;
                p.EndSize = p.Size * 0.5f;
                p.Gravity = gravity;
                p.Drag = 2.5f;
                p.Stretch = 0.07f;
            }

            float flash = Mathf.Min(2f, 0.45f + count * 0.06f + speed * 0.07f);
            ref Particle s = ref Emit(star, true, sortingOrder + 2, at, Vector2.zero, flash, 0.18f, Color.white);
            s.EndSize = flash * 0.35f;
            s.EndColor = color;
            s.Angle = Random.Range(0f, 90f);
            s.Spin = Random.Range(-120f, 120f);
            s.Hold = 0.2f;
            ref Particle g = ref Emit(glow, false, sortingOrder + 1, at, Vector2.zero, flash * 0.7f, 0.25f, WithAlpha(color, 0.55f));
            g.EndSize = flash * 1.2f;
            ref Particle o = ref Emit(ring, false, sortingOrder + 1, at, Vector2.zero, flash * 0.2f, 0.22f, WithAlpha(hot, 0.8f));
            o.EndSize = flash * 0.9f;
        }

        /// <summary>Vệt gió sau lưng khi lao nhanh (gọi mỗi frame trong lúc lao): một vệt mảnh bay ngược hướng vận tốc.</summary>
        public void SpeedLines(Vector2 at, Vector2 velocity, Color color)
        {
            float speed = velocity.magnitude;
            if (speed < 2f)
                return;
            Vector2 back = -velocity / speed;
            Vector2 side = new Vector2(-back.y, back.x);
            for (int i = 0; i < 2; i++)
            {
                ref Particle p = ref Emit(spark, false, sortingOrder - 1, at + back * 0.3f + side * Random.Range(-0.5f, 0.5f),
                    back * speed * 0.25f, Random.Range(0.4f, 0.75f), Random.Range(0.12f, 0.22f), WithAlpha(color, 0.75f));
                p.Stretch = 0.12f;
                p.EndSize = p.Size * 0.4f;
                p.Drag = 4f;
            }
        }

        /// <summary>Mảnh đá văng lên, quay tít rồi rơi.</summary>
        public void Debris(Vector2 at, Color color, int count, float speed, float size, float gravity = 18f, Vector2 bias = default)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = bias == default ? Dir(Random.Range(30f, 150f)) : (Random.insideUnitCircle.normalized + bias).normalized;
                ref Particle p = ref Emit(Rock(), false, sortingOrder + 1, at + new Vector2(Random.Range(-0.25f, 0.25f), 0.05f),
                    dir * speed * Random.Range(0.5f, 1.15f), size * Random.Range(0.55f, 1.25f), Random.Range(0.6f, 0.95f), WithAlpha(color, 1f));
                p.Gravity = Mathf.Max(gravity, 10f);
                p.Spin = Random.Range(-600f, 600f);
                p.Angle = Random.Range(0f, 360f);
                p.Hold = 0.75f;
            }
        }

        /// <summary>Sóng tròn lan ra từ at tới bán kính radius.</summary>
        public void Ring(Vector2 at, float radius, Color color, float life = 0.35f)
        {
            ref Particle p = ref Emit(ring, false, sortingOrder, at, Vector2.zero, radius * 0.4f, life, color);
            p.EndSize = radius * 2f;
            p.FadeIn = 0.05f;
        }

        /// <summary>Sóng dẹt chạy trên mặt đất quanh ground (hai vòng: vòng sáng nhanh, vòng mờ chậm).</summary>
        public void Shockwave(Vector2 ground, float radius, Color color, float life = 0.4f)
        {
            if (shock != null)
            {
                // ảnh vẽ tay đã dẹt sẵn, có tia bắn lên
                ref Particle w = ref Emit(shock, false, sortingOrder, ground, Vector2.zero, radius * 0.5f, life * 1.1f, color);
                w.EndSize = radius * 2.3f;
                w.FadeIn = 0.06f;
                w.Hold = 0.3f;
            }
            else
            {
                ref Particle a = ref Emit(ring, false, sortingOrder, ground, Vector2.zero, radius * 0.4f, life, color);
                a.EndSize = radius * 2f;
                a.Squash = 0.26f;
            }

            ref Particle b = ref Emit(ring, false, sortingOrder - 1, ground, Vector2.zero, radius * 0.3f, life * 1.6f, WithAlpha(color, color.a * 0.45f));
            b.EndSize = radius * 1.5f;
            b.Squash = 0.3f;
        }

        /// <summary>Chớp sáng (quầng + sao nhiều tia xoay) ở at. behind: vẽ sau lưng nhân vật (ánh sáng hắt từ phía sau, không che người).</summary>
        public void Flash(Vector2 at, float size, Color color, float life = 0.25f, bool behind = false)
        {
            Sprite body = impact != null ? impact : glow;
            int order = behind ? backOrder : sortingOrder;
            ref Particle g = ref Emit(body, false, order + 1, at, Vector2.zero, size * 0.7f, life, WithAlpha(color, color.a * 0.75f));
            g.EndSize = size * 1.4f;
            g.FadeIn = 0.08f;
            g.Angle = Random.Range(-20f, 20f);
            ref Particle s = ref Emit(body, true, order + 2, at, Vector2.zero, size * 0.9f, life * 0.8f, new Color(1f, 1f, 1f, 0.8f));
            s.EndSize = size * 0.5f;
            s.EndColor = WithAlpha(color, 0.6f);
            s.Angle = Random.Range(0f, 360f);
            s.Spin = 90f;
            s.Hold = 0.2f;
            ref Particle f = ref Emit(flare, true, order + 2, at, Vector2.zero, size * 0.4f, life * 0.6f, Color.white);
            f.EndSize = size * 0.9f;
            f.Angle = 45f;
        }

        // =============================================================================================================
        // Đất đá: nện đất, kraken
        // =============================================================================================================

        /// <summary>Nện đất: vết nứt sáng toả ra, sóng dẹt trên mặt đất, chớp sáng, đá văng lên, bụi cuộn hai bên, tàn sáng bay.</summary>
        public void GroundSlam(Vector2 ground, float radius, Color glowColor, Color rockColor, int chunks = 8, float power = 1f)
        {
            Cracks(ground, radius, glowColor, 0.9f);
            if (crackBurst != null)
            {
                // Nền nứt vỡ vẽ tay: lớp đá (hoà trộn thường, màu đá ngả tím) + lớp sáng (cộng sáng) ở các khe nứt
                float angle = Random.Range(-4f, 4f);
                ref Particle b = ref Emit(crackBurst, false, sortingOrder - 1, ground, Vector2.zero, radius * 2.2f, 0.75f, Color.Lerp(rockColor, glowColor, 0.35f));
                b.EndSize = radius * 2.7f;
                b.FadeIn = 0.04f;
                b.Hold = 0.45f;
                b.Angle = angle;
                ref Particle l = ref Emit(crackBurst, true, sortingOrder, ground, Vector2.zero, radius * 2.2f, 0.5f, WithAlpha(glowColor, 0.9f));
                l.EndSize = radius * 2.7f;
                l.Hold = 0.25f;
                l.Angle = angle;
            }

            Shockwave(ground, radius * 1.3f, glowColor, 0.4f);
            ref Particle g = ref Emit(glow, false, sortingOrder + 1, ground + Vector2.up * 0.15f, Vector2.zero, radius * 1.4f, 0.3f, WithAlpha(glowColor, 0.55f));
            g.EndSize = radius * 2.2f;
            g.Squash = 0.6f;
            ref Particle s = ref Emit(star, true, sortingOrder + 2, ground + Vector2.up * 0.1f, Vector2.zero, radius * 1.8f, 0.22f, Color.white);
            s.EndSize = radius * 0.6f;
            s.EndColor = glowColor;
            s.Squash = 0.5f;
            s.Hold = 0.2f;
            Debris(ground, rockColor, chunks, 6f * power, 0.3f);
            DustCloud(ground, radius, Dust, 6);
            for (int i = 0; i < 7; i++)
            {
                ref Particle e = ref Emit(glow, false, sortingOrder + 2, ground + new Vector2(Random.Range(-radius, radius) * 0.6f, 0.05f),
                    new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 4f) * power), Random.Range(0.08f, 0.16f), Random.Range(0.5f, 0.9f), HotOf(glowColor));
                e.Gravity = 2f;
                e.Hold = 0.4f;
            }
        }

        /// <summary>Vết nứt sáng toả ra trên mặt đất ở ground, rộng 2.4 × radius, sáng life giây rồi tắt.</summary>
        public void Cracks(Vector2 ground, float radius, Color color, float life)
        {
            ref Particle c = ref Emit(crack, true, groundOrder, ground, Vector2.zero, radius * 2f, life, color);
            c.EndSize = radius * 2.6f;
            c.FadeIn = 0.04f;
            c.Hold = 0.45f;
            ref Particle d = ref Emit(crack, true, sortingOrder - 4, ground, Vector2.zero, radius * 2f, life * 0.6f, WithAlpha(color, 0.55f));
            d.EndSize = radius * 2.6f;
            d.Hold = 0.2f;
        }

        /// <summary>Bụi cuộn sang hai bên từ ground.</summary>
        public void DustCloud(Vector2 ground, float radius, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                ref Particle p = ref Emit(puff, false, sortingOrder - 1, ground + new Vector2(side * Random.Range(0f, radius * 0.4f), 0.1f),
                    new Vector2(side * Random.Range(1f, 2.8f), Random.Range(0.2f, 0.8f)), Random.Range(0.35f, 0.55f), Random.Range(0.6f, 0.95f), color);
                p.Drag = 3f;
                p.EndSize = p.Size * 2.2f;
                p.FadeIn = 0.1f;
                p.Hold = 0.25f;
                p.Angle = Random.Range(0f, 360f);
                p.Spin = Random.Range(-40f, 40f);
            }
        }

        /// <summary>Đất vỡ dưới chân xúc tu khổng lồ: cọc đá trồi lên, lửa tím cháy ở chân, vết nứt, đá văng, sóng dẹt.</summary>
        public void Eruption(Vector2 ground, float width, Color glowColor, Color rockColor, float life)
        {
            Spikes(ground, width, Color.Lerp(rockColor, Color.black, 0.35f), life);
            Flames(ground, width, glowColor, life);
            GroundSlam(ground, width * 0.85f, glowColor, rockColor, 6, 1.2f);
        }

        /// <summary>Cụm cọc đá nhọn trồi vọt lên ở ground (rộng width), đứng một lúc rồi lún xuống.</summary>
        public void Spikes(Vector2 ground, float width, Color color, float life)
        {
            if (spikes == null)
                return;
            SpriteRenderer r = RentSprite(spikes, sortingOrder - 1); // trước chân xúc tu khổng lồ và lửa
            float scale = width * 1.3f / spikes.bounds.size.x;
            float flip = Random.value < 0.5f ? -1f : 1f;
            Color tint = WithAlpha(color, 1f);
            Add(age =>
            {
                float k = age / life;
                if (k >= 1f)
                    return Return(r);
                float rise = age < 0.12f ? EaseOutBack(age / 0.12f) : k > 0.7f ? 1f - Mathf.SmoothStep(0f, 1f, (k - 0.7f) / 0.3f) : 1f;
                Transform t = r.transform;
                t.position = ground + Vector2.down * 0.08f;
                t.rotation = Quaternion.identity;
                t.localScale = new Vector3(scale * flip, scale * rise, 1f);
                r.color = tint;
                return true;
            });
        }

        /// <summary>Lửa cháy liếm lên ở ground (rộng width) trong life giây. behind: cháy sau lưng nhân vật (hào quang, không che người).</summary>
        public void Flames(Vector2 ground, float width, Color color, float life, bool behind = false)
        {
            int order = behind ? backOrder : sortingOrder - 2;
            float debt = 0f;
            float last = 0f;
            Add(age =>
            {
                if (age >= life)
                    return false;
                float dt = age - last;
                last = age;
                float k = age / life;
                debt += dt * 34f * Mathf.Clamp01(age * 10f) * Mathf.Clamp01((1f - k) * 3f) * Mathf.Max(1f, width);
                while (debt >= 1f)
                {
                    debt -= 1f;
                    bool hot = Random.value < 0.3f; // lõi sáng cộng màu, còn lại lửa tím đậm
                    ref Particle p = ref Emit(flame, hot, order, ground + new Vector2(Random.Range(-0.5f, 0.5f) * width, -0.1f),
                        new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1.3f)), width * Random.Range(hot ? 0.25f : 0.35f, hot ? 0.45f : 0.7f),
                        Random.Range(0.35f, 0.55f), hot ? HotOf(color) : color);
                    p.EndSize = p.Size * 0.4f;
                    p.FadeIn = 0.15f;
                    p.Hold = 0.3f;
                    p.Angle = Random.Range(-12f, 12f);
                }

                return true;
            });
        }

        // =============================================================================================================
        // Mây mực, nước, độc
        // =============================================================================================================

        /// <summary>
        /// Đám mây (mực) cuộn ra quanh at trong life giây: cụm mây lõi tối mép sáng, quầng sáng bên trong, bọt nổi lên.
        /// behind: cuộn sau lưng nhân vật (không che người).
        /// </summary>
        public void Cloud(Vector2 at, float radius, Color color, float life, int puffs = 14, bool behind = false)
        {
            int order = behind ? backOrder : sortingOrder - 1;
            var parts = new SpriteRenderer[puffs];
            var offset = new Vector2[puffs];
            var size = new float[puffs];
            var angle = new float[puffs];
            var spin = new float[puffs];
            float unit = Unit(puff);
            for (int i = 0; i < puffs; i++)
            {
                parts[i] = RentSprite(puff, order);
                Vector2 o = Random.insideUnitCircle;
                offset[i] = new Vector2(o.x * radius * 0.8f, o.y * radius * 0.5f + radius * 0.12f);
                size[i] = radius * Random.Range(0.45f, 1.05f);
                angle[i] = Random.Range(0f, 360f);
                spin[i] = Random.Range(-25f, 25f);
            }

            SpriteRenderer core = RentSprite(glow, order - 1, true);
            float glowUnit = Unit(glow);
            Color light = Color.Lerp(color, Color.white, 0.4f);
            float bubbles = 0f;
            float last = 0f;
            Add(age =>
            {
                float k = age / life;
                if (k >= 1f)
                {
                    foreach (SpriteRenderer p in parts)
                        Return(p);
                    return Return(core);
                }

                float dt = age - last;
                last = age;
                float billow = 1f - Cube(1f - Mathf.Clamp01(age / 0.35f));
                float fade = Mathf.Clamp01(age * 12f) * Mathf.Clamp01((1f - k) * 4f);
                for (int i = 0; i < puffs; i++)
                {
                    Vector2 drift = offset[i] * (0.35f + 0.65f * billow + 0.15f * k) + Vector2.up * (0.2f * k);
                    float s = size[i] * (0.45f + 0.55f * billow + 0.15f * k);
                    // cụm nhỏ (ở mép) nhạt hơn: đám mây có chiều sâu
                    Place(parts[i], at + drift, s * unit, WithAlpha(color, color.a * fade * (0.55f + 0.45f * size[i] / radius)), angle[i] + spin[i] * age);
                }

                float pulse = 0.85f + 0.15f * Mathf.Sin(age * 8f);
                Place(core, at + Vector2.up * 0.1f, radius * 2.2f * billow * glowUnit, WithAlpha(light, 0.35f * fade * pulse));
                bubbles += dt * 22f * fade;
                while (bubbles >= 1f)
                {
                    bubbles -= 1f;
                    Vector2 o = Random.insideUnitCircle;
                    ref Particle b = ref Emit(bubble, true, order + 1, at + new Vector2(o.x * radius * 0.7f, o.y * radius * 0.4f),
                        new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.4f, 0.9f)), Random.Range(0.1f, 0.26f), Random.Range(0.6f, 1f), light);
                    b.FadeIn = 0.25f;
                    b.Hold = 0.6f;
                }

                return true;
            });
        }

        /// <summary>Phun giọt (mực, nước, độc) từ at theo hướng dir, toả hình nón ±spread độ.</summary>
        public void Spray(Vector2 at, Vector2 dir, Color color, int count, float speed, float spread = 35f, float size = 0.18f,
            bool additive = false, float gravity = 8f)
        {
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                Vector2 v = Dir(baseAngle + Random.Range(-spread, spread)) * speed * Random.Range(0.45f, 1.1f);
                ref Particle p = ref Emit(drop, additive, sortingOrder + 1, at, v, size * Random.Range(0.6f, 1.3f), Random.Range(0.35f, 0.6f), color);
                p.Gravity = gravity;
                p.Stretch = 0.05f;
                p.Drag = 1f;
                p.Hold = 0.5f;
            }
        }

        /// <summary>Bong bóng bóng loáng nổi lên quanh at.</summary>
        public void Bubbles(Vector2 at, float radius, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                ref Particle p = ref Emit(bubble, true, sortingOrder + 1, at + Random.insideUnitCircle * radius,
                    new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1.1f)), Random.Range(0.1f, 0.24f), Random.Range(0.6f, 1.1f), color);
                p.FadeIn = 0.2f;
                p.Hold = 0.6f;
                p.EndSize = p.Size * 1.2f;
            }
        }

        /// <summary>Độc bắn toé ở at: vũng toé + quầng sáng + giọt văng + bọt.</summary>
        public void Splash(Vector2 at, Color color, float size = 0.8f)
        {
            float angle = Random.Range(0f, 360f);
            ref Particle a = ref Emit(splat, false, sortingOrder + 1, at, Vector2.zero, size * 0.4f, 0.55f, WithAlpha(color, 0.9f));
            a.EndSize = size;
            a.Angle = angle;
            a.Hold = 0.45f;
            ref Particle g = ref Emit(splat, true, sortingOrder + 2, at, Vector2.zero, size * 0.5f, 0.35f, WithAlpha(color, 0.7f));
            g.EndSize = size * 1.15f;
            g.Angle = angle;
            Spray(at, Vector2.up, color, 8, 4.5f, 80f, 0.15f, true, 9f);
            Bubbles(at, size * 0.3f, color, 3);
        }

        /// <summary>
        /// Xoáy nước quanh at trong life giây: các tầng xoáy quay dẹt thành phễu, vệt nước lượn quanh, nước bắn, gợn sóng dưới
        /// chân. Phần lớn xoáy vẽ sau lưng nhân vật để thấy rõ nhân vật xoay tít giữa xoáy; chỉ một lớp xoáy mờ ở chân, vài vệt
        /// nước mảnh lượn thấp và giọt nước ở nửa vòng phía trước (ngoài thân người) là vẽ đè lên.
        /// </summary>
        public void Vortex(Vector2 at, float radius, Color color, float life)
        {
            const int layers = 4;
            var spin = new Spinner[layers + 1];
            for (int i = 0; i < layers; i++)
                spin[i] = RentSpinner(swirl, backOrder - 4 + i);
            spin[layers] = RentSpinner(swirl, sortingOrder - 1); // bản mờ của tầng đáy, trước chân nhân vật
            float swirlUnit = Unit(swirl);
            Color core = Color.Lerp(color, Color.white, 0.75f);
            Color edge = Color.Lerp(color, Violet, 0.5f);
            Color foam = Color.Lerp(color, Color.white, 0.6f);
            Vector2 ground = at + Vector2.down * 0.45f;
            float arcs = 0f, spray = 0f, ripples = 0.9f, last = 0f;
            Add(age =>
            {
                float k = age / life;
                if (k >= 1f)
                {
                    foreach (Spinner s in spin)
                        Return(s);
                    return false;
                }

                float dt = age - last;
                last = age;
                float fade = Mathf.Clamp01(age * 5f) * Mathf.Clamp01((1f - k) * 5f);
                float grow = 1f - (1f - Mathf.Clamp01(age / 0.3f)) * (1f - Mathf.Clamp01(age / 0.3f));
                for (int i = 0; i <= layers; i++)
                {
                    // tầng dưới hẹp, tầng trên rộng: thành cái phễu
                    int tier = i < layers ? i : 0;
                    Spinner s = spin[i];
                    s.Holder.position = at + Vector2.up * (-0.4f + tier * 0.62f);
                    s.Holder.localScale = new Vector3(1f, 0.32f, 1f);
                    s.Renderer.transform.localScale = Vector3.one * (radius * (0.5f + 0.22f * tier) * 2f * grow * swirlUnit);
                    s.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, age * (560f - 80f * tier));
                    s.Renderer.color = WithAlpha(tier % 2 == 0 ? color : Color.Lerp(color, Color.white, 0.35f), (i < layers ? 0.8f : 0.26f) * fade);
                }

                arcs += dt * 24f * fade;
                while (arcs >= 1f)
                {
                    arcs -= 1f;
                    // 1/4 số vệt lượn ở nửa vòng phía trước, thấp quanh chân, mảnh và mờ; còn lại sau lưng nhân vật
                    bool front = Random.value < 0.25f;
                    float h = front ? Random.Range(0f, 0.3f) : Random.value;
                    float from = front ? Random.Range(195f, 230f) : Random.Range(0f, 360f);
                    float r = radius * (front ? Random.Range(0.65f, 0.95f) : Mathf.Lerp(0.3f, 0.95f, h)) * grow;
                    float a = front ? 0.5f * fade : fade;
                    Ribbon rb = Arc(at + Vector2.up * Mathf.Lerp(-0.45f, 1.9f, h), r, from, from + (front ? Random.Range(110f, 150f) : Random.Range(150f, 260f)),
                        WithAlpha(core, a), WithAlpha(color, a), WithAlpha(edge, a), front ? Random.Range(0.1f, 0.18f) : Random.Range(0.14f, 0.28f),
                        Random.Range(0.3f, 0.45f), 0.3f, thinSlashMaterial);
                    rb.Sweep = rb.Life * 0.6f;
                    if (!front)
                        rb.Renderer.sortingOrder = backOrder + 1;
                    DrawRibbon(rb, 0f);
                }

                spray += dt * 30f * fade;
                while (spray >= 1f)
                {
                    spray -= 1f;
                    float a = Random.Range(0f, 2f * Mathf.PI);
                    float h = Random.Range(-0.3f, 1.3f);
                    float r = radius * Mathf.Lerp(0.45f, 0.95f, (h + 0.3f) / 1.6f) * grow;
                    var p0 = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.3f + h);
                    var tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a) * 0.3f).normalized;
                    bool front = Mathf.Sin(a) < 0f && Mathf.Abs(p0.x) > 0.9f; // nửa vòng phía trước, ngoài thân người
                    ref Particle p = ref Emit(drop, true, front ? sortingOrder + 1 : backOrder + 2, at + p0,
                        tangent * Random.Range(3f, 5f) + Vector2.up * Random.Range(1f, 3f), Random.Range(0.1f, 0.2f), Random.Range(0.3f, 0.5f), foam);
                    p.Gravity = 9f;
                    p.Stretch = 0.04f;
                }

                ripples += dt * 3.5f * fade;
                while (ripples >= 1f)
                {
                    ripples -= 1f;
                    ref Particle p = ref Emit(ring, false, backOrder - 5, ground, Vector2.zero, radius * 0.5f, 0.7f, WithAlpha(color, 0.6f));
                    p.EndSize = radius * 2.6f;
                    p.Squash = 0.22f;
                }

                return true;
            });
        }

        // =============================================================================================================
        // Tơ nhện
        // =============================================================================================================

        /// <summary>
        /// Lưới tơ bung ra ở at (từ from nếu có: lưới bay từ from tới at trong lúc bung), bật nảy rồi đứng yên. glowColor:
        /// sợi tơ phát sáng màu đó (lớp lưới cộng sáng phía sau + quầng), không có thì chỉ quầng nhạt màu tơ.
        /// </summary>
        public void WebNet(Vector2 at, float radius, Color color, float life, Vector2? from = null, float spread = 0.25f, Color? glowColor = null)
        {
            SpriteRenderer r = RentSprite(web, sortingOrder);
            SpriteRenderer halo = RentSprite(glow, sortingOrder - 1);
            SpriteRenderer shine = glowColor.HasValue ? RentSprite(web, sortingOrder - 1, true) : null;
            Color tint = glowColor ?? color;
            float unit = Unit(web);
            float glowUnit = Unit(glow);
            Vector2 start = from ?? at;
            float spin = Random.Range(0f, 45f);
            bool opened = false;
            Add(age =>
            {
                float k = age / life;
                if (k >= 1f)
                {
                    Return(halo);
                    if (shine != null)
                        Return(shine);
                    return Return(r);
                }

                float open = Mathf.Clamp01(age / spread);
                float pop = EaseOutBack(open);
                float fade = Mathf.Min(1f, (1f - k) * 4f);
                Vector2 p = Vector2.Lerp(start, at, 1f - Cube(1f - open));
                float wobble = 8f * Mathf.Sin(age * 14f) * Mathf.Exp(-age * 5f);
                Place(r, p, radius * 2f * (0.15f + 0.85f * pop) * unit, WithAlpha(color, color.a * fade), spin + wobble);
                Place(halo, p, radius * 2.2f * pop * glowUnit, WithAlpha(tint, (shine != null ? 0.35f : 0.22f) * fade * (1f - 0.6f * open)));
                if (shine != null)
                    Place(shine, p, radius * 2.06f * (0.15f + 0.85f * pop) * unit, WithAlpha(tint, tint.a * 0.7f * fade), spin + wobble);
                if (!opened && open >= 1f)
                {
                    opened = true;
                    Silk(at, radius, color, 10);
                }

                return true;
            });
        }

        /// <summary>Sợi tơ trắng bắn toả ra từ at (lưới bung, kén nổ).</summary>
        public void Silk(Vector2 at, float radius, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Dir(i * 360f / count + Random.Range(-12f, 12f));
                ref Particle p = ref Emit(spark, true, sortingOrder + 1, at + dir * radius * 0.2f, dir * radius * Random.Range(3f, 5f),
                    Random.Range(0.25f, 0.4f), Random.Range(0.3f, 0.45f), color);
                p.Drag = 4f;
                p.Stretch = 0.08f;
            }

            for (int i = 0; i < count / 3; i++)
            {
                ref Particle p = ref Emit(web, false, sortingOrder + 1, at + Random.insideUnitCircle * radius * 0.4f,
                    Random.insideUnitCircle * radius * 2.5f + Vector2.up, Random.Range(0.2f, 0.35f), Random.Range(0.5f, 0.8f), WithAlpha(color, 0.8f));
                p.Gravity = 3f;
                p.Drag = 2f;
                p.Spin = Random.Range(-360f, 360f);
                p.Hold = 0.4f;
            }
        }

        /// <summary>Sợi tơ / dây nối from() → to() trong life giây (alive() trả false thì tắt sớm): bắn ra, rung rồi căng.</summary>
        public void Thread(Func<Vector2> from, Func<Vector2> to, Color color, float width, float life, Func<bool> alive = null)
        {
            LineRenderer line = RentLine(plainLine, ThreadPoints, sortingOrder - 2);
            LineRenderer halo = RentLine(glowLine != null ? glowLine : plainLine, ThreadPoints, sortingOrder - 3);
            Add(age =>
            {
                if (age >= life || (alive != null && !alive()))
                {
                    Return(halo);
                    return Return(line);
                }

                Vector2 a = from();
                Vector2 d = to() - a;
                Vector2 n = new Vector2(-d.y, d.x).normalized;
                float reach = Mathf.Clamp01(age / 0.07f);
                float wobble = Mathf.Exp(-age * 7f) * Mathf.Sin(age * 45f) * Mathf.Min(0.35f, d.magnitude * 0.06f);
                for (int i = 0; i < ThreadPoints; i++)
                {
                    float s = i / (ThreadPoints - 1f);
                    Vector2 p = a + d * (s * reach) + n * (Mathf.Sin(Mathf.PI * s) * wobble);
                    line.SetPosition(i, p);
                    halo.SetPosition(i, p);
                }

                float alpha = color.a * Mathf.Min(1f, (life - age) * 8f);
                line.widthMultiplier = width;
                line.startColor = line.endColor = WithAlpha(color, alpha);
                halo.widthMultiplier = width * 6f;
                halo.startColor = halo.endColor = WithAlpha(color, 0.35f * alpha);
                return true;
            });
        }

        // =============================================================================================================
        // Xúc tu, đạn, nhện con
        // =============================================================================================================

        /// <summary>Xúc tu khổng lồ trồi lên từ mặt đất ở ground, cao height, đứng life giây rồi chìm xuống.</summary>
        public void Tentacle(Vector2 ground, float height, float life, float lean)
        {
            SpriteRenderer r = RentSprite(tentacle, sortingOrder - 3);
            float spriteHeight = tentacle != null ? tentacle.bounds.size.y : 1f;
            float scale = height / spriteHeight;
            Add(age =>
            {
                float k = age / life;
                if (k >= 1f)
                    return Return(r);

                float rise = k < 0.15f ? EaseOutBack(k / 0.15f) : k > 0.75f ? Mathf.SmoothStep(1f, 0f, (k - 0.75f) / 0.25f) : 1f;
                float sway = lean + 10f * Mathf.Sin(age * 7f);
                // Ảnh xúc tu: gốc ở trên, ngọn ở dưới → lật dọc để ngọn chĩa lên trời, gốc cắm ở mặt đất
                Transform t = r.transform;
                t.localScale = new Vector3(scale * 1.1f, -scale * rise, 1f);
                t.rotation = Quaternion.Euler(0f, 0f, sway);
                t.position = ground + (Vector2)(t.rotation * Vector2.up) * (spriteHeight * scale * rise * 0.5f);
                r.color = Color.white;
                return true;
            });
        }

        /// <summary>
        /// Viên đạn bay từ from với vận tốc velocity tối đa range unit; mỗi frame gọi hit(vị trí) — trả true là trúng, dừng.
        /// </summary>
        public void Projectile(Vector2 from, Vector2 velocity, float range, Color color, float size, Func<Vector2, bool> hit, bool useWeb = true)
        {
            SpriteRenderer r = RentSprite(useWeb ? web : glow, sortingOrder, !useWeb);
            SpriteRenderer core = RentSprite(glow, sortingOrder + 1, true);
            float unit = Unit(r.sprite);
            float glowUnit = Unit(glow);
            Vector2 position = from;
            Vector2 back = -velocity.normalized;
            float travelled = 0f;
            float trail = 0f;
            Add(age =>
            {
                float dt = Time.deltaTime;
                position += velocity * dt;
                travelled += velocity.magnitude * dt;
                Place(r, position, size * unit, color, age * 720f);
                Place(core, position, size * 1.3f * glowUnit, WithAlpha(color, 0.75f));
                trail += dt * 70f;
                while (trail >= 1f)
                {
                    trail -= 1f;
                    ref Particle p = ref Emit(spark, true, sortingOrder - 1, position + back * size * 0.3f + Random.insideUnitCircle * size * 0.25f,
                        velocity * 0.2f + back * Random.Range(0.5f, 1.5f), size * Random.Range(0.6f, 1f), Random.Range(0.12f, 0.2f), WithAlpha(color, 0.8f));
                    p.Stretch = 0.03f;
                    p.EndSize = p.Size * 0.3f;
                }

                if (travelled > range || hit(position))
                {
                    Return(core);
                    return Return(r);
                }

                return true;
            });
        }

        /// <summary>
        /// Nhện con chui lên ở from rồi bò dọc mặt đất theo hướng dir (±1) tối đa range unit, ngang size unit; mỗi frame gọi
        /// hit(vị trí) — true là đã cắn trúng: nhảy bám lên mục tiêu gặm một lúc rồi rơi ra, mờ đi. Cho dễ thấy trên nền hang
        /// tối: quầng hồng sau lưng, mắt hồng sáng, bóng dưới chân, bụi đất tung sau lưng.
        /// </summary>
        public void Crawler(Vector2 from, float dir, float speed, float range, Func<Vector2, bool> hit, float size = 0.85f)
        {
            const float Emerge = 0.14f, Bite = 0.32f, Drop = 0.25f;
            SpriteRenderer r = RentSprite(spiderling, sortingOrder);
            SpriteRenderer halo = RentSprite(glow, sortingOrder - 1, true);
            SpriteRenderer eyes = RentSprite(glow, sortingOrder + 1, true);
            SpriteRenderer shadow = RentSprite(glow, sortingOrder - 3);
            float unit = Unit(spiderling), glowUnit = Unit(glow);
            float aspect = spiderling != null ? spiderling.bounds.size.y / spiderling.bounds.size.x : 0.6f;
            float stride = size * 0.2f; // đổi dáng chân sau mỗi bấy nhiêu unit bò được
            Vector2 position = from;
            Vector2 latch = Vector2.zero, fall = Vector2.zero;
            float travelled = 0f;
            float seed = Random.value * 10f;
            float dust = Random.value;
            float bitten = -1f; // tuổi lúc cắn trúng
            Add(age =>
            {
                float dt = Time.deltaTime;
                float pop = EaseOutBack(age / Emerge);  // chui lên khỏi mặt đất
                float alpha = 1f;
                float gait = Mathf.Sin((age + seed) * 24f);
                Vector2 body;
                float tilt;
                if (bitten < 0f)
                {
                    float step = speed * dt;
                    position.x += dir * step;
                    travelled += step;
                    alpha = Mathf.Clamp01((range - travelled) / 0.6f); // hết tầm thì mờ đi
                    body = position + Vector2.up * (size * aspect * 0.45f * pop + Mathf.Abs(gait) * 0.05f * size - 0.12f * (1f - Mathf.Clamp01(age / Emerge)));
                    tilt = gait * 4f * dir;
                    Step(Mathf.FloorToInt(travelled / stride));

                    // Bụi đất tung sau lưng
                    dust += dt * 14f;
                    while (dust >= 1f)
                    {
                        dust -= 1f;
                        ref Particle p = ref Emit(puff, false, sortingOrder - 2, position + new Vector2(-dir * size * 0.3f, 0.06f),
                            new Vector2(-dir * Random.Range(0.3f, 0.9f), Random.Range(0.15f, 0.5f)), size * Random.Range(0.2f, 0.32f),
                            Random.Range(0.4f, 0.55f), WithAlpha(TrailDust, TrailDust.a * alpha));
                        p.EndSize = p.Size * 2.2f;
                        p.Drag = 2.5f;
                        p.FadeIn = 0.1f;
                        p.Angle = Random.Range(0f, 360f);
                        p.Spin = Random.Range(-60f, 60f);
                    }

                    if (travelled > range)
                        return Finish();
                    if (hit(position))
                    {
                        // Nhảy bám lên mục tiêu: mỗi con một chỗ cho cả bầy không chồng lên nhau
                        bitten = age;
                        latch = position + new Vector2(dir * Random.Range(0.45f, 0.8f), Random.Range(0.25f, 1f));
                        r.sortingOrder = sortingOrder + 3; // bám trên mục tiêu: vẽ đè lên tia lửa, độc toé chỗ trúng đòn
                        halo.sortingOrder = sortingOrder + 2;
                        eyes.sortingOrder = sortingOrder + 4;
                        fall = new Vector2(-dir * Random.Range(1f, 1.8f), Random.Range(1.2f, 2.2f));
                    }
                }
                else
                {
                    float t = age - bitten;
                    if (t < Bite)
                    {
                        // Bám trên mục tiêu gặm lia lịa
                        float leap = Mathf.Clamp01(t / 0.08f);
                        Vector2 rest = position + Vector2.up * (size * aspect * 0.45f);
                        body = Vector2.Lerp(rest, latch, 1f - Cube(1f - leap)) + Vector2.up * (Mathf.Sin(leap * Mathf.PI) * 0.15f)
                            + new Vector2(dir * Mathf.Abs(gait) * 0.04f, 0f);
                        tilt = (30f + gait * 10f) * dir; // ngóc đầu bám lên mục tiêu, chân cào lia lịa
                        Step(Mathf.FloorToInt(t * 18f));
                    }
                    else if (t < Bite + Drop)
                    {
                        // Rơi ra, lộn ngược, mờ dần
                        float u = t - Bite;
                        body = latch + fall * u + Vector2.down * (7f * u * u);
                        tilt = (30f + 540f * u) * dir;
                        alpha = 1f - u / Drop;
                    }
                    else
                        return Finish();
                }

                Transform tr = r.transform;
                tr.position = body;
                tr.rotation = Quaternion.Euler(0f, 0f, tilt);
                float squash = bitten < 0f ? Mathf.Abs(gait) * 0.08f : 0f;
                tr.localScale = new Vector3(size * unit * dir * pop * (1f + squash * 0.6f), size * unit * pop * (1f - squash), 1f);
                r.color = WithAlpha(Color.white, alpha);
                Place(halo, body, size * 1.25f * pop * glowUnit, WithAlpha(Pink, 0.22f * alpha));
                halo.transform.localScale = new Vector3(halo.transform.localScale.x, halo.transform.localScale.y * 0.7f, 1f);
                Vector2 eye = body + (Vector2)(tr.rotation * new Vector2(spiderlingEyes.x * dir, spiderlingEyes.y) * (size * pop));
                float flicker = 0.85f + 0.15f * Mathf.Sin((age + seed) * 31f);
                Place(eyes, eye, size * 0.3f * pop * glowUnit, WithAlpha(Pink, 0.9f * flicker * alpha));
                float onGround = bitten < 0f ? 1f : 1f - Mathf.Clamp01((age - bitten) / 0.08f);
                Place(shadow, new Vector2(body.x, position.y + 0.02f), size * 1.05f * pop * glowUnit, new Color(0.02f, 0f, 0.06f, 0.5f * alpha * onGround));
                shadow.transform.localScale = new Vector3(shadow.transform.localScale.x, shadow.transform.localScale.y * 0.22f, 1f);
                return true;
            });

            void Step(int n) => r.sprite = n % 2 == 1 && spiderlingStep != null ? spiderlingStep : spiderling;

            bool Finish()
            {
                Return(halo);
                Return(eyes);
                Return(shadow);
                return Return(r);
            }
        }

        // =============================================================================================================
        // Dải sáng (mesh)
        // =============================================================================================================

        Ribbon Arc(Vector2 center, float radius, float fromDeg, float toDeg, Color core, Color main, Color edge, float width, float life, float squash,
            Material material = null)
        {
            Ribbon r = RentRibbon(center, core, main, edge, width, life, material);
            r.Radius = radius;
            r.From = fromDeg;
            r.To = toDeg;
            r.Squash = squash;
            r.Sweep = Mathf.Min(life * 0.55f, Mathf.Clamp(Mathf.Abs(toDeg - fromDeg) / 1300f, 0.08f, 0.2f));
            return r;
        }

        Ribbon RentRibbon(Vector2 center, Color core, Color main, Color edge, float width, float life, Material material = null)
        {
            Ribbon r = _freeRibbons.Count > 0 ? _freeRibbons.Pop() : NewRibbon();
            r.Renderer.transform.position = center;
            r.Renderer.sortingOrder = sortingOrder;
            r.Renderer.sharedMaterial = material != null ? material : slashMaterial != null ? slashMaterial : additiveMaterial;
            r.Center = center;
            r.Straight = r.Claw = false;
            r.Squash = 1f;
            r.Core = core;
            r.Main = main;
            r.Edge = edge;
            r.Width = width;
            r.Life = life;
            r.Age = r.Delay = r.Sparks = r.SparkDebt = 0f;
            _ribbons.Add(r);
            return r;
        }

        Ribbon NewRibbon()
        {
            if (s_ribbonTriangles == null)
                BuildRibbonTopology();
            var go = new GameObject("Fx Ribbon");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = slashMaterial != null ? slashMaterial : additiveMaterial;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var mesh = new Mesh { name = "Fx Ribbon" };
            mesh.MarkDynamic();
            var r = new Ribbon { Renderer = renderer, Mesh = mesh };
            mesh.vertices = r.Vertices;
            mesh.colors = r.Colors;
            mesh.uv = s_ribbonUv;
            mesh.triangles = s_ribbonTriangles;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            _meshes.Add(mesh);
            return r;
        }

        static void BuildRibbonTopology()
        {
            s_ribbonUv = new Vector2[RibbonColumns * RibbonRows];
            for (int j = 0; j < RibbonRows; j++)
            for (int c = 0; c < RibbonColumns; c++)
                s_ribbonUv[j * RibbonColumns + c] = new Vector2(c / (RibbonColumns - 1f), j / (RibbonRows - 1f));

            s_ribbonTriangles = new int[(RibbonColumns - 1) * (RibbonRows - 1) * 6];
            int t = 0;
            for (int j = 0; j < RibbonRows - 1; j++)
            for (int c = 0; c < RibbonColumns - 1; c++)
            {
                int a = j * RibbonColumns + c;
                int b = a + RibbonColumns;
                s_ribbonTriangles[t++] = a;
                s_ribbonTriangles[t++] = b;
                s_ribbonTriangles[t++] = a + 1;
                s_ribbonTriangles[t++] = a + 1;
                s_ribbonTriangles[t++] = b;
                s_ribbonTriangles[t++] = b + 1;
            }
        }

        // Dựng lại mesh dải sáng theo tuổi; false = đã tắt hẳn
        bool DrawRibbon(Ribbon r, float dt)
        {
            float age = r.Age - r.Delay;
            if (age >= r.Life)
                return false;
            if (age < 0f)
            {
                r.Renderer.enabled = false;
                return true;
            }

            r.Renderer.enabled = true;
            float head = 1f - Cube(1f - Mathf.Clamp01(age / r.Sweep));
            float lag = Mathf.Clamp01((age - r.Sweep * 0.35f) / Mathf.Max(1e-3f, r.Life - r.Sweep * 0.35f));
            float tail = Mathf.Min(head, lag * lag * lag); // đuôi đứng một chút cho thấy trọn vệt rồi mới đuổi theo
            float after = Mathf.Clamp01((age - r.Sweep) / Mathf.Max(1e-3f, r.Life - r.Sweep));
            float thin = 1f - 0.5f * after;
            float fade = 1f - after * after * after;

            for (int c = 0; c < RibbonColumns; c++)
            {
                float s = c / (RibbonColumns - 1f);
                Frame(r, Mathf.Lerp(tail, head, s), out Vector2 p, out Vector2 n);
                // Lưỡi liềm: đuôi mảnh, dày nhất ở 2/3, nhọn ở đầu. Vết cào: thon hai đầu
                float profile = r.Claw ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * s)), 0.7f) : Mathf.Pow(s, 0.65f) * (1f - Mathf.Pow(s, 6f)) * 1.43f;
                float w = r.Width * thin * profile;
                float a = fade * (r.Claw ? 1f : Mathf.SmoothStep(0f, 1f, s * 2.2f));
                for (int j = 0; j < RibbonRows; j++)
                {
                    float v = j / (RibbonRows - 1f);
                    int k = j * RibbonColumns + c;
                    r.Vertices[k] = p + n * (w * Mathf.Lerp(-1f, 0.2f, v));
                    // mép trong ngả màu edge, giữa màu chính, lõi trắng rực sát mép ngoài, viền ngoài cùng lại về màu chính
                    Color col = j == 0 ? r.Edge : j == 1 ? Color.Lerp(r.Edge, r.Main, 0.6f) : j == 3 ? r.Core : r.Main;
                    col.a *= a;
                    r.Colors[k] = col;
                }
            }

            r.Mesh.vertices = r.Vertices;
            r.Mesh.colors = r.Colors;
            r.Mesh.RecalculateBounds();

            // Tia lửa nhỏ văng ra từ đầu vệt trong lúc quét
            if (r.Sparks > 0f && age < r.Sweep)
            {
                r.SparkDebt += r.Sparks * dt;
                while (r.SparkDebt >= 1f)
                {
                    r.SparkDebt -= 1f;
                    Frame(r, head, out Vector2 p, out Vector2 n);
                    Frame(r, Mathf.Max(0f, head - 0.05f), out Vector2 q, out _);
                    Vector2 forward = (p - q).normalized;
                    ref Particle s = ref Emit(spark, false, sortingOrder + 1, r.Center + p + n * (r.Width * Random.Range(-0.6f, 0.1f)),
                        forward * Random.Range(2.5f, 5f) + n * Random.Range(0.2f, 1.5f), Random.Range(0.18f, 0.32f), Random.Range(0.15f, 0.28f), r.Core);
                    s.EndColor = r.Main;
                    s.EndSize = s.Size * 0.4f;
                    s.Drag = 3f;
                    s.Stretch = 0.06f;
                }
            }

            return true;
        }

        // Điểm (so với tâm) và pháp tuyến hướng ra ngoài của dải sáng ở vị trí u (0 = đầu cung / điểm đầu, 1 = cuối)
        static void Frame(Ribbon r, float u, out Vector2 p, out Vector2 n)
        {
            if (r.Straight)
            {
                Vector2 d = r.End - r.Center;
                p = d * u;
                n = new Vector2(-d.y, d.x).normalized;
                return;
            }

            float a = Mathf.Lerp(r.From, r.To, u) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            p = new Vector2(cos * r.Radius, sin * r.Radius * r.Squash);
            n = new Vector2(cos * r.Squash, sin).normalized;
        }

        // =============================================================================================================
        // Hạt
        // =============================================================================================================

        ref Particle Emit(Sprite sprite, bool additive, int order, Vector2 position, Vector2 velocity, float size, float life, Color color)
        {
            if (_particles == null)
                _particles = new Particle[Mathf.Max(32, maxParticles)];
            if (sprite == null || _particleCount >= _particles.Length)
            {
                _spare = default;
                return ref _spare;
            }

            ref Particle p = ref _particles[_particleCount++];
            p = default;
            p.Renderer = RentSprite(sprite, order, additive);
            p.Position = position;
            p.Velocity = velocity;
            p.Life = Mathf.Max(0.01f, life);
            p.Size = p.EndSize = size;
            p.Squash = 1f;
            p.Color = p.EndColor = color;
            p.Unit = Unit(sprite);
            return ref p;
        }

        void TickParticles(float dt)
        {
            for (int i = 0; i < _particleCount;)
            {
                ref Particle p = ref _particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    Return(p.Renderer);
                    _particles[i] = _particles[--_particleCount];
                    _particles[_particleCount] = default;
                    continue;
                }

                float k = p.Age / p.Life;
                p.Velocity.y -= p.Gravity * dt;
                if (p.Drag > 0f)
                    p.Velocity *= 1f / (1f + p.Drag * dt);
                p.Position += p.Velocity * dt;
                p.Angle += p.Spin * dt;

                float size = Mathf.LerpUnclamped(p.Size, p.EndSize, 1f - (1f - k) * (1f - k)) * p.Unit;
                float alpha = p.FadeIn > 0f ? Mathf.Clamp01(k / p.FadeIn) : 1f;
                if (k > p.Hold)
                    alpha *= 1f - (k - p.Hold) / (1f - p.Hold);
                Color c = Color.LerpUnclamped(p.Color, p.EndColor, k);
                c.a *= alpha;

                Transform t = p.Renderer.transform;
                t.position = p.Position;
                if (p.Stretch > 0f)
                {
                    float speed = p.Velocity.magnitude;
                    t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg);
                    t.localScale = new Vector3(size * (1f + speed * p.Stretch), size * p.Squash, 1f);
                }
                else
                {
                    t.rotation = Quaternion.Euler(0f, 0f, p.Angle);
                    t.localScale = new Vector3(size, size * p.Squash, 1f);
                }

                p.Renderer.color = c;
                i++;
            }
        }

        // =============================================================================================================
        // Dùng lại đối tượng
        // =============================================================================================================

        SpriteRenderer RentSprite(Sprite sprite, int order, bool additive = false)
        {
            SpriteRenderer r = _sprites.Count > 0 ? _sprites.Pop() : NewSprite();
            r.sprite = sprite;
            Material material = additive ? additiveMaterial : alphaMaterial;
            r.sharedMaterial = material != null ? material : _defaultSpriteMaterial;
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
            var r = new GameObject("Fx").AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            if (_defaultSpriteMaterial == null)
                _defaultSpriteMaterial = r.sharedMaterial;
            return r;
        }

        bool Return(SpriteRenderer r)
        {
            r.enabled = false;
            _sprites.Push(r);
            return false;
        }

        Spinner RentSpinner(Sprite sprite, int order)
        {
            Spinner s;
            if (_spinners.Count > 0)
                s = _spinners.Pop();
            else
            {
                var holder = new GameObject("Fx Spinner").transform;
                holder.SetParent(transform, false);
                var renderer = new GameObject("Fx").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(holder, false);
                s = new Spinner { Holder = holder, Renderer = renderer };
            }

            s.Renderer.sprite = sprite;
            s.Renderer.sharedMaterial = alphaMaterial != null ? alphaMaterial : s.Renderer.sharedMaterial;
            s.Renderer.sortingOrder = order;
            s.Renderer.transform.localScale = Vector3.zero;
            s.Renderer.enabled = true;
            return s;
        }

        void Return(Spinner s)
        {
            s.Renderer.enabled = false;
            _spinners.Push(s);
        }

        LineRenderer RentLine(Material material, int points, int order)
        {
            LineRenderer line = _lines.Count > 0 ? _lines.Pop() : NewLine();
            line.sharedMaterial = material;
            line.positionCount = points;
            line.sortingOrder = order;
            line.widthMultiplier = 1f;
            line.enabled = true;
            return line;
        }

        LineRenderer NewLine()
        {
            var line = new GameObject("Fx Line").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        bool Return(LineRenderer line)
        {
            line.enabled = false;
            _lines.Push(line);
            return false;
        }

        // =============================================================================================================
        // Tiện ích
        // =============================================================================================================

        Sprite Rock() => rocks != null && rocks.Length > 0 ? rocks[Random.Range(0, rocks.Length)] : glow;

        static void Place(SpriteRenderer r, Vector2 at, float scale, Color color, float angle = 0f)
        {
            Transform t = r.transform;
            t.position = at;
            t.localScale = Vector3.one * scale;
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            r.color = color;
        }

        // 1 / bề ngang ảnh (unit): nhân với cỡ mong muốn ra localScale
        static float Unit(Sprite sprite) => sprite != null ? 1f / Mathf.Max(1e-4f, sprite.bounds.size.x) : 1f;

        static float Cube(float x) => x * x * x;

        static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            return 1f + 2.70158f * x * x * x + 1.70158f * x * x;
        }

        /// <summary>Lõi trắng rực của vệt sáng màu color.</summary>
        static Color HotOf(Color color) => WithAlpha(Color.Lerp(color, Color.white, 0.75f), color.a);

        /// <summary>Mép trong của vệt sáng: ngả sang tím.</summary>
        static Color EdgeOf(Color color) => WithAlpha(Color.Lerp(color, Violet, 0.55f), color.a);

        static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
