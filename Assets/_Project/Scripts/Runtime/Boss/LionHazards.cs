using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Octopus
{
    /// <summary>
    /// Những thứ boss sư tử tung ra rồi tự chạy: vòng đỏ báo trước (nằm dẹt trên mặt sàn / mặt đá), cầu lửa bay vòng cung,
    /// sàn cháy, sóng chấn động chạy trên sàn, thiên thạch mưa lửa, đốm lửa tụ về bờm. Hình là SpriteRenderer dùng lại (không
    /// tạo / huỷ liên tục); lửa, bụi, chớp, đá văng mượn CombatFx. Đánh người chơi qua PlayerHealth (LionBoss gán Target).
    /// Frozen (boss bị đóng băng): mọi thứ đang bay / đang cháy / đang báo trước đứng yên và không gây sát thương.
    /// </summary>
    public class LionHazards : MonoBehaviour
    {
        [Header("Hình")]
        [Tooltip("Vòng báo trước: vòng tròn có lòng mờ, bị ép dẹt cho nằm trên mặt sàn")]
        [SerializeField] Sprite markerSprite;
        [Tooltip("Quầng sáng tròn mềm")]
        [SerializeField] Sprite glowSprite;
        [Tooltip("Quả cầu lửa (lõi sáng, viền lửa)")]
        [SerializeField] Sprite fireballSprite;
        [Tooltip("Vật liệu cộng sáng cho quầng sáng, cầu lửa (trống = vật liệu sprite mặc định)")]
        [SerializeField] Material additive;
        [Tooltip("Thứ tự vẽ: trên boss và người chơi để luôn thấy vòng đỏ, dưới số sát thương")]
        [SerializeField] int sortingOrder = 18;
        [Tooltip("Lớp đá treo: cầu lửa đụng đá thì nổ")]
        [SerializeField] LayerMask rockMask;

        [Header("Màu")]
        [SerializeField] Color warnColor = new Color(1f, 0.12f, 0.08f, 0.95f);
        [SerializeField] Color fireColor = new Color(1f, 0.42f, 0.1f);
        [SerializeField] Color hotColor = new Color(1f, 0.88f, 0.5f);
        [SerializeField] Color rockColor = new Color(0.32f, 0.13f, 0.1f);
        [SerializeField] Color dustColor = new Color(0.78f, 0.48f, 0.34f, 0.7f);
        [Tooltip("Vòng báo trước ép dẹt: cao / rộng")]
        [SerializeField, Range(0.1f, 1f)] float markerSquash = 0.3f;

        /// <summary>Vòng đỏ báo trước một chỗ sắp bị đánh.</summary>
        public sealed class Marker
        {
            internal SpriteRenderer Ring;
            internal SpriteRenderer Glow;
            internal Vector2 At;
            internal float Radius, Age, Life;
            internal bool Alive;
            public Vector2 Position => At;
            public bool IsShowing => Alive;
        }

        readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        readonly List<SpriteRenderer> _active = new List<SpriteRenderer>();
        readonly List<Marker> _markers = new List<Marker>();

        public PlayerHealth Target { get; set; }
        public float FloorY { get; set; }

        /// <summary>Đứng hình (boss bị đóng băng): cầu lửa, thiên thạch, sàn cháy, sóng chấn động dừng lại, không gây sát thương.</summary>
        public bool Frozen { get; set; }

        // Thời gian trôi của các đòn trong frame này: đóng băng thì 0
        float Dt => Frozen ? 0f : Time.deltaTime;
        public Color FireColor => fireColor;
        public Color WarnColor => warnColor;

        /// <summary>Số vòng báo trước đang hiện.</summary>
        public int MarkerCount => _markers.Count;

        public IReadOnlyList<Marker> Markers => _markers;

        static CombatFx Fx => CombatFx.Instance;

        // ---------- Vòng báo trước ----------

        /// <summary>Vòng đỏ dẹt quanh ground, bán kính radius; life = còn bấy nhiêu giây tới lúc đòn đánh xuống (nháy nhanh dần).</summary>
        public Marker ShowMarker(Vector2 ground, float radius, float life)
        {
            var m = new Marker
            {
                Ring = Rent(markerSprite, sortingOrder - 2, false),
                Glow = Rent(glowSprite, sortingOrder - 3, true),
                At = ground,
                Radius = radius,
                Life = Mathf.Max(0.05f, life),
                Alive = true,
            };
            _markers.Add(m);
            DrawMarker(m);
            return m;
        }

        public void HideMarker(Marker m)
        {
            if (m == null || !m.Alive)
                return;
            m.Alive = false;
            Return(m.Ring);
            Return(m.Glow);
            _markers.Remove(m);
        }

        void DrawMarker(Marker m)
        {
            float k = Mathf.Clamp01(m.Age / m.Life);
            float pop = m.Age < 0.12f ? Mathf.Lerp(0.6f, 1.06f, m.Age / 0.12f) : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((m.Age - 0.12f) / 0.1f));
            float pulse = 0.5f + 0.5f * Mathf.Sin(m.Age * Mathf.Lerp(10f, 34f, k));
            Place(m.Ring, m.At + Vector2.up * 0.04f, new Vector2(m.Radius * 2f, m.Radius * 2f * markerSquash) * pop, 0f);
            m.Ring.color = WithAlpha(warnColor, Mathf.Lerp(0.55f, 1f, Mathf.Max(k, pulse * 0.6f)));
            Place(m.Glow, m.At + Vector2.up * 0.1f, new Vector2(m.Radius * 2.8f, m.Radius * 2.8f * markerSquash * 1.5f) * pop, 0f);
            m.Glow.color = WithAlpha(warnColor, (0.3f + 0.4f * k) * (0.7f + 0.3f * pulse));
        }

        // ---------- Đốm lửa tụ về (báo trước cầu lửa) ----------

        /// <summary>count đốm lửa từ xa (radius) bay tụ về at trong duration giây.</summary>
        public void Gather(System.Func<Vector2> at, float radius, float duration, int count) => StartCoroutine(GatherRoutine(at, radius, duration, count));

        IEnumerator GatherRoutine(System.Func<Vector2> at, float radius, float duration, int count)
        {
            var sparks = new SpriteRenderer[count];
            var from = new Vector2[count];
            var delay = new float[count];
            for (int i = 0; i < count; i++)
            {
                sparks[i] = Rent(glowSprite, sortingOrder, true);
                from[i] = Random.insideUnitCircle.normalized * radius * Random.Range(0.7f, 1.1f);
                delay[i] = Random.Range(0f, duration * 0.4f);
                sparks[i].enabled = false;
            }

            for (float t = 0f; t < duration; t += Dt)
            {
                Vector2 center = at();
                for (int i = 0; i < count; i++)
                {
                    float k = Mathf.Clamp01((t - delay[i]) / Mathf.Max(0.05f, duration - delay[i]));
                    sparks[i].enabled = t >= delay[i];
                    float ease = k * k;
                    Place(sparks[i], center + from[i] * (1f - ease), Vector2.one * Mathf.Lerp(1.8f, 2.8f, ease), 0f);
                    sparks[i].color = WithAlpha(Color.Lerp(fireColor, hotColor, ease), Mathf.Lerp(0.4f, 1f, k));
                }

                yield return null;
            }

            foreach (SpriteRenderer s in sparks)
                Return(s);
        }

        // ---------- Cầu lửa ----------

        /// <summary>
        /// Cầu lửa bay từ from với vận tốc velocity, rơi theo gravity. Trúng người chơi: damage. Đụng đá: nổ. Rơi xuống sàn:
        /// sàn cháy rộng burnWidth trong burnTime giây, burnDps sát thương / giây cho ai đứng trên đó.
        /// </summary>
        public void Fireball(Vector2 from, Vector2 velocity, float gravity, float damage, Vector2 knockback, float burnWidth, float burnTime, float burnDps,
            float size = 0.9f) =>
            StartCoroutine(FireballRoutine(from, velocity, gravity, damage, knockback, burnWidth, burnTime, burnDps, size));

        /// <summary>Vận tốc ném để bay từ from tới đúng to sau time giây (rơi theo gravity).</summary>
        public static Vector2 Launch(Vector2 from, Vector2 to, float time, float gravity) =>
            (to - from) / time + 0.5f * gravity * time * Vector2.up;

        IEnumerator FireballRoutine(Vector2 position, Vector2 velocity, float gravity, float damage, Vector2 knockback, float burnWidth,
            float burnTime, float burnDps, float size)
        {
            SpriteRenderer halo = Rent(glowSprite, sortingOrder, true);
            SpriteRenderer ball = Rent(fireballSprite != null ? fireballSprite : glowSprite, sortingOrder + 1, fireballSprite == null);
            float trail = 0f;
            for (float age = 0f; age < 6f; age += Dt)
            {
                float dt = Dt;
                if (dt <= 0f)
                {
                    yield return null; // đóng băng: đứng yên giữa không trung
                    continue;
                }

                velocity += Vector2.down * gravity * dt;
                position += velocity * dt;
                float spin = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                float flicker = 1f + 0.08f * Mathf.Sin(age * 40f);
                Place(ball, position, new Vector2(size * 1.5f, size) * flicker, spin);
                ball.color = Color.white;
                Place(halo, position, Vector2.one * size * 3.2f * flicker, 0f);
                halo.color = WithAlpha(fireColor, 0.6f);

                trail += dt * 40f;
                if (Fx != null && trail >= 1f)
                {
                    trail -= 1f;
                    Fx.SpeedLines(position, velocity, fireColor);
                    Fx.SpeedLines(position, velocity, hotColor);
                }

                if (Target != null && !Target.IsDead && Vector2.Distance(position, Target.Center) < size * 0.5f + 0.45f)
                {
                    Target.Hit(damage, position - velocity.normalized, knockback);
                    Explode(position, size, false);
                    break;
                }

                if (velocity.y < 0f && position.y <= FloorY + 0.1f)
                {
                    Vector2 ground = new Vector2(position.x, FloorY);
                    Explode(ground + Vector2.up * 0.2f, size, true);
                    if (burnTime > 0f)
                        StartCoroutine(BurnRoutine(ground, burnWidth, burnTime, burnDps));
                    break;
                }

                if (rockMask.value != 0 && Physics2D.OverlapPoint(position, rockMask) != null)
                {
                    Explode(position, size, false);
                    break;
                }

                yield return null;
            }

            Return(ball);
            Return(halo);
        }

        void Explode(Vector2 at, float size, bool onGround)
        {
            if (Fx == null)
                return;
            Fx.Flash(at, size * 2.2f, fireColor, 0.25f);
            Fx.Burst(at, fireColor, 10, 5f, 0.14f, 0.35f);
            Fx.Debris(at, rockColor, onGround ? 5 : 3, 4.5f, 0.2f);
            if (onGround)
                Fx.DustCloud(at, size, dustColor, 4);
        }

        // ---------- Sàn cháy ----------

        IEnumerator BurnRoutine(Vector2 ground, float width, float duration, float dps)
        {
            if (Fx != null)
            {
                Fx.Flames(ground, width, fireColor, duration);
                Fx.Cracks(ground, width * 0.45f, fireColor, duration);
            }

            SpriteRenderer glow = Rent(glowSprite, sortingOrder - 4, true);
            const float tick = 0.25f;
            float next = 0f;
            for (float t = 0f; t < duration; t += Dt)
            {
                if (Frozen)
                {
                    yield return null;
                    continue;
                }

                float fade = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((duration - t) / 0.4f);
                Place(glow, ground + Vector2.up * 0.15f, new Vector2(width * 1.6f, width * 0.45f), 0f);
                glow.color = WithAlpha(fireColor, 0.5f * fade * (0.85f + 0.15f * Mathf.Sin(t * 13f)));
                if (t >= next)
                {
                    next += tick;
                    if (Target != null && !Target.IsDead)
                    {
                        Vector2 p = Target.Center;
                        if (Mathf.Abs(p.x - ground.x) < width * 0.5f + 0.3f && p.y - FloorY < 1.3f)
                            Target.Burn(dps * tick, ground);
                    }
                }

                yield return null;
            }

            Return(glow);
        }

        // ---------- Sóng chấn động trên sàn ----------

        /// <summary>
        /// Sóng chấn động chạy dọc mặt sàn từ origin sang hai bên tới range unit, tốc độ speed. Người chơi đang ở sát sàn khi
        /// đỉnh sóng đi qua thì trúng (nhảy lên là né được).
        /// </summary>
        public void GroundWave(Vector2 origin, float startOffset, float range, float speed, float damage, Vector2 knockback, float height = 1.4f) =>
            StartCoroutine(WaveRoutine(origin, startOffset, range, speed, damage, knockback, height));

        IEnumerator WaveRoutine(Vector2 origin, float startOffset, float range, float speed, float damage, Vector2 knockback, float height)
        {
            var crests = new[] { Rent(glowSprite, sortingOrder - 1, true), Rent(glowSprite, sortingOrder - 1, true) };
            bool hit = false;
            float dust = 0f;
            for (float front = startOffset; front < range; front += speed * Dt)
            {
                if (Frozen)
                {
                    yield return null;
                    continue;
                }

                float fade = 1f - Mathf.InverseLerp(range * 0.6f, range, front);
                for (int side = 0; side < 2; side++)
                {
                    float x = origin.x + (side == 0 ? -front : front);
                    Place(crests[side], new Vector2(x, FloorY + 0.4f), new Vector2(3f, 1.8f), 0f);
                    crests[side].color = WithAlpha(fireColor, 0.85f * fade);
                    if (!hit && Target != null && !Target.IsDead)
                    {
                        Vector2 p = Target.Center;
                        if (Mathf.Abs(p.x - x) < 0.9f && p.y - FloorY < height)
                            hit = Target.Hit(damage, new Vector2(origin.x, FloorY), knockback);
                    }
                }

                dust += Dt;
                if (Fx != null && dust > 0.05f)
                {
                    dust = 0f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 at = new Vector2(origin.x + side * front, FloorY);
                        Fx.Debris(at, rockColor, 1, 4f * fade, 0.18f);
                        Fx.SpeedLines(at + Vector2.up * 0.3f, new Vector2(side * speed, 2f), fireColor);
                    }
                }

                yield return null;
            }

            Return(crests[0]);
            Return(crests[1]);
        }

        // ---------- Thiên thạch ----------

        /// <summary>
        /// Vòng đỏ ở ground trong warn giây, rồi thiên thạch rơi từ trên cao xuống trúng ground sau fall giây: nổ, damage cho
        /// người chơi trong bán kính radius.
        /// </summary>
        public void Meteor(Vector2 ground, float warn, float fall, float damage, float radius, Vector2 knockback) =>
            StartCoroutine(MeteorRoutine(ground, warn, fall, damage, radius, knockback));

        IEnumerator MeteorRoutine(Vector2 ground, float warn, float fall, float damage, float radius, Vector2 knockback)
        {
            Marker marker = ShowMarker(ground, radius, warn + fall);
            for (float t = 0f; t < warn; t += Dt)
                yield return null;

            SpriteRenderer halo = Rent(glowSprite, sortingOrder, true);
            SpriteRenderer rock = Rent(fireballSprite != null ? fireballSprite : glowSprite, sortingOrder + 1, fireballSprite == null);
            Vector2 start = ground + new Vector2(Random.Range(-2.5f, 2.5f), 16f);
            Vector2 velocity = (ground - start) / fall;
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            for (float t = 0f; t < fall; t += Dt)
            {
                if (Frozen)
                {
                    yield return null;
                    continue;
                }

                Vector2 p = Vector2.Lerp(start, ground, t / fall);
                Place(rock, p, new Vector2(3.2f, 1.9f), angle);
                rock.color = Color.white;
                Place(halo, p, Vector2.one * 5f, 0f);
                halo.color = WithAlpha(fireColor, 0.7f);
                if (Fx != null)
                {
                    Fx.SpeedLines(p, velocity, fireColor);
                    Fx.SpeedLines(p + Random.insideUnitCircle * 0.5f, velocity, hotColor);
                }
                yield return null;
            }

            Return(rock);
            Return(halo);
            HideMarker(marker);
            if (Fx != null)
            {
                Fx.GroundSlam(ground, radius * 0.75f, fireColor, rockColor, 6, 1.1f);
                Fx.Flash(ground + Vector2.up * 0.4f, radius * 1.6f, fireColor, 0.3f);
                Fx.Flames(ground, radius * 0.8f, fireColor, 0.6f);
            }

            CameraShake.Impulse(Vector2.down, 0.45f);
            if (Target != null && !Target.IsDead && Vector2.Distance(Target.Center, ground + Vector2.up * 0.3f) < radius + 0.45f)
                Target.Hit(damage, ground + Vector2.down, knockback, true);
        }

        // ---------- Dọn dẹp ----------

        /// <summary>Tắt mọi thứ đang bay / đang cháy / đang báo trước (boss chết, người chơi gục).</summary>
        public void StopAll()
        {
            StopAllCoroutines();
            for (int i = _active.Count - 1; i >= 0; i--)
                Return(_active[i]);
            foreach (Marker m in _markers)
                m.Alive = false;
            _markers.Clear();
        }

        void OnDestroy()
        {
            if (_holder != null)
                Destroy(_holder.gameObject);
        }

        void Update()
        {
            float dt = Dt;
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                Marker m = _markers[i];
                m.Age += dt;
                DrawMarker(m);
            }
        }

        // ---------- Kho hình ----------

        SpriteRenderer Rent(Sprite sprite, int order, bool glow)
        {
            SpriteRenderer r = _pool.Count > 0 ? _pool.Pop() : NewRenderer();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.sharedMaterial = glow && additive != null ? additive : _defaultMaterial;
            r.enabled = true;
            r.gameObject.SetActive(true);
            _active.Add(r);
            return r;
        }

        Material _defaultMaterial;
        Transform _holder;

        // Hình nằm ngoài boss (boss di chuyển không kéo theo vòng báo trước, cầu lửa)
        SpriteRenderer NewRenderer()
        {
            if (_holder == null)
                _holder = new GameObject("Lion Hazards FX").transform;
            var go = new GameObject("Hazard FX");
            go.transform.SetParent(_holder, false);
            var r = go.AddComponent<SpriteRenderer>();
            if (_defaultMaterial == null)
                _defaultMaterial = r.sharedMaterial;
            return r;
        }

        void Return(SpriteRenderer r)
        {
            if (r == null || !_active.Remove(r))
                return;
            r.gameObject.SetActive(false);
            _pool.Push(r);
        }

        static void Place(SpriteRenderer r, Vector2 at, Vector2 size, float angle)
        {
            Transform t = r.transform;
            t.position = new Vector3(at.x, at.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 native = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            t.localScale = new Vector3(size.x / Mathf.Max(native.x, 1e-3f), size.y / Mathf.Max(native.y, 1e-3f), 1f);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
