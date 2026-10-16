using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Bia tập (tượng đá có lõi pha lê) để thử chiêu: có máu, trúng đòn thì loé sáng, bẹp xuống rồi nảy giãn (đòn càng nặng
    /// càng bẹp), lắc nghiêng như bao cát, bị đẩy văng / kéo lại rồi lò xo về chỗ cũ, đòn nặng làm văng mảnh đá. Hiện trạng
    /// thái tơ trói, kén tơ, mực, trúng độc (mất máu dần), choáng (vòng sao vàng quay quanh đầu). Hết máu thì vỡ tung, vài
    /// giây sau dựng lại; không bị đánh một lúc thì hồi đầy máu. Không cản đường (collider dạng trigger).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] SpriteRenderer art;
        [Tooltip("Lớp trắng loé lên khi trúng đòn")]
        [SerializeField] SpriteRenderer flash;
        [Tooltip("Thanh máu: phần đầy (co giãn theo chiều ngang, gốc bên trái)")]
        [SerializeField] Transform barFill;
        [SerializeField] GameObject bar;
        [Tooltip("Lớp tơ phủ khi bị trói")]
        [SerializeField] SpriteRenderer web;
        [Tooltip("Kén tơ bọc ngoài")]
        [SerializeField] SpriteRenderer cocoon;
        [Tooltip("Sao xoay trên đầu khi choáng")]
        [SerializeField] Transform stars;
        [SerializeField] float respawnTime = 3f;
        [Tooltip("Không bị đánh bấy nhiêu giây thì hồi đầy máu")]
        [SerializeField] float regenDelay = 4f;
        [SerializeField] Color poisonColor = new Color(0.55f, 1f, 0.35f);
        [SerializeField] Color inkColor = new Color(0.28f, 0.18f, 0.42f);

        [Header("Bẹp / giãn khi trúng đòn")]
        [Tooltip("Đòn thường làm bia bẹp xuống bấy nhiêu (0.1 = thấp đi 10%, bè ra tương ứng); đòn nhỏ (laze, độc) bẹp ít hơn")]
        [SerializeField, Range(0f, 0.5f)] float squashLight = 0.1f;
        [Tooltip("Đòn nặng bẹp xuống bấy nhiêu")]
        [SerializeField, Range(0f, 0.5f)] float squashHeavy = 0.24f;
        [Tooltip("Bị hất tung lên mạnh hơn mức này (unit/giây) thì giãn dài ra thay vì bẹp xuống")]
        [SerializeField] float launchStretch = 4f;
        [Tooltip("Độ cứng lò xo bẹp-giãn: càng lớn nảy càng nhanh")]
        [SerializeField] float squashStiffness = 420f;
        [Tooltip("Tắt dần của bẹp-giãn: càng nhỏ càng nảy nhiều lần")]
        [SerializeField] float squashDamping = 15f;

        [Header("Lắc nghiêng, đẩy văng")]
        [Tooltip("Cú lắc nghiêng của đòn thường (độ / giây, quanh chân đế)")]
        [SerializeField] float tiltLight = 160f;
        [Tooltip("Cú lắc nghiêng của đòn nặng (độ / giây)")]
        [SerializeField] float tiltHeavy = 330f;
        [Tooltip("Nghiêng tối đa (độ)")]
        [SerializeField, Range(0f, 60f)] float maxTilt = 30f;
        [Tooltip("Độ cứng lò xo dựng bia thẳng lại")]
        [SerializeField] float tiltStiffness = 170f;
        [Tooltip("Tắt dần của lắc nghiêng: càng nhỏ càng lắc lư lâu")]
        [SerializeField] float tiltDamping = 7f;
        [Tooltip("Nhân thêm vào lực đẩy văng của đòn (lớn hơn 1 = văng xa, gắt hơn)")]
        [SerializeField, Min(0f)] float knockbackScale = 1.15f;
        [Tooltip("Độ cứng lò xo kéo bia về chỗ cũ")]
        [SerializeField] float returnStiffness = 24f;
        [Tooltip("Tắt dần khi về chỗ cũ")]
        [SerializeField] float returnDamping = 6f;

        [Header("Mảnh vỡ")]
        [Tooltip("Số mảnh đá văng ra khi trúng đòn nặng (đòn thường ra một phần ba)")]
        [SerializeField, Min(0)] int debris = 9;
        [SerializeField] Color debrisColor = new Color(0.68f, 0.62f, 0.8f);
        [Tooltip("Màu mảnh pha lê văng ra cùng mảnh đá")]
        [SerializeField] Color crystalColor = new Color(0.55f, 0.88f, 1f);
        [SerializeField] Color dustColor = new Color(0.55f, 0.5f, 0.62f, 0.6f);

        [Header("Choáng: vòng sao vàng quanh đầu")]
        [Tooltip("Vòng sáng elip trên đầu (con của Stars). Trống thì không vẽ vòng")]
        [SerializeField] SpriteRenderer dizzyRing;
        [Tooltip("Các ngôi sao chạy quanh vòng (con của Stars). Trống thì dùng mọi con của Stars")]
        [SerializeField] SpriteRenderer[] dizzyStars = new SpriteRenderer[0];
        [Tooltip("Bán kính vòng sao: x = ngang, y = dọc (elip dẹt, như vòng nằm ngang nhìn nghiêng)")]
        [SerializeField] Vector2 starOrbit = new Vector2(0.42f, 0.12f);
        [Tooltip("Tốc độ quay vòng sao (độ / giây)")]
        [SerializeField] float starSpeed = 260f;
        [Tooltip("Vòng sao lắc lư nghiêng qua lại bấy nhiêu độ (cho ra vẻ chóng mặt)")]
        [SerializeField] float starWobble = 9f;
        [Tooltip("Cỡ sao phía trước / phía sau vòng (sao phía sau nhỏ và mờ hơn)")]
        [SerializeField] Vector2 starScale = new Vector2(0.46f, 0.3f);

        // Phần trăm bán kính elip so với nửa khung ảnh vòng sao (GameFeelBuilder vẽ vòng chiếm 90% khung)
        const float RingFill = 0.9f;

        Health _health;
        Collider2D _collider;
        Vector3 _home;
        Vector2 _offset, _velocity;
        float _flash, _shake, _snare, _web, _ink, _cocoon, _stun;
        float _poisonDps, _poisonTime, _poisonTick;
        float _sinceHit = 99f, _dead;
        float _squash, _squashVelocity, _tilt, _tiltVelocity, _dizzy;
        Vector3 _artScale = Vector3.one, _starsLocal, _starsBaseScale = Vector3.one, _ringScale = Vector3.one;
        bool _flashChild;
        Transform[] _orbit = new Transform[0];
        SpriteRenderer[] _orbitSprites = new SpriteRenderer[0];
        int[] _orbitOrder = new int[0];

        public bool IsSnared => _snare > 0f;
        public bool IsCocooned => _cocoon > 0f;
        public bool IsStunned => _stun > 0f;
        public bool IsPoisoned => _poisonTime > 0f;
        public bool IsBroken => _dead > 0f;

        /// <summary>Lệch khỏi chỗ đứng bao nhiêu (bị đẩy / kéo).</summary>
        public Vector2 Displacement => _offset;

        /// <summary>Đang bẹp (dương) / giãn (âm) bao nhiêu.</summary>
        public float Squash => _squash;

        /// <summary>Đang nghiêng bao nhiêu độ (quanh chân đế).</summary>
        public float Tilt => art != null ? Mathf.DeltaAngle(0f, art.transform.localEulerAngles.z) : 0f;

        void Awake()
        {
            _health = GetComponent<Health>();
            _collider = GetComponent<Collider2D>();
            _home = transform.position;
            _artScale = art.transform.localScale;
            _flashChild = flash != null && flash.transform.parent == art.transform;
            if (stars != null)
            {
                _starsLocal = art.transform.InverseTransformPoint(stars.position); // vòng sao bám theo đầu khi bia nghiêng
                _starsBaseScale = stars.localScale;
                CollectStars();
            }

            if (dizzyRing != null)
                _ringScale = dizzyRing.transform.localScale;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        void OnDestroy()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        // Sao chạy vòng: dizzyStars nếu có, không thì mọi con của Stars (trừ vòng sáng)
        void CollectStars()
        {
            if (dizzyStars != null && dizzyStars.Length > 0)
            {
                _orbitSprites = dizzyStars;
                _orbit = new Transform[dizzyStars.Length];
                for (int i = 0; i < dizzyStars.Length; i++)
                    _orbit[i] = dizzyStars[i].transform;
            }
            else
            {
                var list = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in stars)
                {
                    if (dizzyRing == null || child != dizzyRing.transform)
                        list.Add(child);
                }

                _orbit = list.ToArray();
                _orbitSprites = new SpriteRenderer[_orbit.Length];
                for (int i = 0; i < _orbit.Length; i++)
                    _orbitSprites[i] = _orbit[i].GetComponent<SpriteRenderer>();
            }

            _orbitOrder = new int[_orbit.Length];
            for (int i = 0; i < _orbit.Length; i++)
                _orbitOrder[i] = _orbitSprites[i] != null ? _orbitSprites[i].sortingOrder : 0;
        }

        void OnDamaged(float amount, Vector2 from, HitEffect effect)
        {
            _sinceHit = 0f;
            if (amount > 0f)
                _flash = Mathf.Max(_flash, effect.heavy ? 1f : Mathf.Lerp(0.3f, 1f, amount / 15f)); // đòn nhỏ liên tục (laze, độc) chỉ loé nhẹ
            _shake = Mathf.Max(_shake, effect.heavy ? 1f : 0.5f);
            _velocity += effect.knockback * knockbackScale;
            _snare = Mathf.Max(_snare, effect.snare);
            if (effect.ink)
                _ink = Mathf.Max(_ink, effect.snare);
            else
                _web = Mathf.Max(_web, effect.snare);
            _stun = Mathf.Max(_stun, effect.stun);
            if (effect.cocoon)
                _cocoon = Mathf.Max(_cocoon, Mathf.Max(effect.stun, 1.5f));
            if (effect.poisonTime > 0f)
            {
                _poisonDps = Mathf.Max(effect.poisonDps, _poisonTime > 0f ? _poisonDps : 0f);
                _poisonTime = Mathf.Max(_poisonTime, effect.poisonTime);
            }

            React(amount, from, effect);
        }

        // Dáng trúng đòn: đặt ngay (giữ nguyên trong lúc khựng hình), rồi lò xo nảy về sau đó
        void React(float amount, Vector2 from, HitEffect effect)
        {
            // Đòn nhỏ (laze mỗi khung, độc) gần như không bẹp; đòn nặng bẹp nhiều
            float weight = effect.heavy ? 1f : Mathf.Clamp01(amount / 12f);
            float squash = effect.heavy ? squashHeavy * Mathf.Lerp(1f, 1.3f, Mathf.Clamp01((amount - 30f) / 60f)) : squashLight * weight;
            float pose = effect.knockback.y > launchStretch ? -squash * 0.8f : squash; // bị hất tung: giãn dài ra thay vì bẹp
            if (Mathf.Abs(pose) > Mathf.Abs(_squash))
            {
                _squash = pose;
                _squashVelocity = 0f;
            }

            // Lắc nghiêng quanh chân đế, ngả ra xa người đánh (đòn kéo lại thì ngả về phía người đánh)
            float side = Mathf.Abs(effect.knockback.x) > 0.05f ? Mathf.Sign(effect.knockback.x) : Mathf.Sign(transform.position.x - from.x + 1e-3f);
            float kick = (effect.heavy ? tiltHeavy : tiltLight * weight) * Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp01(Mathf.Abs(effect.knockback.x) / 6f));
            _tiltVelocity -= side * kick;

            // Mảnh đá (và vài mảnh pha lê) văng ra theo hướng đòn, bụi tung ở chân khi bị đánh mạnh
            if (CombatFx.Instance == null || amount < 8f)
                return;
            Vector2 at = transform.position + Vector3.up * 1f;
            Vector2 dir = effect.knockback.sqrMagnitude > 0.01f ? effect.knockback.normalized : new Vector2(side, 0.4f);
            int count = effect.heavy ? debris : debris / 3;
            if (count > 0)
            {
                CombatFx.Instance.Burst(at, debrisColor, count, effect.heavy ? 6.5f : 4.5f, 0.16f, 0.6f, 16f, dir * 0.8f + Vector2.up * 0.3f);
                CombatFx.Instance.Burst(at, crystalColor, Mathf.Max(1, count / 3), 5f, 0.08f, 0.45f, 12f, dir * 0.6f);
            }

            if (effect.heavy)
                CombatFx.Instance.Burst((Vector2)transform.position + Vector2.up * 0.1f, dustColor, 7, 2.2f, 0.32f, 0.5f, -1.5f, Vector2.up * 0.4f);
        }

        void OnDied()
        {
            _dead = respawnTime;
            _snare = _web = _ink = _cocoon = _stun = _poisonTime = 0f;
            _dizzy = 0f;
            Vector2 at = transform.position + Vector3.up * 0.9f;
            if (CombatFx.Instance != null)
            {
                CombatFx.Instance.Burst(at, new Color(0.55f, 0.85f, 1f), 26, 7f, 0.22f, 0.7f, 9f);
                CombatFx.Instance.Burst(at, new Color(0.35f, 0.3f, 0.45f), 18, 5f, 0.3f, 0.8f, 12f);
                CombatFx.Instance.Ring(at, 1.8f, new Color(0.6f, 0.9f, 1f, 0.8f));
            }

            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            art.enabled = visible;
            flash.enabled = visible;
            if (bar != null)
                bar.SetActive(visible);
            if (_collider != null)
                _collider.enabled = visible;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_dead > 0f)
            {
                _dead -= dt;
                if (_dead <= 0f)
                {
                    _health.Restore();
                    _offset = _velocity = Vector2.zero;
                    _squash = _squashVelocity = _tilt = _tiltVelocity = 0f;
                    _sinceHit = 99f;
                    SetVisible(true);
                    if (CombatFx.Instance != null)
                        CombatFx.Instance.Flash(transform.position + Vector3.up * 0.9f, 1.6f, new Color(0.55f, 0.85f, 1f, 0.8f), 0.4f);
                }

                UpdateOverlays(dt);
                return;
            }

            // Độc: mất máu dần, số màu xanh
            if (_poisonTime > 0f)
            {
                _poisonTime -= dt;
                _poisonTick -= dt;
                if (_poisonTick <= 0f)
                {
                    _poisonTick = 0.5f;
                    float dealt = _health.TakeHit(_poisonDps * 0.5f, transform.position, default);
                    DamageNumbers.Show(transform.position + Vector3.up * 1.6f, dealt, poisonColor, false, this);
                    if (CombatFx.Instance != null)
                        CombatFx.Instance.Burst(transform.position + Vector3.up * 1f, poisonColor, 4, 1.2f, 0.12f, 0.6f, -1.5f);
                }
            }

            _snare -= dt;
            _web -= dt;
            _ink -= dt;
            _stun -= dt;
            _cocoon -= dt;
            _sinceHit += dt;
            if (_sinceHit > regenDelay && _health.Current < _health.Max)
                _health.Heal(_health.Max * dt);

            // Bị đẩy / kéo: trôi đi rồi lò xo kéo về chỗ cũ (đang bị tơ trói / kén thì đứng im tại chỗ)
            if (_snare > 0f || _cocoon > 0f)
                _velocity *= 1f - Mathf.Min(1f, 12f * dt);
            _velocity += (-_offset * returnStiffness - _velocity * returnDamping) * dt;
            _offset += _velocity * dt;
            _offset = Vector2.ClampMagnitude(_offset, 4f);
            _offset.y = Mathf.Max(_offset.y, 0f);

            // Bẹp-giãn và lắc nghiêng: lò xo nảy tắt dần (chia bước nhỏ cho ổn định khi khung hình dài)
            int steps = Mathf.CeilToInt(dt * 240f);
            float h = steps > 0 ? dt / steps : 0f;
            bool held = _snare > 0f || _cocoon > 0f;
            for (int i = 0; i < steps; i++)
            {
                _squashVelocity += (-_squash * squashStiffness - _squashVelocity * squashDamping) * h;
                _squash += _squashVelocity * h;
                _tiltVelocity += (-_tilt * tiltStiffness - _tiltVelocity * (held ? tiltDamping * 3f : tiltDamping)) * h;
                _tilt += _tiltVelocity * h;
            }

            _tilt = Mathf.Clamp(_tilt, -maxTilt, maxTilt);
            _squash = Mathf.Clamp(_squash, -0.45f, 0.45f);

            _shake = Mathf.MoveTowards(_shake, 0f, dt * 4f);
            Vector2 jitter = Random.insideUnitCircle * (0.08f * _shake); // rung cả trong lúc khựng hình
            transform.position = _home + (Vector3)(_offset + jitter);
            float tilt = Mathf.Clamp(_tilt + _velocity.x * -1.5f, -maxTilt, maxTilt) + _shake * 4f * Mathf.Sin(Time.time * 50f);
            art.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            art.transform.localScale = Vector3.Scale(_artScale, new Vector3(1f + _squash * 0.85f, 1f - _squash, 1f));

            _flash = Mathf.MoveTowards(_flash, 0f, dt * 7f);
            flash.color = new Color(1f, 1f, 1f, 0.7f * _flash);
            if (!_flashChild)
                flash.transform.localRotation = art.transform.localRotation; // lớp loé là con của art thì tự theo
            Color tint = Color.white;
            if (_poisonTime > 0f)
                tint = Color.Lerp(tint, poisonColor, 0.35f + 0.15f * Mathf.Sin(Time.time * 8f));
            if (_ink > 0f)
                tint = Color.Lerp(tint, inkColor, 0.6f);
            art.color = tint;

            if (barFill != null)
                barFill.localScale = new Vector3(Mathf.Clamp01(_health.Current / _health.Max), 1f, 1f);
            UpdateOverlays(dt);
        }

        void UpdateOverlays(float dt)
        {
            bool alive = _dead <= 0f;
            if (web != null)
            {
                web.enabled = alive && _web > 0f;
                web.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 20f);
            }

            if (cocoon != null)
                cocoon.enabled = alive && _cocoon > 0f;
            if (stars != null)
                UpdateStars(alive && _stun > 0f, dt);
        }

        // Vòng sao vàng trên đầu: bật ra (phóng to quá cỡ rồi co về), sao chạy quanh elip, sao phía trước to và sáng hơn
        // sao phía sau, cả vòng lắc lư; hết choáng thì co lại biến mất
        void UpdateStars(bool stunned, float dt)
        {
            _dizzy = Mathf.MoveTowards(_dizzy, stunned ? 1f : 0f, dt * (stunned ? 5f : 4f));
            bool show = _dizzy > 0f;
            if (stars.gameObject.activeSelf != show)
                stars.gameObject.SetActive(show);
            if (!show)
                return;

            float t = Time.time;
            float pop = stunned ? BackOut(_dizzy) : _dizzy;
            stars.position = art.transform.TransformPoint(_starsLocal) + Vector3.up * (0.03f * Mathf.Sin(t * 5f));
            stars.localRotation = Quaternion.Euler(0f, 0f, starWobble * Mathf.Sin(t * 3.1f));
            stars.localScale = _starsBaseScale * pop;

            if (dizzyRing != null && dizzyRing.sprite != null)
            {
                Vector2 extents = dizzyRing.sprite.bounds.extents * RingFill;
                dizzyRing.transform.localPosition = Vector3.zero;
                dizzyRing.transform.localScale = new Vector3(starOrbit.x / Mathf.Max(extents.x, 1e-3f), starOrbit.y / Mathf.Max(extents.y, 1e-3f), 1f);
                Color c = dizzyRing.color;
                c.a = 0.85f * _dizzy;
                dizzyRing.color = c;
            }

            for (int i = 0; i < _orbit.Length; i++)
            {
                if (_orbit[i] == null)
                    continue;
                float a = (t * starSpeed + i * 360f / _orbit.Length) * Mathf.Deg2Rad;
                float depth = -Mathf.Sin(a); // 1 = phía trước (dưới elip), -1 = phía sau
                float front = depth * 0.5f + 0.5f;
                _orbit[i].localPosition = new Vector3(Mathf.Cos(a) * starOrbit.x, Mathf.Sin(a) * starOrbit.y, 0f);
                _orbit[i].localRotation = Quaternion.Euler(0f, 0f, t * 200f + i * 50f);
                _orbit[i].localScale = Vector3.one * Mathf.Lerp(starScale.y, starScale.x, front);
                SpriteRenderer sprite = _orbitSprites[i];
                if (sprite != null)
                {
                    Color c = sprite.color;
                    c.a = Mathf.Lerp(0.55f, 1f, front) * _dizzy;
                    sprite.color = c;
                    sprite.sortingOrder = _orbitOrder[i] + (depth > 0f ? 2 : -2); // sao phía trước vẽ đè lên vòng
                }
            }
        }

        static float BackOut(float x)
        {
            const float c = 1.9f;
            float u = x - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }
    }
}
