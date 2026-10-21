using System.Collections.Generic;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Vẽ chiêu laze theo trạng thái của LaserSkill:
    ///  - Vận sức: quầng tím rực dần quanh người, mắt sáng dần tới trắng chói, vòng sáng co dần vào mắt, vệt năng lượng
    ///    bị hút vào mắt, tia sét tím đánh quanh đầu ngày càng dày; ngay trước khi bắn mắt co lại một nhịp.
    ///  - Bắn: vòng sáng bung ra ở mắt, tia mảnh rồi bung thành chùm laze (quầng tím, giữa xanh, lõi trắng) rung nhẹ, chớp sáng ở mắt,
    ///    hạt sáng bay quanh tia, tia lửa bắn ra ở chỗ trúng.
    ///  - Tắt: tia co lại, mắt nguội dần, khói bốc lên.
    /// Các bộ phận (LineRenderer, SpriteRenderer) tự tạo lúc chạy từ vật liệu / sprite đã gán sẵn.
    /// </summary>
    [DefaultExecutionOrder(100)] // sau LaserSkill: đầu tia đã tính xong cho frame này
    public class LaserBeamView : MonoBehaviour
    {
        [SerializeField] LaserSkill skill;
        [Tooltip("Vật liệu mặt cắt tia: đặc ở giữa, mềm hai mép (dùng cho tia và tia sét)")]
        [SerializeField] Material beamMaterial;
        [SerializeField] Sprite glow;
        [Tooltip("Ngôi sao 4 cánh: chớp sáng ở mắt và chỗ tia trúng")]
        [SerializeField] Sprite flare;
        [SerializeField] Sprite smoke;
        [Tooltip("Vòng sáng: co dần vào mắt khi vận sức, bung ra khi tia bắn")]
        [SerializeField] Sprite ring;

        [Header("Màu")]
        [SerializeField] Color outerColor = new Color(0.55f, 0.22f, 1f, 0.6f);
        [SerializeField] Color midColor = new Color(0.38f, 0.72f, 1f, 0.92f);
        [SerializeField] Color coreColor = new Color(0.94f, 0.98f, 1f, 1f);
        [SerializeField] Color chargeColor = new Color(0.66f, 0.35f, 1f, 1f);

        [Header("Kích thước tia (unit)")]
        [SerializeField] float outerWidth = 0.95f;
        [SerializeField] float midWidth = 0.5f;
        [SerializeField] float coreWidth = 0.2f;
        [Tooltip("Tia bung từ sợi mảnh thành chùm to trong bấy nhiêu giây")]
        [SerializeField] float burstTime = 0.2f;

        [Header("Vận sức")]
        [Tooltip("Tầm quanh mắt (unit) mà tia sét, vệt năng lượng, vòng sáng toả ra / hút vào (chiêu tia to thì to theo)")]
        [SerializeField, Min(0.2f)] float chargeRadius = 1.1f;
        [Tooltip("Quầng tím rực quanh người lúc sắp bắn (đường kính, unit)")]
        [SerializeField, Min(0f)] float auraSize = 3f;
        [Tooltip("Số vệt năng lượng hút vào mắt mỗi giây: lúc mới vận sức → lúc sắp bắn")]
        [SerializeField] Vector2 chargeMotes = new Vector2(24f, 120f);

        [Header("Thứ tự vẽ")]
        [Tooltip("Tia nằm sau đầu (thân = 10) để như bắn ra từ mắt; trước các xúc tu")]
        [SerializeField] int beamOrder = 9;
        [Tooltip("Chớp sáng, hạt, sét, khói nằm trước mặt")]
        [SerializeField] int frontOrder = 13;
        [Tooltip("Quầng tím lúc vận sức nằm sau cả con (sau xúc tu, chân nhện)")]
        [SerializeField] int auraOrder = -6;

        const int BeamPoints = 20;
        const int ArcCount = 6;
        const int ArcPoints = 9;
        const int RingCount = 3;
        const int MaxParticles = 160;
        const float RingRadius = 1.19f; // bán kính nét vòng của ảnh UiRing ở scale 1 (unit)

        enum Kind { Mote, Spark, Smoke }

        struct Particle
        {
            public Kind Kind;
            public Vector2 Position, Velocity;
            public float Life, MaxLife, Size, Spin;
            public Color Color;
        }

        LineRenderer _outer, _mid, _core;
        readonly LineRenderer[] _arcs = new LineRenderer[ArcCount];
        readonly LineRenderer[] _arcGlows = new LineRenderer[ArcCount]; // quầng tím mờ bao quanh nét sét
        readonly SpriteRenderer[] _rings = new SpriteRenderer[RingCount];
        readonly float[] _arcTimer = new float[ArcCount];
        readonly bool[] _arcOn = new bool[ArcCount];
        SpriteRenderer _eyeGlow, _eyeCore, _eyeFlare, _hitGlow, _hitFlare, _aura;
        readonly List<Particle> _particles = new List<Particle>(MaxParticles);
        SpriteRenderer[] _particleSprites;
        readonly Vector3[] _points = new Vector3[BeamPoints];
        // Phần lẻ số hạt chưa sinh của từng nguồn (để sinh đều theo tốc độ dù frame dài ngắn khác nhau)
        float _chargeDebt, _beamDebt, _hitDebt, _smokeDebt;
        float _time;
        LaserPhase _lastPhase;
        [System.NonSerialized] bool _built; // không giữ qua lần nạp lại code lúc đang Play: dựng lại cho đủ bộ phận

        void Awake()
        {
            if (skill == null)
                skill = GetComponentInParent<LaserSkill>();
            Build();
        }

        void Build()
        {
            if (_built || beamMaterial == null || glow == null)
                return;

            // Bộ phận cũ (còn lại sau khi Unity nạp lại code lúc đang Play) thì bỏ, dựng mới cho khớp
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _particles.Clear();

            _outer = Line("Beam Outer", beamOrder);
            _mid = Line("Beam Mid", beamOrder);
            _core = Line("Beam Core", beamOrder);
            foreach (LineRenderer line in new[] { _outer, _mid, _core })
            {
                line.positionCount = BeamPoints;
                line.numCapVertices = 6;
            }

            for (int i = 0; i < ArcCount; i++)
            {
                _arcGlows[i] = Line($"Spark Arc Glow {i}", frontOrder);
                _arcGlows[i].positionCount = ArcPoints;
                _arcs[i] = Line($"Spark Arc {i}", frontOrder);
                _arcs[i].positionCount = ArcPoints;
            }

            _aura = Sprite("Charge Aura", glow, auraOrder);
            for (int i = 0; i < RingCount; i++)
                _rings[i] = Sprite($"Charge Ring {i}", ring != null ? ring : glow, frontOrder);
            _eyeGlow = Sprite("Eye Glow", glow, frontOrder);
            _eyeCore = Sprite("Eye Core", glow, frontOrder);
            _eyeFlare = Sprite("Eye Flare", flare != null ? flare : glow, frontOrder);
            _hitGlow = Sprite("Hit Glow", glow, frontOrder);
            _hitFlare = Sprite("Hit Flare", flare != null ? flare : glow, frontOrder);

            _particleSprites = new SpriteRenderer[MaxParticles];
            var pool = new GameObject("Particles").transform;
            pool.SetParent(transform, false);
            for (int i = 0; i < MaxParticles; i++)
            {
                _particleSprites[i] = new GameObject("P").AddComponent<SpriteRenderer>();
                _particleSprites[i].transform.SetParent(pool, false);
                _particleSprites[i].sortingOrder = frontOrder;
                _particleSprites[i].enabled = false;
            }

            _built = true;
            HideAll();
        }

        LineRenderer Line(string name, int order)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = beamMaterial;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingOrder = order;
            line.enabled = false;
            return line;
        }

        SpriteRenderer Sprite(string name, Sprite sprite, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        void HideAll()
        {
            _outer.enabled = _mid.enabled = _core.enabled = false;
            for (int i = 0; i < ArcCount; i++)
                _arcs[i].enabled = _arcGlows[i].enabled = false;
            foreach (SpriteRenderer r in _rings)
                r.enabled = false;
            _eyeGlow.enabled = _eyeCore.enabled = _eyeFlare.enabled = _hitGlow.enabled = _hitFlare.enabled = _aura.enabled = false;
        }

        void LateUpdate()
        {
            Build();
            if (!_built || skill == null)
                return;

            float dt = Time.deltaTime;
            _time += dt;
            LaserPhase phase = skill.Phase;
            float t = skill.Elapsed;
            Vector2 origin = skill.Origin;

            if (phase != _lastPhase && phase == LaserPhase.Firing)
                Burst(origin, skill.Direction, 14, Kind.Spark); // tia vừa bung: tia lửa toé ra ở mắt
            if (phase != _lastPhase && phase == LaserPhase.Charging)
            {
                // Lần vận sức mới: tia sét vẽ lại từ đầu (không hiện lại tia cũ ở chỗ cũ)
                for (int i = 0; i < ArcCount; i++)
                {
                    _arcOn[i] = false;
                    _arcTimer[i] = 0f;
                }
            }
            _lastPhase = phase;

            HideAll();
            switch (phase)
            {
                case LaserPhase.Charging:
                    DrawCharge(origin, skill.PhaseProgress, dt);
                    break;
                case LaserPhase.Firing:
                    DrawBeam(origin, t, dt);
                    break;
                case LaserPhase.Cooldown when t < 0.6f:
                    // Mắt nguội dần, khói bốc lên
                    float cool = 1f - t / 0.6f;
                    Show(_eyeGlow, origin, 0.55f * cool, WithAlpha(chargeColor, 0.6f * cool * cool), 0f);
                    for (int i = Emit(ref _smokeDebt, t < 0.45f ? 14f : 0f, dt); i > 0; i--)
                        Add(SmokePuff(origin));
                    break;
            }

            UpdateParticles(dt);
        }

        // ---------- Vận sức ----------

        void DrawCharge(Vector2 eye, float p, float dt)
        {
            // Chiêu tia to (đại pháo) thì quầng, sét to theo, mắt to vừa phải (không che hết đầu); càng gần lúc bắn càng dồn dập
            float k = Mathf.Sqrt(Mathf.Max(skill.WidthScale, 0.5f));
            float g = Mathf.Sqrt(k);
            float radius = chargeRadius * k;
            float e = 1f - (1f - p) * (1f - p); // sáng lên ngay khi bấm, rồi tăng dần
            float pulse = 1f + (0.08f + 0.14f * p) * Mathf.Sin(_time * Mathf.Lerp(12f, 46f, p));
            float pinch = 1f - 0.3f * Mathf.SmoothStep(0f, 1f, (p - 0.86f) / 0.14f); // ngay trước khi bắn: co lại một nhịp
            Color color = Color.Lerp(chargeColor, coreColor, p * p);

            // Quầng tím sau lưng: cả con rực lên
            Show(_aura, eye, auraSize * k * Mathf.Lerp(0.45f, 1f, e) * (1f + 0.05f * Mathf.Sin(_time * 23f)),
                WithAlpha(chargeColor, Mathf.Lerp(0.1f, 0.42f, e)), 0f);

            // Mắt: quầng sáng to dần, lõi trắng chói dần, sao 4 cánh hiện ra ở nửa sau
            Show(_eyeGlow, eye, Mathf.Lerp(0.45f, 1.2f, e) * g * pulse * pinch, WithAlpha(color, Mathf.Lerp(0.4f, 0.85f, e)), 0f);
            Show(_eyeCore, eye, Mathf.Lerp(0.14f, 0.45f, e) * g * pulse * pinch, WithAlpha(coreColor, Mathf.Lerp(0.5f, 1f, e)), 0f);
            if (p > 0.35f)
            {
                float f = (p - 0.35f) / 0.65f;
                Show(_eyeFlare, eye, Mathf.Lerp(0.25f, 1f, f) * g * pulse * pinch, WithAlpha(coreColor, Mathf.Min(1f, f * 1.6f)), _time * 150f);
            }

            // Vòng sáng co dần vào mắt (gom năng lượng): càng tới gần mắt càng nhanh. Vận sức lâu thì nhiều đợt hơn
            float waves = Mathf.Max(1f, skill.ChargeTime / 0.35f);
            for (int i = 0; i < RingCount; i++)
            {
                float start = i / (float)RingCount;
                float phase = p * waves - start;
                if (phase < 0f)
                    continue; // đợt này chưa tới
                phase = Mathf.Repeat(phase, 1f);
                float r = Mathf.Lerp(chargeRadius * g, 0.1f, phase * phase);
                float alpha = Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 0.7f) * Mathf.Lerp(0.15f, 0.4f, e);
                ShowRing(_rings[i], eye, r, WithAlpha(Color.Lerp(chargeColor, coreColor, 0.15f + 0.45f * phase), alpha));
            }

            // Vệt năng lượng bị hút vào mắt, càng gần lúc bắn càng dày
            for (int i = Emit(ref _chargeDebt, Mathf.Lerp(chargeMotes.x, chargeMotes.y, p) * k, dt); i > 0; i--)
            {
                Vector2 from = eye + Random.insideUnitCircle.normalized * (Random.Range(0.9f, 1.8f) * radius);
                Vector2 swirl = Vector2.Perpendicular(eye - from).normalized * Random.Range(-1.2f, 1.2f);
                Add(new Particle
                {
                    Kind = Kind.Mote, Position = from, Velocity = (eye - from).normalized * Random.Range(0.8f, 1.6f) + swirl,
                    MaxLife = 0.7f, Size = Random.Range(0.1f, 0.2f) * k, Color = RandomEnergyColor(),
                });
            }

            // Tia sét tím: nửa đánh từ quanh đầu vào mắt, nửa bò vòng quanh đầu; mỗi tia chớp tắt ngẫu nhiên, hình gấp khúc
            // đổi liên tục, càng gần lúc bắn càng nhiều tia, càng to
            for (int i = 0; i < ArcCount; i++)
            {
                _arcTimer[i] -= dt;
                if (_arcTimer[i] <= 0f)
                {
                    _arcTimer[i] = Random.Range(0.035f, 0.08f);
                    _arcOn[i] = Random.value < 0.2f + 0.75f * p;
                    if (_arcOn[i])
                        Lightning(i, eye, radius, p, k);
                }

                _arcs[i].enabled = _arcGlows[i].enabled = _arcOn[i];
            }
        }

        void Lightning(int index, Vector2 eye, float radius, float p, float k)
        {
            LineRenderer core = _arcs[index], halo = _arcGlows[index];
            float from = Random.Range(0f, 2f * Mathf.PI);
            bool inward = Random.value < 0.5f;
            float to = inward ? from + Random.Range(-0.4f, 0.4f) : from + Random.Range(0.6f, 1.4f) * (Random.value < 0.5f ? -1f : 1f);
            float r0 = (inward ? Random.Range(0.6f, 1f) : Random.Range(0.45f, 0.95f)) * radius;
            float r1 = inward ? Random.Range(0.08f, 0.2f) * radius : r0 * Random.Range(0.8f, 1.2f);
            float jag = 0.12f * radius;
            for (int j = 0; j < ArcPoints; j++)
            {
                float u = j / (ArcPoints - 1f);
                float a = Mathf.Lerp(from, to, u);
                float r = Mathf.Lerp(r0, r1, u) + (j == 0 || j == ArcPoints - 1 ? 0f : Random.Range(-jag, jag));
                Vector3 point = eye + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                core.SetPosition(j, point);
                halo.SetPosition(j, point);
            }

            float width = Random.Range(0.04f, 0.065f) * (0.7f + 0.7f * p) * k;
            core.startWidth = width;
            core.endWidth = width * 0.5f;
            core.startColor = WithAlpha(Color.Lerp(coreColor, chargeColor, 0.3f), 1f);
            core.endColor = WithAlpha(Color.Lerp(coreColor, chargeColor, 0.6f), 0.85f);
            halo.startWidth = width * 5f;
            halo.endWidth = width * 3f;
            halo.startColor = WithAlpha(chargeColor, 0.55f);
            halo.endColor = WithAlpha(chargeColor, 0.3f);
        }

        // ---------- Bắn ----------

        void DrawBeam(Vector2 origin, float t, float dt)
        {
            float fire = skill.FireTime;
            // Sợi mảnh → bung to (hơi vượt rồi ổn định) → giữ, rung nhẹ → co lại khi sắp tắt
            float width;
            if (t < burstTime * 0.4f)
                width = 0.12f;
            else if (t < burstTime)
                width = Mathf.Lerp(0.12f, 1.25f, Mathf.SmoothStep(0f, 1f, (t - burstTime * 0.4f) / (burstTime * 0.6f)));
            else
                width = Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - burstTime) / 0.15f));
            width *= Mathf.SmoothStep(0f, 1f, (fire - t) / 0.2f); // sắp tắt thì co lại thành sợi mảnh rồi biến mất
            width *= 1f + 0.07f * Mathf.Sin(_time * 53f) + 0.05f * Mathf.Sin(_time * 31f + 1f);

            Vector2 end = skill.BeamEnd;
            Vector2 dir = skill.Direction;
            float length = Vector2.Distance(origin, end);
            float scale = skill.WidthScale; // chiêu to (đại pháo) thì tia, chớp sáng to theo
            SetBeam(_outer, origin, dir, length, outerWidth * width * scale, outerColor, 0.05f * scale, 9f / scale);
            SetBeam(_mid, origin, dir, length, midWidth * width * scale, midColor, 0.025f * scale, 13f / scale);
            SetBeam(_core, origin, dir, length, coreWidth * width * scale, coreColor, 0f, 0f);

            // Tia vừa bung: vòng sáng nở ra ở mắt
            if (t < 0.3f)
            {
                float f = t / 0.3f;
                ShowRing(_rings[0], origin, Mathf.Lerp(0.25f, 1.8f, 1f - (1f - f) * (1f - f)) * Mathf.Sqrt(scale),
                    WithAlpha(Color.Lerp(coreColor, chargeColor, f), 0.5f * (1f - f)));
            }

            // Chớp sáng ở mắt
            float flicker = 1f + 0.12f * Mathf.Sin(_time * 61f);
            float flash = Mathf.Clamp01(width);
            Show(_eyeGlow, origin, 1.15f * flicker * flash * Mathf.Sqrt(scale), WithAlpha(coreColor, 0.9f * flash), 0f);
            Show(_eyeFlare, origin, 1.25f * flicker * flash * Mathf.Sqrt(scale), WithAlpha(coreColor, flash), _time * 90f);

            // Hạt sáng bay quanh tia
            Vector2 across = Vector2.Perpendicular(dir);
            for (int i = Emit(ref _beamDebt, 36f * flash, dt); i > 0; i--)
            {
                Add(new Particle
                {
                    Kind = Kind.Mote,
                    Position = origin + dir * Random.Range(0.3f, length) + across * Random.Range(-0.2f, 0.2f),
                    Velocity = across * Random.Range(-0.7f, 0.7f) + dir * Random.Range(0.5f, 1.5f),
                    MaxLife = Random.Range(0.25f, 0.45f), Size = Random.Range(0.12f, 0.22f), Color = RandomEnergyColor(),
                });
            }

            // Chỗ tia trúng vật cản: sáng chói, tia lửa toé ngược lại
            if (skill.BeamHit)
            {
                Show(_hitGlow, end, 0.8f * flicker * flash * scale, WithAlpha(midColor, 0.85f * flash), 0f);
                Show(_hitFlare, end, 0.7f * flicker * flash * scale, WithAlpha(coreColor, flash), -_time * 140f);
                Vector2 normal = skill.HitNormal;
                for (int i = Emit(ref _hitDebt, 45f * flash, dt); i > 0; i--)
                {
                    Add(new Particle
                    {
                        Kind = Kind.Spark, Position = end,
                        Velocity = (normal + Random.insideUnitCircle * 0.9f).normalized * Random.Range(1.5f, 4.5f),
                        MaxLife = Random.Range(0.2f, 0.4f), Size = Random.Range(0.1f, 0.16f),
                        Color = Color.Lerp(coreColor, midColor, Random.value),
                    });
                }
            }
        }

        // Tia gồm nhiều điểm, quầng ngoài uốn sóng nhẹ chạy dọc tia; gốc tia đứng yên ở mắt
        void SetBeam(LineRenderer line, Vector2 origin, Vector2 dir, float length, float width, Color color,
            float wobble, float frequency)
        {
            line.enabled = width > 0.001f;
            if (!line.enabled)
                return;

            Vector2 across = Vector2.Perpendicular(dir);
            for (int i = 0; i < BeamPoints; i++)
            {
                float s = length * i / (BeamPoints - 1f);
                float wave = wobble * Mathf.Sin(s * frequency - _time * 40f) * Mathf.Clamp01(s / 0.6f);
                _points[i] = origin + dir * s + across * wave;
            }

            line.SetPositions(_points);
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = WithAlpha(color, color.a * 0.75f);
        }

        // ---------- Hạt ----------

        // Số hạt cần sinh trong frame này để được rate hạt / giây
        static int Emit(ref float debt, float rate, float dt)
        {
            debt += rate * dt;
            int count = Mathf.FloorToInt(debt);
            debt -= count;
            return count;
        }

        void Burst(Vector2 at, Vector2 dir, int count, Kind kind)
        {
            for (int i = 0; i < count; i++)
            {
                Add(new Particle
                {
                    Kind = kind, Position = at,
                    Velocity = (dir + Random.insideUnitCircle * 1.2f).normalized * Random.Range(2f, 5f),
                    MaxLife = Random.Range(0.2f, 0.35f), Size = Random.Range(0.12f, 0.18f),
                    Color = Color.Lerp(coreColor, midColor, Random.value),
                });
            }
        }

        Particle SmokePuff(Vector2 eye) => new Particle
        {
            Kind = Kind.Smoke, Position = eye + Random.insideUnitCircle * 0.08f,
            Velocity = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.35f, 0.75f)),
            MaxLife = Random.Range(0.8f, 1.1f), Size = Random.Range(0.3f, 0.42f), Spin = Random.Range(-90f, 90f),
            Color = new Color(0.66f, 0.6f, 0.82f, 0.6f),
        };

        void Add(Particle particle)
        {
            if (_particles.Count >= MaxParticles)
                return;
            particle.Life = particle.MaxLife;
            _particles.Add(particle);
        }

        void UpdateParticles(float dt)
        {
            Vector2 eye = skill.Origin;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Life -= dt;
                switch (p.Kind)
                {
                    case Kind.Mote when skill.Phase == LaserPhase.Charging:
                        // Bị hút vào mắt, càng gần càng nhanh; tới mắt (hoặc vụt qua mắt) thì tan
                        Vector2 pull = eye - p.Position;
                        p.Velocity += pull.normalized * (20f * dt);
                        float near = pull.magnitude;
                        if (near < 0.1f || (near < 0.35f && Vector2.Dot(pull, p.Velocity) < 0f))
                            p.Life = 0f;
                        break;
                    case Kind.Spark:
                        p.Velocity += Vector2.down * (9f * dt);
                        break;
                    case Kind.Smoke:
                        p.Velocity *= 1f - 0.8f * dt;
                        p.Size += 0.5f * dt;
                        break;
                }

                p.Position += p.Velocity * dt;
                if (p.Life <= 0f)
                    _particles.RemoveAt(i);
                else
                    _particles[i] = p;
            }

            bool charging = skill.Phase == LaserPhase.Charging;
            for (int i = 0; i < MaxParticles; i++)
            {
                SpriteRenderer renderer = _particleSprites[i];
                if (i >= _particles.Count)
                {
                    renderer.enabled = false;
                    continue;
                }

                Particle p = _particles[i];
                float age = 1f - p.Life / p.MaxLife;
                float alpha = p.Kind == Kind.Smoke ? Mathf.Sin(Mathf.PI * age) : Mathf.Clamp01(p.Life / p.MaxLife * 2f) * Mathf.Clamp01(age * 6f);
                renderer.enabled = true;
                renderer.sprite = p.Kind == Kind.Smoke && smoke != null ? smoke : glow;
                renderer.color = WithAlpha(p.Color, p.Color.a * alpha);
                Transform t = renderer.transform;
                t.position = p.Position;
                if (p.Kind == Kind.Mote && charging)
                {
                    // Đang bị hút vào mắt: kéo dài thành vệt theo hướng bay
                    float speed = p.Velocity.magnitude;
                    t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg);
                    t.localScale = new Vector3(p.Size * (1f + 0.22f * speed), p.Size * 0.75f, 1f);
                    continue;
                }

                t.rotation = Quaternion.Euler(0f, 0f, p.Spin * age);
                t.localScale = Vector3.one * p.Size * (p.Kind == Kind.Spark ? 1f : 1f + 0.3f * Mathf.Sin(_time * 20f + i));
            }
        }

        // ---------- Tiện ích ----------

        static void Show(SpriteRenderer renderer, Vector2 at, float size, Color color, float angle)
        {
            renderer.enabled = size > 0.001f && color.a > 0.001f;
            Transform t = renderer.transform;
            t.position = at;
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = Vector3.one * size;
            renderer.color = color;
        }

        // Vòng sáng bán kính radius (unit, tính tới nét vòng)
        static void ShowRing(SpriteRenderer renderer, Vector2 at, float radius, Color color) =>
            Show(renderer, at, radius / RingRadius, color, 0f);

        Color RandomEnergyColor()
        {
            float r = Random.value;
            return r < 0.5f ? chargeColor : r < 0.85f ? midColor : coreColor;
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
