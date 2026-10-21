using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Octopus
{
    /// <summary>Các đòn của boss sư tử.</summary>
    public enum LionAttack
    {
        None,
        ClawSwipe, // Vả vuốt
        Pounce,    // Vồ
        Roar,      // Gầm
        Fireballs, // Cầu lửa từ bờm (pha 2+)
        Charge,    // Húc lao (pha 2+)
        FireRain,  // Mưa lửa (pha 3)
    }

    /// <summary>
    /// Boss "SƯ TỬ HẮC VIÊM" (Docs/boss_su_tu/PHAN_TICH_BOSS.md): to gấp ~5 lần nhân vật, mạnh dưới sàn nhưng chậm.
    ///  - Máu 6000, 3 pha: 100–60% (vả vuốt, vồ, gầm), 60–30% (thêm cầu lửa, húc lao), dưới 30% nổi giận (nhanh hơn 25%,
    ///    nối 2 đòn liền, mưa lửa mỗi 15 giây). Đổi pha thì gầm.
    ///  - Mọi đòn có báo trước (dáng lấy đà + hiệu ứng + vòng đỏ); quay mặt về phía người chơi trước mỗi đòn; nghỉ giữa hai đòn
    ///    1.2 / 0.9 / 0.6 giây theo pha; người chơi ở xa thì đi tới.
    ///  - Da cứng: không bị đẩy, không bị kéo; choáng chỉ còn 30% thời gian, trói / kén tối đa 0.6 giây, độc ăn đủ.
    ///  - Choáng gục: nhận ≥ 600 sát thương trong 6 giây thì gục 2 giây, nhận thêm 25% sát thương.
    ///  - Bị đóng băng (Health.Freeze, chiêu ngưng đọng thời gian): đứng hình — đòn đang ra, bước đi, hình, cầu lửa / thiên thạch
    ///    / sàn cháy / sóng chấn động đều dừng và không gây sát thương; hết đóng băng thì chạy tiếp từ chỗ dừng.
    /// Hình boss là ILionVisual (LionRigBuilder dựng); AI chỉ nói chuyện với hình qua giao diện đó. Hitbox thân (Health +
    /// BoxCollider2D, lớp Enemy) phủ khung hình; gốc của Health luôn dời tới chỗ trên thân gần người chơi nhất để chiêu nhắm,
    /// móc kéo, phi tơ và số sát thương hiện đúng chỗ bị đánh.
    /// </summary>
    [RequireComponent(typeof(LionHazards))]
    public class LionBoss : MonoBehaviour
    {
        public const string DisplayName = "SƯ TỬ HẮC VIÊM";

        [Header("Tham chiếu")]
        [Tooltip("Máu boss (trên object Hurtbox, cùng BoxCollider2D lớp Enemy)")]
        [SerializeField] Health health;
        [SerializeField] BoxCollider2D hurtbox;
        [Tooltip("Người chơi")]
        [SerializeField] PlayerHealth target;
        [Tooltip("Hình boss (component có ILionVisual). Trống thì tìm trong con")]
        [SerializeField] MonoBehaviour visual;
        [Tooltip("Quầng đỏ sau lưng boss")]
        [SerializeField] SpriteRenderer backLight;
        [Tooltip("Vòng sao quay trên đầu khi choáng (các con là ngôi sao)")]
        [SerializeField] Transform stunStars;
        [Tooltip("Mặt sàn + đá: chỗ thiên thạch rơi xuống")]
        [SerializeField] LayerMask surfaceMask;

        [Header("Thân hình, đấu trường")]
        [Tooltip("Chiều cao hình boss (unit)")]
        [SerializeField, Min(1f)] float height = 9.5f;
        [Tooltip("Độ cao mặt sàn đấu trường")]
        [SerializeField] float floorY;
        [Tooltip("Mép trong của 2 vách đấu trường (x)")]
        [SerializeField] Vector2 arenaX = new Vector2(-28f, 28f);
        [Tooltip("Trần hang (thiên thạch rơi từ trên đó)")]
        [SerializeField] float ceilingY = 18f;
        [Tooltip("Hitbox nhận đòn = khung hình (VisualBounds, không tính đuôi) nhân tỉ lệ này (rộng, cao)")]
        [SerializeField] Vector2 hurtboxScale = new Vector2(0.92f, 0.95f);
        [Tooltip("Thân đè / húc trúng người chơi = khung hình nhân tỉ lệ này (nhỏ hơn hitbox nhận đòn cho dễ né)")]
        [SerializeField] Vector2 attackBodyScale = new Vector2(0.8f, 0.85f);

        [Header("Nhịp trận")]
        [Tooltip("Chờ bấy nhiêu giây rồi gầm mở màn")]
        [SerializeField, Min(0f)] float introDelay = 0.8f;
        [Tooltip("Tốc độ đi (unit/giây). Hình đi đẹp ở 2.7–5.4 (cao 9.5), nhanh hơn ~5.4 thì chuyển sang phi")]
        [SerializeField, Min(0f)] float walkSpeed = 3.2f;
        [Tooltip("Nghỉ giữa hai đòn ở pha 1, 2, 3 (giây)")]
        [SerializeField] float[] restTime = { 1.2f, 0.9f, 0.6f };
        [Tooltip("Còn bấy nhiêu phần máu thì sang pha 2 / pha 3")]
        [SerializeField, Range(0f, 1f)] float phase2At = 0.6f;
        [SerializeField, Range(0f, 1f)] float phase3At = 0.3f;
        [Tooltip("Pha 3 (nổi giận): mọi thứ nhanh gấp mấy lần")]
        [SerializeField, Min(1f)] float enrageSpeed = 1.25f;
        [Tooltip("Pha 3: tỉ lệ nối thêm đòn thứ hai ngay sau đòn đầu")]
        [SerializeField, Range(0f, 1f)] float comboChance = 0.75f;
        [Tooltip("Quay đầu mất bấy nhiêu giây (hình quay mượt, AI chờ quay xong mới ra đòn)")]
        [SerializeField, Min(0f)] float turnTime = 0.25f;
        [Tooltip("Người chơi cao hơn sàn bấy nhiêu unit là 'ở trên cao'")]
        [SerializeField] float highAbove = 7f;
        [Tooltip("Ở trên cao lâu hơn bấy nhiêu giây thì boss gầm")]
        [SerializeField] float highTimeForRoar = 6f;
        [Tooltip("Người chơi cao hơn sàn bấy nhiêu thì ngoài tầm vồ / cầu lửa (tầng đá cao)")]
        [SerializeField] float outOfReachAbove = 11f;

        [Header("Khống chế, choáng gục")]
        [Tooltip("Choáng chỉ còn tỉ lệ này thời gian")]
        [SerializeField, Range(0f, 1f)] float stunScale = 0.3f;
        [Tooltip("Trói / kén tơ giữ boss tối đa bấy nhiêu giây")]
        [SerializeField, Min(0f)] float maxHold = 0.6f;
        [Tooltip("Nhận ≥ chừng này sát thương trong Stagger Window giây thì choáng gục")]
        [SerializeField, Min(1f)] float staggerDamage = 600f;
        [SerializeField, Min(0.1f)] float staggerWindow = 6f;
        [SerializeField, Min(0f)] float staggerTime = 2f;
        [Tooltip("Đang choáng gục nhận sát thương nhân thêm")]
        [SerializeField, Min(1f)] float staggerBonus = 1.25f;
        [SerializeField] Color poisonColor = new Color(0.55f, 1f, 0.35f);

        [Header("Khớp với hoạt ảnh (LionRigAnimator)")]
        [Tooltip("Phần thời gian nhát vả lúc móng quét qua trước mặt: vả 1, vả 2")]
        [SerializeField] Vector2 clawCross = new Vector2(0.46f, 0.44f);
        [Tooltip("Gầm: phần thời gian lúc há miệng gầm (bắn vòng sóng âm)")]
        [SerializeField, Range(0.1f, 0.6f)] float roarShockAt = 0.32f;
        [Tooltip("Gầm: thời lượng dáng gầm (giây), không ngắn / dài hơn khoảng này")]
        [SerializeField] Vector2 roarTime = new Vector2(0.8f, 1.6f);
        [Tooltip("Lắc bờm: khoảng mạnh nhất (phần thời gian) để phóng cầu lửa")]
        [SerializeField] Vector2 shakeWindow = new Vector2(0.25f, 0.78f);
        [Tooltip("Lấy đà húc: 2 lần cào đất (phần thời gian)")]
        [SerializeField] Vector2 scrapeAt = new Vector2(0.4f, 0.7f);
        [Tooltip("Đáp đất (giây)")]
        [SerializeField] float landTime = 0.5f;
        [Tooltip("Nằm gục xong đứng dậy mất bấy nhiêu giây")]
        [SerializeField] float getUpTime = 0.6f;
        [Tooltip("Trúng đòn nặng lúc đứng không: giật mình bấy nhiêu giây")]
        [SerializeField] float hurtTime = 0.35f;

        [Header("Vả vuốt")]
        [Tooltip("Chồm đứng báo trước (giây, hình cần ≥ 0.45)")]
        [SerializeField] float clawWindup = 0.5f;
        [SerializeField] float clawDamage = 30f;
        [Tooltip("Mỗi nhát vả dài bấy nhiêu giây (hình đẹp ở 0.4–0.55)")]
        [SerializeField] float clawSwipeTime = 0.48f;
        [Tooltip("Vuốt với ra trước mặt (mép đầu / bờm) bấy nhiêu unit")]
        [SerializeField] float clawReach = 2.6f;
        [Tooltip("Vuốt với lên tới bấy nhiêu phần chiều cao boss (tầng đá thấp ~5 unit thì trúng, tầng giữa ~9 thì không)")]
        [SerializeField, Range(0.3f, 1.2f)] float clawHeight = 0.72f;
        [SerializeField] Vector2 clawKnockback = new Vector2(8f, 6f);

        [Header("Vồ")]
        [SerializeField] float pounceWindup = 0.6f;
        [SerializeField] float pounceFlight = 0.8f;
        [Tooltip("Bị thân boss đè / va lúc bay")]
        [SerializeField] float pounceDamage = 40f;
        [Tooltip("Sóng chấn động lan trên sàn lúc đáp")]
        [SerializeField] float pounceWaveDamage = 20f;
        [SerializeField] float pounceWaveRange = 14f;
        [SerializeField] float pounceWaveSpeed = 16f;
        [Tooltip("Nhảy cao tối đa (unit): thân boss với tới tầng đá giữa, không tới tầng cao")]
        [SerializeField] float pounceMaxLift = 2.2f;
        [SerializeField] Vector2 pounceKnockback = new Vector2(10f, 9f);

        [Header("Gầm")]
        [SerializeField] float roarWindup = 0.5f;
        [SerializeField] float roarRadius = 14f;
        [SerializeField] float roarDamage = 15f;
        [Tooltip("Vòng sóng âm lan ra với tốc độ này (unit/giây)")]
        [SerializeField] float roarSpeed = 30f;
        [SerializeField] Vector2 roarPush = new Vector2(11f, 6f);

        [Header("Cầu lửa (pha 2+)")]
        [Tooltip("Lắc bờm, đốm lửa tụ lại (giây) trước quả đầu tiên")]
        [SerializeField] float fireballWindup = 0.55f;
        [Tooltip("Các quả cầu lửa phóng cách nhau bấy nhiêu giây")]
        [SerializeField] float fireballInterval = 0.05f;
        [SerializeField] int fireballCount = 5;
        [SerializeField] float fireballDamage = 25f;
        [Tooltip("Các quả cách nhau bấy nhiêu unit quanh người chơi")]
        [SerializeField] float fireballSpread = 2.2f;
        [SerializeField] float fireballFlight = 1f;
        [SerializeField] float fireballGravity = 14f;
        [Tooltip("Cầu lửa không bay cao hơn mặt sàn bấy nhiêu unit (tầng đá cao an toàn)")]
        [SerializeField] float fireballMaxHeight = 10.5f;
        [SerializeField] float burnTime = 2f;
        [SerializeField] float burnDps = 10f;
        [SerializeField] float burnWidth = 2.2f;

        [Header("Húc lao (pha 2+)")]
        [Tooltip("Cào chân lấy đà (giây, hình 0.6–0.8: cào 2 lần)")]
        [SerializeField] float chargeWindup = 0.7f;
        [Tooltip("Tốc độ lao (unit/giây). Hình phi đẹp ở 16–22 (cao 9.5)")]
        [SerializeField] float chargeSpeed = 18f;
        [SerializeField] float chargeDamage = 45f;
        [SerializeField] Vector2 chargeLaunch = new Vector2(9f, 14f);
        [Tooltip("Đâm vách thì choáng bấy nhiêu giây")]
        [SerializeField] float wallStun = 1.2f;

        [Header("Mưa lửa (pha 3)")]
        [SerializeField] float fireRainInterval = 15f;
        [Tooltip("Vào pha 3 bấy nhiêu giây thì mưa lửa lần đầu")]
        [SerializeField] float fireRainFirst = 3f;
        [SerializeField] int fireRainCount = 8;
        [Tooltip("Vòng đỏ hiện bấy nhiêu giây trước khi thiên thạch rơi")]
        [SerializeField] float fireRainWarn = 1f;
        [SerializeField] float meteorFall = 0.35f;
        [SerializeField] float meteorDamage = 30f;
        [SerializeField] float meteorRadius = 1.7f;

        [Header("Màu")]
        [SerializeField] Color clawColor = new Color(1f, 0.16f, 0.1f);
        [SerializeField] Color clawEdge = new Color(0.45f, 0.02f, 0.05f);
        [SerializeField] Color fireColor = new Color(1f, 0.45f, 0.12f);
        [SerializeField] Color roarColor = new Color(1f, 0.55f, 0.25f, 0.85f);
        [SerializeField] Color dustColor = new Color(0.78f, 0.48f, 0.34f, 0.7f);
        [SerializeField] Color rockColor = new Color(0.3f, 0.12f, 0.1f);

        ILionVisual _visual;
        LionHazards _hazards;
        Coroutine _brain;
        LionHazards.Marker _marker; // vòng báo trước của đòn đang ra (dọn khi bị ngắt)
        readonly Queue<(float time, float amount)> _recent = new Queue<(float, float)>();
        float _recentSum;
        float _facing = -1f;
        float _front, _back, _centerOffset; // mép trước / sau của hình tính từ gốc (giữa hai bàn chân), tâm khung lệch về trước
        float _lastX;
        float _stun, _hold, _wallStun;
        float _poisonDps, _poisonTime, _poisonTick;
        float _highTime, _fireRainTimer;
        float _enrage;
        float _fightStart = -1f, _fightEnd = -1f;
        float _starsAlpha;
        float _glow; // quầng đỏ sau lưng rực thêm (báo trước gầm, mưa lửa)
        bool _aiEnabled = true;
        bool _airborne, _staggerPending, _phaseRoarPending;
        LionAttack _last;
        int _repeat;
        int _walks; // số lần liền chỉ đi tới mà chưa ra đòn

        public Health Health => health;
        public PlayerHealth Target => target;
        public ILionVisual Visual => _visual;
        public float Height => height;
        public float FloorY => floorY;
        public LionHazards Hazards => _hazards;

        /// <summary>Pha hiện tại: 1, 2, 3.</summary>
        public int Phase { get; private set; } = 1;
        public bool IsDead { get; private set; }
        public bool IsStaggered { get; private set; }
        public bool IsStunned => _stun > 0f || _wallStun > 0f;
        public float StunRemaining => Mathf.Max(_stun, _wallStun);
        public float HoldRemaining => Mathf.Max(0f, _hold);
        public bool IsPoisoned => _poisonTime > 0f;
        public bool IsAirborne => _airborne;

        /// <summary>Đã gầm mở màn, trận đấu bắt đầu.</summary>
        public bool Engaged { get; private set; }

        /// <summary>Đòn đang ra (None = đang đi / nghỉ).</summary>
        public LionAttack Current { get; private set; }

        /// <summary>Số lần đã ra từng đòn (test, cân bằng).</summary>
        public int AttackCount(LionAttack attack) => _counts.TryGetValue(attack, out int n) ? n : 0;

        readonly Dictionary<LionAttack, int> _counts = new Dictionary<LionAttack, int>();

        public float HealthFraction => health != null ? health.Current / health.Max : 0f;
        public float Facing => _facing;
        float X => transform.position.x;

        /// <summary>Thời gian đánh (giây) tính từ lúc gầm mở màn tới lúc boss gục.</summary>
        public float FightTime => _fightStart < 0f ? 0f : (_fightEnd >= 0f ? _fightEnd : Time.time) - _fightStart;

        /// <summary>Khung thân nhận đòn (world): hitbox của boss.</summary>
        public Bounds Body => Scaled(hurtboxScale);

        /// <summary>Khung thân đè / húc trúng người chơi (world), nhỏ hơn Body.</summary>
        public Bounds AttackBody => Scaled(attackBodyScale);

        Bounds Scaled(Vector2 scale)
        {
            Bounds b = _visual != null ? _visual.VisualBounds : new Bounds(transform.position + Vector3.up * height * 0.5f, new Vector3(height * 1.4f, height, 0f));
            var size = new Vector3(b.size.x * scale.x, b.size.y * scale.y, 0f);
            return new Bounds(new Vector3(b.center.x, Mathf.Max(b.min.y, floorY) + size.y * 0.5f, 0f), size);
        }

        public event Action<int> PhaseChanged;
        public event Action Staggered;
        public event Action<LionAttack> AttackStarted;

        /// <summary>Hết máu (gọi ngay).</summary>
        public event Action Died;

        /// <summary>Đã gục hẳn, lửa tắt: tới lúc hiện màn thắng.</summary>
        public event Action Defeated;

        /// <summary>Đang bị đóng băng (ngưng đọng thời gian): đứng hình, đòn và đạn dừng lại.</summary>
        public bool IsFrozen => health != null && health.IsFrozen && !IsDead;

        // Pha 3 nhanh hơn; choáng / đóng băng thì đứng hình (các nhịp chờ dừng lại)
        float PhaseSpeed => Phase >= 3 ? enrageSpeed : 1f;
        float Tempo => IsStunned || IsFrozen ? 0f : PhaseSpeed;

        // Thời gian trôi của boss trong frame này: đóng băng thì 0
        float Dt => IsFrozen ? 0f : Time.deltaTime;

        void Awake()
        {
            _hazards = GetComponent<LionHazards>();
            _visual = visual as ILionVisual;
            if (_visual == null)
                _visual = GetComponentInChildren<ILionVisual>();
            if (health == null)
                health = GetComponentInChildren<Health>();
            if (hurtbox == null && health != null)
                hurtbox = health.GetComponent<BoxCollider2D>();
            if (target == null)
                target = FindAnyObjectByType<PlayerHealth>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (target != null)
                target.Died += OnPlayerDied;
        }

        void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            if (target != null)
                target.Died -= OnPlayerDied;
        }

        void Start()
        {
            _hazards.Target = target;
            _hazards.FloorY = floorY;
            MeasureVisual();
            _lastX = X;
            if (target != null)
                _facing = Mathf.Sign(target.Center.x - X + 1e-3f);
            if (_visual != null)
                _visual.Facing = _facing;
            UpdateHurtbox();
            _brain = StartCoroutine(Intro());
        }

        // Hình sư tử lệch về phía đầu: mép trước (đầu, bờm, móng trước) xa gốc hơn mép sau (hông; đuôi không tính).
        // Đo lúc đứng yên, lấy theo độ lớn nên không phụ thuộc hình đang quay về phía nào
        void MeasureVisual()
        {
            Bounds b = _visual != null ? _visual.VisualBounds : default;
            float a = b.max.x - X, c = X - b.min.x;
            _front = Mathf.Max(a, c);
            _back = Mathf.Min(a, c);
            _centerOffset = Mathf.Abs(b.center.x - X);
            if (_front < height * 0.3f)
            {
                // hình chưa kịp tính khung: lấy cỡ sư tử thường (dài ~1.4 lần cao)
                _front = _back = height * 0.7f;
                _centerOffset = 0f;
            }
        }

        // =============================================================================================================
        // Điều khiển từ ngoài (test, chỉnh thông số)
        // =============================================================================================================

        /// <summary>Bật / tắt AI. Tắt thì boss đứng yên (vẫn nhận đòn, vẫn choáng gục / chết).</summary>
        public void SetAi(bool enabled)
        {
            _aiEnabled = enabled;
            if (IsDead)
                return;
            if (!enabled)
                Interrupt();
            else if (_brain == null && !IsStaggered)
                _brain = StartCoroutine(Brain(LionAttack.None));
        }

        /// <summary>Ra ngay đòn attack (bỏ đòn đang ra), xong thì AI chạy tiếp nếu đang bật.</summary>
        public void ForceAttack(LionAttack attack)
        {
            if (IsDead)
                return;
            Interrupt();
            Engage();
            _brain = StartCoroutine(Brain(attack));
        }

        // =============================================================================================================
        // Bộ não
        // =============================================================================================================

        IEnumerator Intro()
        {
            for (float t = 0f; t < introDelay; t += Dt)
                yield return null;
            Current = LionAttack.Roar;
            yield return RoarRoutine(false);
            Current = LionAttack.None;
            Engage();
            yield return Brain(LionAttack.None);
        }

        void Engage()
        {
            if (Engaged)
                return;
            Engaged = true;
            _fightStart = Time.time;
        }

        IEnumerator Brain(LionAttack first)
        {
            Engage();
            if (first != LionAttack.None)
            {
                yield return Run(first);
                yield return Rest();
            }

            while (true)
            {
                if (!_aiEnabled || target == null || target.IsDead)
                {
                    _brain = null;
                    yield break;
                }

                if (_phaseRoarPending)
                {
                    _phaseRoarPending = false;
                    yield return Run(LionAttack.Roar);
                    yield return Rest();
                    continue;
                }

                LionAttack next = Choose();
                if (next == LionAttack.None)
                {
                    _walks++;
                    yield return Approach(1.2f);
                    continue;
                }

                _walks = 0;

                yield return Run(next);

                // Pha 3: nối đòn thứ hai ngay, không nghỉ
                if (Phase >= 3 && Random.value < comboChance && target != null && !target.IsDead)
                {
                    LionAttack second = Choose();
                    if (second != LionAttack.None && second != next && second != LionAttack.FireRain)
                        yield return Run(second);
                }

                yield return Rest();
            }
        }

        IEnumerator Run(LionAttack attack)
        {
            Current = attack;
            _repeat = attack == _last ? _repeat + 1 : 1;
            _last = attack;
            _counts[attack] = AttackCount(attack) + 1;
            AttackStarted?.Invoke(attack);
            yield return FaceTarget();
            switch (attack)
            {
                case LionAttack.ClawSwipe:
                    yield return ClawRoutine();
                    break;
                case LionAttack.Pounce:
                    yield return PounceRoutine();
                    break;
                case LionAttack.Roar:
                    yield return RoarRoutine(true);
                    break;
                case LionAttack.Fireballs:
                    yield return FireballRoutine();
                    break;
                case LionAttack.Charge:
                    yield return ChargeRoutine();
                    break;
                case LionAttack.FireRain:
                    yield return FireRainRoutine();
                    break;
            }

            Current = LionAttack.None;
        }

        IEnumerator Rest()
        {
            float rest = restTime[Mathf.Clamp(Phase - 1, 0, restTime.Length - 1)];
            yield return Wait(rest * PhaseSpeed); // Wait chia cho tốc độ pha; nghỉ đã tính riêng theo pha
        }

        /// <summary>Chọn đòn theo chỗ người chơi đang ở (bảng "Khi nào" trong tài liệu). None = đi tới gần hơn.</summary>
        LionAttack Choose()
        {
            if (target == null || target.IsDead)
                return LionAttack.None;
            if (Phase >= 3 && _fireRainTimer <= 0f)
                return LionAttack.FireRain;
            if (_highTime >= highTimeForRoar)
                return LionAttack.Roar;

            Vector2 p = target.Center;
            float side = Mathf.Sign(p.x - X + 1e-3f);
            float gap = Mathf.Abs(p.x - X) - _front; // khoảng cách tới mặt boss (đã quay về phía người chơi)
            float up = p.y - floorY;
            if (InClawZone(p, side))
                return Varied(LionAttack.ClawSwipe, Phase >= 2 ? LionAttack.Roar : LionAttack.Pounce);

            if (up < 3.2f) // dưới sàn (kể cả đang nhảy)
            {
                bool restless = _walks >= 2; // đi mãi chưa tới: vồ / húc luôn cho khỏi lững thững
                if (Phase >= 2 && gap > 8f && (restless || Random.value < 0.5f))
                    return Varied(LionAttack.Charge, LionAttack.Pounce);
                if (gap > 4f && (restless || Random.value < 0.6f))
                    return Varied(LionAttack.Pounce, Phase >= 2 && gap > 8f ? LionAttack.Charge : LionAttack.None);
                return LionAttack.None;
            }

            if (up > outOfReachAbove)
                return LionAttack.None; // tầng đá cao: đi tới dưới chân, ở lâu thì gầm

            if (Phase >= 2 && Random.value < 0.55f)
                return Varied(LionAttack.Fireballs, LionAttack.Pounce);
            return Varied(LionAttack.Pounce, Phase >= 2 ? LionAttack.Fireballs : LionAttack.None);
        }

        // Không ra một đòn quá 2 lần liền
        LionAttack Varied(LionAttack wanted, LionAttack instead) => wanted == _last && _repeat >= 2 ? instead : wanted;

        // Trước mặt (từ ngực tới quá đầu clawReach), thấp hơn tầm vuốt
        bool InClawZone(Vector2 p, float side)
        {
            float ahead = (p.x - X) * side;
            return ahead > _front * 0.3f && ahead < _front + clawReach && p.y - floorY < height * clawHeight;
        }

        // Đi tới: người chơi dưới sàn thì đứng cho họ lọt vào tầm vuốt; trên đá thì tới dưới chân họ
        IEnumerator Approach(float maxTime)
        {
            Current = LionAttack.None;
            for (float t = 0f; t < maxTime; t += Dt)
            {
                if (target == null || target.IsDead)
                    yield break;
                Vector2 p = target.Center;
                float side = Mathf.Sign(p.x - X + 1e-3f);
                if (t > 0.2f && InClawZone(p, side))
                    yield break;

                float goal = p.y - floorY < 3.2f ? p.x - side * (_front + clawReach * 0.5f) : p.x - side * _centerOffset;
                goal = ClampX(goal);
                float dx = goal - X;
                if (Mathf.Abs(dx) < 0.3f)
                    yield break;

                float dir = Mathf.Sign(dx);
                if (dir != _facing)
                {
                    SetFacing(dir);
                    yield return Wait(turnTime);
                    continue;
                }

                if (_hold <= 0f)
                    SetX(X + dir * Mathf.Min(Mathf.Abs(dx), walkSpeed * Tempo * Time.deltaTime));
                yield return null;
            }
        }

        IEnumerator FaceTarget()
        {
            if (target == null)
                yield break;
            float want = Mathf.Sign(target.Center.x - X + 1e-3f);
            if (want == _facing)
                yield break;
            SetFacing(want);
            yield return Wait(turnTime);
        }

        void SetFacing(float facing)
        {
            _facing = facing < 0f ? -1f : 1f;
            if (_visual != null)
                _visual.Facing = _facing;
        }

        // Chờ theo đồng hồ của boss: pha 3 nhanh hơn, đang choáng thì dừng
        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime * Tempo)
                yield return null;
        }

        void Act(LionPose pose, float seconds, bool hold = false) => _visual?.Act(pose, seconds / PhaseSpeed, hold);

        // =============================================================================================================
        // Các đòn
        // =============================================================================================================

        // Vả vuốt: chồm lên bằng 2 chân sau (mắt loé), rồi vả 2 nhát (RearUp → ClawSwipe → ClawSwipeAlt nối liền). Mỗi nhát
        // một vệt lưỡi liềm đỏ trước mặt; trúng đúng lúc móng quét qua trước mặt (clawCross của hình)
        IEnumerator ClawRoutine()
        {
            Act(LionPose.RearUp, clawWindup + 0.1f); // chồm xong thì giữ dáng tới nhát vả
            Fx?.Flash(Mouth + Vector2.up * height * 0.06f, 3f, clawColor, clawWindup); // mắt loé đỏ
            yield return Wait(clawWindup);

            for (int i = 0; i < 2; i++)
            {
                bool alt = i == 1;
                float cross = alt ? clawCross.y : clawCross.x;
                float f = _facing;
                Act(alt ? LionPose.ClawSwipeAlt : LionPose.ClawSwipe, clawSwipeTime);

                // Vệt lưỡi liềm hiện sớm một nhịp để đầu vệt quét qua trước mặt đúng lúc móng tới
                yield return Wait(clawSwipeTime * (cross - 0.14f));
                float A(float a) => f > 0f ? a : 180f - a;
                Vector2 c = new Vector2(X + f * _front * 0.45f, floorY + height * 0.47f);
                float r = _front * 0.6f + clawReach * 0.55f;
                Fx?.Slash(c, r, A(alt ? 120f : 100f), A(alt ? -70f : -60f), clawColor, clawEdge, 2.4f, 0.4f);
                Fx?.Slash(c, r * 0.72f, A(alt ? 110f : 95f), A(alt ? -60f : -50f), clawColor, clawEdge, 1.2f, 0.32f, 1f, 0.03f);
                Fx?.Claw(c, r * 1.06f, A(alt ? 110f : 95f), A(alt ? -60f : -50f), clawColor, clawEdge, 0.45f, 0.36f, 3, 0.55f);
                yield return Wait(clawSwipeTime * 0.14f);

                // Móng qua trước mặt: loé + tia lửa ở đầu móng, đánh trúng trong lúc móng còn quét xuống (tới ~0.62)
                Fx?.Flash(Paw, 2.5f, clawColor, 0.2f);
                Fx?.Burst(Paw, clawColor, 8, 6f, 0.16f, 0.3f, 6f, new Vector2(f, -0.4f));
                CameraShake.Impulse(new Vector2(f, -0.5f), 0.5f);
                float face = X + f * _front;
                var zone = Rect.MinMaxRect(Mathf.Min(face - f * _front * 0.7f, face + f * clawReach), floorY - 1f,
                    Mathf.Max(face - f * _front * 0.7f, face + f * clawReach), floorY + height * clawHeight);
                bool hit = false;
                float window = clawSwipeTime * Mathf.Max(0.05f, 0.62f - cross);
                for (float t = 0f; t < window && !hit; t += Time.deltaTime * Tempo)
                {
                    if (PlayerIn(zone))
                        hit = HitPlayer(clawDamage, transform.position, clawKnockback, false);
                    yield return null;
                }

                yield return Wait(clawSwipeTime * (1f - cross) - window);
            }

            yield return Wait(0.15f);
        }

        // Vồ: thu mình (vòng đỏ ở chỗ sắp đáp), nhảy vòng cung tới chỗ người chơi (thân đè lên họ), đáp xuống tạo sóng chấn
        // động trên sàn. Crouch → Leap (đúng thời gian bay) → Land nối liền
        IEnumerator PounceRoutine()
        {
            if (target == null)
                yield break;
            Vector2 p = target.Center;
            float f = _facing;
            float landX = ClampX(p.x - f * _centerOffset); // tâm thân (lệch về trước gốc) đáp ngay chỗ người chơi
            Vector2 spot = new Vector2(landX + f * _centerOffset, floorY);
            _marker = _hazards.ShowMarker(spot, (_front + _back) * 0.4f, (pounceWindup + pounceFlight) / PhaseSpeed);
            Act(LionPose.Crouch, pounceWindup);
            Fx?.DustCloud(new Vector2(X, floorY), _back + 1f, dustColor, 6);
            yield return Wait(pounceWindup);

            // Thân boss bay qua chỗ người chơi (nếu họ ở trên đá thấp / giữa), không quá tầng cao
            float lift = Mathf.Clamp(p.y - floorY - height * 0.55f, 0.8f, pounceMaxLift);
            float startX = X;
            Act(LionPose.Leap, pounceFlight);
            Fx?.Debris(new Vector2(X, floorY), rockColor, 6, 5f, 0.3f);
            Fx?.DustCloud(new Vector2(X - f * _back * 0.5f, floorY), 1.6f, dustColor, 6);
            _airborne = true;
            bool hit = false;
            float flight = pounceFlight / PhaseSpeed;
            for (float t = 0f; t < flight; t += Dt)
            {
                // Bị choáng giữa cú vồ: đứng hình giữa không trung (như mọi đòn khác dừng lại khi choáng), hết choáng bay tiếp
                if (_stun > 0f)
                {
                    while (_stun > 0f && !IsDead)
                        yield return null;
                    if (IsDead)
                        yield break;
                    _visual?.Act(LionPose.Leap, flight - t); // dáng bay hết hạn trong lúc choáng thì diễn lại quãng còn lại
                }

                float k = t / flight;
                SetPosition(Mathf.Lerp(startX, landX, k), floorY + 4f * lift * k * (1f - k));
                if (!hit && PlayerIn(AttackBody))
                    hit = HitPlayer(pounceDamage, (Vector2)AttackBody.center, pounceKnockback, true);
                yield return null;
            }

            SetPosition(landX, floorY);
            _airborne = false;
            _hazards.HideMarker(_marker);
            _marker = null;
            Act(LionPose.Land, landTime);
            Fx?.GroundSlam(spot, (_front + _back) * 0.3f, fireColor, rockColor, 10, 1.4f);
            CameraShake.Impulse(Vector2.down, 1.6f);
            if (!hit && PlayerIn(AttackBody))
                HitPlayer(pounceDamage, spot, pounceKnockback, true);
            _hazards.GroundWave(spot, (_front + _back) * 0.3f, pounceWaveRange, pounceWaveSpeed * PhaseSpeed, pounceWaveDamage, new Vector2(6f, 7f));
            yield return Wait(landTime + 0.05f);
        }

        // Gầm: hít vào cúi đầu, ngẩng lên, bờm bùng lửa; há miệng (roarShockAt của dáng gầm) thì vòng sóng âm lan rộng; trúng
        // thì mất máu và bị đẩy ra xa
        IEnumerator RoarRoutine(bool damage)
        {
            float duration = Mathf.Clamp(roarWindup / roarShockAt, roarTime.x, roarTime.y);
            float windup = duration * roarShockAt;
            Act(LionPose.Roar, duration); // bờm bùng lửa do hình tự diễn; quầng đỏ sau lưng rực lên báo trước
            _glow = 1f;
            yield return Wait(windup);

            Vector2 mouth = Mouth;
            CameraShake.Impulse(Vector2.up, 1.4f);
            for (int i = 0; i < 3; i++)
                Fx?.Ring(mouth, roarRadius * (1f - i * 0.18f), roarColor, 0.45f + i * 0.12f);
            Fx?.Flash(mouth, 1.8f, Color.white, 0.2f);
            if (damage)
                _highTime = 0f;

            bool hit = !damage;
            float elapsed = 0f;
            for (float front = 0f; front < roarRadius; front += roarSpeed * PhaseSpeed * Dt)
            {
                if (!hit && !IsFrozen && target != null && !target.IsDead && Vector2.Distance(target.Center, mouth) <= front)
                {
                    Vector2 away = (target.Center - mouth).normalized;
                    hit = true;
                    target.Hit(roarDamage, mouth, new Vector2(roarPush.x * Mathf.Max(0.35f, Mathf.Abs(away.x)), roarPush.y));
                }

                elapsed += Dt * PhaseSpeed;
                yield return null;
            }

            // Gầm hết dáng (ngậm miệng, về) rồi mới đi tiếp
            yield return Wait(Mathf.Max(0.1f, duration * 0.85f - windup - elapsed));
        }

        // Cầu lửa: lắc bờm, đốm lửa tụ lại, rồi 5 quả cầu lửa bay vòng cung về phía người chơi (trong lúc bờm lắc mạnh nhất);
        // rơi xuống sàn thì cháy
        IEnumerator FireballRoutine()
        {
            int count = Mathf.Max(1, fireballCount);
            float last = fireballWindup + fireballInterval * (count - 1);
            // Quả cuối còn trong khoảng bờm lắc mạnh nhất; quả đầu không sớm hơn lúc bắt đầu lắc mạnh
            float duration = Mathf.Max(last / shakeWindow.y + 0.05f, 0.6f);
            duration = Mathf.Min(duration, fireballWindup / Mathf.Max(0.05f, shakeWindow.x));
            Act(LionPose.ManeShake, duration);
            _hazards.Gather(() => Mane, height * 0.45f, fireballWindup / PhaseSpeed, 10);
            yield return Wait(fireballWindup);
            if (target == null)
                yield break;

            Vector2 p = target.Center;
            for (int i = 0; i < count; i++)
            {
                Vector2 from = Mane;
                float offset = (i - (count - 1) * 0.5f) * fireballSpread;
                var aim = new Vector2(Mathf.Clamp(p.x + offset, arenaX.x + 1f, arenaX.y - 1f), Mathf.Min(p.y, floorY + fireballMaxHeight));
                float time = (fireballFlight + 0.07f * Mathf.Abs(i - (count - 1) * 0.5f)) / PhaseSpeed;
                float gravity = fireballGravity * PhaseSpeed * PhaseSpeed;
                Vector2 velocity = LionHazards.Launch(from, aim, time, gravity);
                _hazards.Fireball(from, velocity, gravity, fireballDamage, new Vector2(5f, 6f), burnWidth, burnTime, burnDps);
                Fx?.Flash(from, 1.4f, fireColor, 0.15f);
                if (i < count - 1)
                    yield return Wait(fireballInterval);
            }

            yield return Wait(Mathf.Max(0.2f, duration - last));
        }

        // Húc lao: cào chân 2 lần, cúi đầu, bụi bay; rồi lao thẳng tới vách (dáng Charge suốt quãng lao, chân phi theo tốc độ).
        // Trúng thì hất tung; đâm vách thì nằm choáng rồi đứng dậy
        IEnumerator ChargeRoutine()
        {
            Act(LionPose.ChargeWindup, chargeWindup);
            float waited = 0f;
            for (int i = 0; i < 2; i++)
            {
                float at = chargeWindup * (i == 0 ? scrapeAt.x : scrapeAt.y);
                yield return Wait(at - waited);
                waited = at;
                var paw = new Vector2(Paw.x, floorY); // chân trước cào đất ra sau
                Fx?.DustCloud(paw, 2.2f, dustColor, 6);
                Fx?.Debris(paw, rockColor, 4, 5f, 0.3f, 18f, new Vector2(-_facing, 0.6f));
                Fx?.Burst(paw + Vector2.up * 0.2f, fireColor, 4, 4f, 0.14f, 0.35f, 8f, new Vector2(-_facing, 0.5f));
                CameraShake.Impulse(Vector2.down, 0.25f);
            }

            yield return Wait(chargeWindup - waited);

            float dir = _facing;
            float wall = dir > 0f ? ClampX(arenaX.y) : ClampX(arenaX.x);
            float speed = chargeSpeed * PhaseSpeed;
            _visual?.Act(LionPose.Charge, Mathf.Abs(wall - X) / speed + 0.15f);
            bool hit = false;
            float dust = 0f;
            while ((wall - X) * dir > 0.01f)
            {
                if (IsDead)
                    yield break;
                if (_stun > 0f)
                {
                    // Bị choáng giữa đường lao: đứng yên, không húc trúng ai; hết choáng thì lao tiếp quãng còn lại
                    while (_stun > 0f && !IsDead)
                        yield return null;
                    if (IsDead)
                        yield break;
                    _visual?.Act(LionPose.Charge, Mathf.Abs(wall - X) / speed + 0.15f);
                    continue;
                }

                SetX(Mathf.Abs(wall - X) < speed * Dt ? wall : X + dir * speed * Dt);
                dust += Dt;
                if (dust > 0.05f)
                {
                    dust = 0f;
                    Bounds body = Body;
                    for (int k = 0; k < 3; k++)
                        Fx?.SpeedLines(new Vector2(body.center.x, body.min.y + body.size.y * (0.25f + 0.25f * k)), new Vector2(dir * chargeSpeed, 0f), fireColor);
                    Fx?.DustCloud(new Vector2(X - dir * _back * 0.8f, floorY), 1.6f, dustColor, 3);
                    Fx?.Flames(new Vector2(X - dir * _back * 0.6f, floorY), 1.5f, fireColor, 0.25f);
                }

                if (!hit && PlayerIn(AttackBody))
                    hit = HitPlayer(chargeDamage, new Vector2(X - dir * 3f, floorY), chargeLaunch, true);
                yield return null;
            }

            // Đâm vách: rung mạnh, đá vụn rơi, nằm choáng rồi đứng dậy
            var impact = new Vector2(X + dir * _front, floorY + height * 0.4f);
            Fx?.GroundSlam(new Vector2(impact.x, floorY), 2.5f, fireColor, rockColor, 10, 1.4f);
            Fx?.Flash(impact, 4f, fireColor, 0.3f);
            Fx?.Debris(impact + Vector2.up * height * 0.4f, rockColor, 10, 3f, 0.35f);
            CameraShake.Impulse(new Vector2(dir, 0f), 2f);
            _visual?.Act(LionPose.Stagger, wallStun, true);
            _wallStun = wallStun;
            while (_wallStun > 0f && !IsDead)
                yield return null;
            _visual?.Act(LionPose.None, 0f);
            yield return Wait(getUpTime);
        }

        // Mưa lửa: gầm lên trời, vòng đỏ đánh dấu 8 chỗ (trên đá và sàn, có chỗ ngay dưới người chơi), rồi thiên thạch rơi
        IEnumerator FireRainRoutine()
        {
            _fireRainTimer = fireRainInterval;
            float duration = roarTime.x + 0.3f;
            Act(LionPose.Roar, duration);
            _glow = 1f;
            yield return Wait(duration * roarShockAt);
            Vector2 mouth = Mouth;
            Fx?.Flash(mouth, 3f, fireColor, 0.5f);
            Fx?.Ring(mouth, 6f, roarColor, 0.5f);
            CameraShake.Impulse(Vector2.up, 0.8f);

            var spots = new List<float>();
            float px = target != null ? target.Center.x : X;
            spots.Add(px);
            spots.Add(px - 3.4f);
            spots.Add(px + 3.4f);
            for (int tries = 0; spots.Count < fireRainCount && tries < 200; tries++)
            {
                float x = Random.Range(arenaX.x + 1.5f, arenaX.y - 1.5f);
                bool clear = true;
                foreach (float s in spots)
                    clear &= Mathf.Abs(s - x) > 2.8f;
                if (clear)
                    spots.Add(x);
            }

            for (int i = 0; i < spots.Count; i++)
            {
                float x = Mathf.Clamp(spots[i], arenaX.x + 1f, arenaX.y - 1f);
                _hazards.Meteor(SurfaceBelow(x), (fireRainWarn + i * 0.06f) / PhaseSpeed, meteorFall, meteorDamage, meteorRadius, new Vector2(6f, 8f));
            }

            yield return Wait(fireRainWarn + 0.6f);
        }

        // Mặt trên cao nhất (đá hoặc sàn) dưới trần ở x
        Vector2 SurfaceBelow(float x)
        {
            var from = new Vector2(x, ceilingY - 0.3f);
            RaycastHit2D hit = surfaceMask.value != 0 ? Physics2D.Raycast(from, Vector2.down, ceilingY - floorY + 2f, surfaceMask) : default;
            return hit.collider != null ? hit.point : new Vector2(x, floorY);
        }

        // =============================================================================================================
        // Trúng đòn, khống chế, choáng gục, chết
        // =============================================================================================================

        void OnDamaged(float amount, Vector2 from, HitEffect effect)
        {
            if (IsDead)
                return;

            Engage(); // bị đánh trước khi gầm mở màn thì trận đấu cũng bắt đầu luôn
            _visual?.Flash(effect.heavy ? 1f : Mathf.Lerp(0.25f, 0.8f, amount / 40f));
            if (effect.heavy && Current == LionAttack.None && !IsStaggered && !_airborne && amount >= 20f && !IsFrozen)
                _visual?.Act(LionPose.Hurt, hurtTime);

            // Da cứng: không bị đẩy / kéo (bỏ knockback); choáng 30%; trói, kén tối đa 0.6 s; độc ăn đủ
            float stun = effect.stun * stunScale;
            if (effect.cocoon)
                stun = Mathf.Max(stun, maxHold);
            stun = effect.cocoon ? Mathf.Min(stun, maxHold) : stun;
            if (stun > _stun)
                _stun = stun;
            _hold = Mathf.Max(_hold, Mathf.Min(effect.snare, maxHold));
            if (effect.poisonTime > 0f)
            {
                _poisonDps = Mathf.Max(effect.poisonDps, _poisonTime > 0f ? _poisonDps : 0f);
                _poisonTime = Mathf.Max(_poisonTime, effect.poisonTime);
            }

            // Choáng gục: dồn đủ sát thương trong cửa sổ thời gian
            if (!IsStaggered && amount > 0f)
            {
                float now = Time.time;
                _recent.Enqueue((now, amount));
                _recentSum += amount;
                while (_recent.Count > 0 && _recent.Peek().time < now - staggerWindow)
                    _recentSum -= _recent.Dequeue().amount;
                if (_recentSum >= staggerDamage && health.IsAlive)
                {
                    _recent.Clear();
                    _recentSum = 0f;
                    if (_airborne)
                        _staggerPending = true;
                    else
                        BeginStagger();
                }
            }

            UpdatePhase();
        }

        void UpdatePhase()
        {
            float f = HealthFraction;
            int phase = f <= phase3At ? 3 : f <= phase2At ? 2 : 1;
            if (phase <= Phase || !health.IsAlive)
                return;
            Phase = phase;
            _phaseRoarPending = true;
            if (phase >= 3)
                _fireRainTimer = fireRainFirst;
            PhaseChanged?.Invoke(phase);
        }

        void BeginStagger()
        {
            _staggerPending = false;
            Interrupt();
            IsStaggered = true;
            health.DamageMultiplier = staggerBonus;
            _visual?.Act(LionPose.Stagger, staggerTime, true);
            Fx?.DustCloud(new Vector2(X + _facing * _centerOffset, floorY), _front, dustColor, 10);
            Fx?.Debris(new Vector2(X, floorY), rockColor, 8, 4f, 0.3f);
            CameraShake.Impulse(Vector2.down, 1.2f);
            Staggered?.Invoke();
            _brain = StartCoroutine(StaggerRoutine());
        }

        IEnumerator StaggerRoutine()
        {
            for (float t = 0f; t < staggerTime; t += Dt)
                yield return null;
            IsStaggered = false;
            health.DamageMultiplier = 1f;
            _visual?.Act(LionPose.None, 0f); // thôi nằm: hình tự đứng dậy
            for (float t = 0f; t < getUpTime; t += Dt)
                yield return null;
            _brain = null;
            if (_aiEnabled && !IsDead)
                _brain = StartCoroutine(Brain(LionAttack.None));
        }

        // Dừng đòn đang ra (choáng gục, chết, test ép đòn): dọn vòng báo trước, đáp xuống sàn
        void Interrupt()
        {
            if (_brain != null)
                StopCoroutine(_brain);
            _brain = null;
            if (_marker != null)
                _hazards.HideMarker(_marker);
            _marker = null;
            _wallStun = 0f;
            if (_airborne)
                SetPosition(X, floorY);
            _airborne = false;
            Current = LionAttack.None;
            _visual?.Act(LionPose.None, 0f); // bỏ dáng đang diễn / đang giữ (chồm, nằm gục)
            if (IsStaggered)
            {
                IsStaggered = false;
                health.DamageMultiplier = 1f;
            }
        }

        void OnDied()
        {
            if (IsDead)
                return;
            bool wasAirborne = _airborne;
            float y = transform.position.y;
            Interrupt();
            if (wasAirborne)
                SetPosition(X, y);
            IsDead = true;
            IsStaggered = false;
            _stun = _hold = _poisonTime = 0f;
            _fightEnd = Time.time;
            _hazards.StopAll();
            if (hurtbox != null)
                hurtbox.enabled = false;
            Died?.Invoke();
            StartCoroutine(DieRoutine(wasAirborne));
        }

        IEnumerator DieRoutine(bool falling)
        {
            if (falling)
            {
                float y = transform.position.y;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    SetPosition(X, Mathf.Lerp(y, floorY, t / 0.3f));
                    yield return null;
                }

                SetPosition(X, floorY);
            }

            _visual?.Act(LionPose.Die, 1.5f, true);
            Bounds b = Body;
            Fx?.Flash(b.center, height * 0.6f, fireColor, 0.5f);
            Fx?.Burst(b.center, fireColor, 30, 9f, 0.25f, 0.8f);
            Fx?.GroundSlam(new Vector2(b.center.x, floorY), _front * 0.5f, fireColor, rockColor, 12, 1.2f);
            CameraShake.Impulse(Vector2.down, 2f);
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                if (Fx != null && Random.value < 0.25f)
                    Fx.Burst((Vector2)b.center + Random.insideUnitCircle * height * 0.3f, new Color(0.35f, 0.25f, 0.25f, 0.6f), 1, 1.5f, 0.2f, 0.9f, -1.5f);
                yield return null;
            }

            Defeated?.Invoke();
        }

        // Người chơi gục: boss thôi đánh, gầm thắng
        void OnPlayerDied()
        {
            if (IsDead)
                return;
            Interrupt();
            _hazards.StopAll();
            _visual?.Act(LionPose.Roar, 1.4f);
        }

        // =============================================================================================================
        // Mỗi frame
        // =============================================================================================================

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsDead)
            {
                SetVisualFrozen(false);
                _hazards.Frozen = false;
                _enrage = Mathf.MoveTowards(_enrage, 0f, dt);
                _visual?.SetEnrage(_enrage);
                UpdateBackLight(dt);
                UpdateStunStars(dt);
                return;
            }

            // Đóng băng: hình đứng im (tắt hoạt ảnh), đạn / lửa của boss dừng, mọi đồng hồ (choáng, độc, mưa lửa) dừng
            bool frozen = IsFrozen;
            SetVisualFrozen(frozen);
            _hazards.Frozen = frozen;
            if (frozen)
            {
                UpdateBackLight(dt);
                return;
            }

            _stun = Mathf.Max(0f, _stun - dt);
            _hold = Mathf.Max(0f, _hold - dt);
            _wallStun = Mathf.Max(0f, _wallStun - dt);

            if (_poisonTime > 0f)
            {
                _poisonTime -= dt;
                _poisonTick -= dt;
                if (_poisonTick <= 0f)
                {
                    _poisonTick = 0.5f;
                    float dealt = health.TakeHit(_poisonDps * 0.5f, transform.position, default);
                    DamageNumbers.Show((Vector2)health.transform.position + Vector2.up * 1.6f, dealt, poisonColor, false, this);
                    Fx?.Burst((Vector2)health.transform.position + Vector2.up * 0.9f, poisonColor, 4, 1.2f, 0.12f, 0.6f, -1.5f);
                    if (IsDead)
                        return;
                }
            }

            if (target != null && !target.IsDead && Engaged)
            {
                float up = target.Center.y - floorY;
                _highTime = up > highAbove ? _highTime + dt : up < highAbove - 1.5f ? 0f : _highTime;
            }

            if (Phase >= 3 && Engaged)
                _fireRainTimer -= dt * Tempo;

            _enrage = Mathf.MoveTowards(_enrage, Phase >= 3 ? 1f : 0f, dt * 0.8f);
            _visual?.SetEnrage(_enrage);
            UpdateBackLight(dt);
            UpdateStunStars(dt);
        }

        void LateUpdate()
        {
            if (_staggerPending && !_airborne && !IsDead)
                BeginStagger();

            float dt = Time.deltaTime;
            float vx = dt > 0f ? (X - _lastX) / dt : 0f;
            _lastX = X;
            _visual?.SetLocomotion(vx, !_airborne);
            UpdateHurtbox();
        }

        // Gốc của Health = chỗ trên thân gần người chơi nhất (hạ 0.9 vì SkillContext.Aim cộng lên 0.9); collider phủ cả thân
        void UpdateHurtbox()
        {
            if (hurtbox == null)
                return;
            Bounds b = Body;
            Vector2 point = b.center;
            if (target != null)
            {
                Vector2 p = target.Center;
                float insetX = Mathf.Min(0.6f, b.extents.x);
                float insetY = Mathf.Min(0.5f, b.extents.y);
                point.x = Mathf.Clamp(p.x, b.min.x + insetX, b.max.x - insetX);
                point.y = Mathf.Clamp(p.y, b.min.y + insetY, b.max.y - insetY);
            }

            Transform t = hurtbox.transform;
            t.position = new Vector3(point.x, point.y - 0.9f, 0f);
            hurtbox.offset = (Vector2)t.InverseTransformPoint(b.center);
            hurtbox.size = b.size;
        }

        void UpdateBackLight(float dt)
        {
            if (backLight == null)
                return;
            Bounds b = Body;
            backLight.transform.position = new Vector3(b.center.x, floorY + height * 0.45f, 0f);
            float alive = IsDead ? Mathf.Clamp01(1f - (Time.time - _fightEnd) / 2f) : 1f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * (2f + 3f * _enrage));
            _glow = Mathf.MoveTowards(_glow, 0f, dt * 1.2f);
            Color c = backLight.color;
            c.a = Mathf.Min(1f, (0.32f + 0.28f * _enrage) * pulse + 0.45f * _glow) * alive;
            backLight.color = c;
        }

        void UpdateStunStars(float dt)
        {
            if (stunStars == null)
                return;
            bool on = !IsDead && (IsStunned || IsStaggered);
            _starsAlpha = Mathf.MoveTowards(_starsAlpha, on ? 1f : 0f, dt * 5f);
            bool show = _starsAlpha > 0f;
            if (stunStars.gameObject.activeSelf != show)
                stunStars.gameObject.SetActive(show);
            if (!show)
                return;

            Bounds b = Body;
            Vector2 head = _visual != null ? _visual.MouthPoint : (Vector2)b.center;
            stunStars.position = new Vector3(head.x - _facing * 0.4f, (IsStaggered ? b.center.y : b.max.y) + 0.4f, 0f);
            float time = Time.time;
            int n = stunStars.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform star = stunStars.GetChild(i);
                float a = (time * 260f + i * 360f / n) * Mathf.Deg2Rad;
                float front = -Mathf.Sin(a) * 0.5f + 0.5f;
                star.localPosition = new Vector3(Mathf.Cos(a) * height * 0.22f, Mathf.Sin(a) * height * 0.06f, 0f);
                star.localScale = Vector3.one * Mathf.Lerp(1.4f, 2.3f, front) * _starsAlpha;
                star.localRotation = Quaternion.Euler(0f, 0f, time * 200f + i * 50f);
            }
        }

        // =============================================================================================================
        // Tiện ích
        // =============================================================================================================

        static CombatFx Fx => CombatFx.Instance;

        Vector2 Mouth => _visual != null ? _visual.MouthPoint : (Vector2)transform.position + new Vector2(_facing * height * 0.55f, height * 0.75f);

        Vector2 Paw => _visual != null ? _visual.PawPoint : (Vector2)transform.position + new Vector2(_facing * height * 0.6f, height * 0.15f);

        // Chỗ bờm (phóng cầu lửa, lửa bùng khi gầm): bờm phủ từ đầu tới vai, giữa bờm cao ~0.78 chiều cao boss
        Vector2 Mane => new Vector2(X + _facing * Mathf.Max(_centerOffset, _front * 0.4f), transform.position.y + height * 0.78f);

        // Mép xa nhất (đầu) không lọt vào vách dù đang quay về phía nào
        float ClampX(float x) => Mathf.Clamp(x, arenaX.x + _front + 0.2f, arenaX.y - _front - 0.2f);

        void SetX(float x) => SetPosition(x, transform.position.y);

        void SetPosition(float x, float y) => transform.position = new Vector3(ClampX(x), y, transform.position.z);

        bool PlayerIn(Bounds bounds) => PlayerIn(Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y));

        // Thân người chơi (tròn bán kính 0.5) chạm vùng. Đang đóng băng thì không đánh trúng ai
        bool PlayerIn(Rect zone)
        {
            if (target == null || target.IsDead || IsFrozen)
                return false;
            Vector2 p = target.Center;
            var closest = new Vector2(Mathf.Clamp(p.x, zone.xMin, zone.xMax), Mathf.Clamp(p.y, zone.yMin, zone.yMax));
            return (closest - p).sqrMagnitude < 0.45f * 0.45f;
        }

        // Đang đóng băng hay đang choáng thì móng / thân boss không đánh trúng ai (đòn đang dở dừng lại tới hết choáng)
        bool HitPlayer(float damage, Vector2 from, Vector2 knockback, bool heavy) =>
            target != null && !IsFrozen && !IsStunned && target.Hit(damage, from, knockback, heavy);

        bool _visualFrozen;

        // Hình đứng hình: tắt hoạt ảnh (giữ nguyên dáng đang diễn), hết đóng băng thì bật lại diễn tiếp
        void SetVisualFrozen(bool frozen)
        {
            if (_visualFrozen == frozen)
                return;
            _visualFrozen = frozen;
            if (_visual is Behaviour b && b != null)
                b.enabled = !frozen;
        }
    }
}
