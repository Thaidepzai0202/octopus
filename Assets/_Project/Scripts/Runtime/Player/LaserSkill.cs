using System;
using UnityEngine;

namespace Octopus
{
    public enum LaserPhase
    {
        Ready,
        Charging,
        Firing,
        Cooldown,
    }

    /// <summary>
    /// Chiêu bắn laze từ mắt: vận sức → bắn → hồi chiêu (đếm từ lúc tia tắt).
    /// Tia bắn ngang theo hướng bạch tuộc đang quay mặt lúc bấm chiêu, dừng ở sàn / vách / đá đầu tiên trên đường bay.
    /// Các chiêu laze (CombatController) gọi TryCast(LaserShot) với thời gian, độ to, góc quét riêng; khi đó không hồi chiêu ở
    /// đây (bộ chiêu tự tính hồi chiêu). Phần hình (tia, hạt sáng, mắt sáng) do LaserBeamView
    /// và OctopusRigAnimator đọc trạng thái ở đây mà vẽ.
    /// </summary>
    [RequireComponent(typeof(OctopusController))]
    [DefaultExecutionOrder(50)] // sau OctopusRigAnimator: lấy đúng vị trí mắt của frame này
    public class LaserSkill : MonoBehaviour
    {
        /// <summary>Thông số một lần bắn.</summary>
        [Serializable]
        public struct LaserShot
        {
            [Tooltip("Vận sức (giây)")] public float charge;
            [Tooltip("Tia kéo dài (giây)")] public float fire;
            [Tooltip("Tia to gấp mấy lần bình thường")] public float width;
            [Tooltip("Góc tia lúc bắt đầu bắn (độ, 0 = thẳng trước mặt, dương = chếch lên)")] public float sweepFrom;
            [Tooltip("Góc tia lúc tắt (độ); khác Sweep From thì tia quét dần")] public float sweepTo;
            [Tooltip("Tia dài gấp mấy lần ô Range (0 = 1): tầm chiêu của nhân vật")] public float rangeScale;
        }

        [Tooltip("Vận sức bao lâu trước khi bắn (giây)")]
        [SerializeField, Min(0f)] float chargeTime = 1f;
        [Tooltip("Tia laze kéo dài bao lâu (giây)")]
        [SerializeField, Min(0.1f)] float fireTime = 2f;
        [Tooltip("Hồi chiêu bao lâu, tính từ lúc tia tắt (giây)")]
        [SerializeField, Min(0f)] float cooldown = 5f;
        [Tooltip("Tia dài tối đa (unit)")]
        [SerializeField, Min(1f)] float range = 16f;
        [Tooltip("Tia dừng lại khi chạm các lớp này (sàn, vách, đá)")]
        [SerializeField] LayerMask blockMask = 1;

        OctopusController _controller;
        ILaserEye _eye;
        float _elapsed;
        float _charge, _fire, _cooldown, _sweepFrom, _sweepTo, _rangeScale = 1f;
        float _facing = 1f;

        public LaserPhase Phase { get; private set; }

        /// <summary>Số giây đã trôi trong giai đoạn hiện tại.</summary>
        public float Elapsed => _elapsed;

        /// <summary>Thời gian vận sức / bắn / hồi chiêu của lần bắn hiện tại.</summary>
        public float ChargeTime => _charge;
        public float FireTime => _fire;
        public float Cooldown => _cooldown;

        /// <summary>Tia lần này to gấp mấy lần bình thường.</summary>
        public float WidthScale { get; private set; } = 1f;

        /// <summary>Độ dài giai đoạn hiện tại (giây); Ready = 0.</summary>
        public float PhaseDuration => Phase switch
        {
            LaserPhase.Charging => _charge,
            LaserPhase.Firing => _fire,
            LaserPhase.Cooldown => _cooldown,
            _ => 0f,
        };

        /// <summary>0..1: đã đi được bao nhiêu phần giai đoạn hiện tại.</summary>
        public float PhaseProgress => PhaseDuration > 0f ? Mathf.Clamp01(_elapsed / PhaseDuration) : 1f;

        public float CooldownRemaining => Phase == LaserPhase.Cooldown ? Mathf.Max(_cooldown - _elapsed, 0f) : 0f;
        public bool IsReady => Phase == LaserPhase.Ready;

        /// <summary>Hướng tia (world), chốt lúc bấm chiêu.</summary>
        public Vector2 Direction { get; private set; } = Vector2.right;

        /// <summary>Chỗ tia bắt đầu: mắt của nhân vật đang hiện (bạch tuộc / nhện), không có thì tâm người chơi.</summary>
        public Vector2 Origin
        {
            get
            {
                if (!(_eye is Behaviour { isActiveAndEnabled: true }))
                    _eye = GetComponentInChildren<ILaserEye>();
                return _eye != null ? _eye.EyePosition : (Vector2)transform.position;
            }
        }

        /// <summary>Đầu kia của tia (chỗ chạm vật cản, hoặc hết tầm bắn). Chỉ đúng khi đang bắn.</summary>
        public Vector2 BeamEnd { get; private set; }

        /// <summary>Tia đang chạm vật cản (có tia lửa ở đầu tia).</summary>
        public bool BeamHit { get; private set; }

        public Vector2 HitNormal { get; private set; }

        /// <summary>Đổi giai đoạn (vừa bấm, bắt đầu bắn, tắt tia, hồi xong).</summary>
        public event Action<LaserPhase> PhaseChanged;

        void Awake()
        {
            _controller = GetComponent<OctopusController>();
            _charge = chargeTime;
            _fire = fireTime;
            _cooldown = cooldown;
        }

        /// <summary>Bắn theo thông số mặc định (ô Charge Time / Fire Time / Cooldown). False nếu đang bận / hồi chiêu.</summary>
        public bool TryCast() =>
            TryCast(new LaserShot { charge = chargeTime, fire = fireTime, width = 1f }, cooldown);

        /// <summary>Bắn theo thông số riêng của một chiêu, không hồi chiêu (bộ chiêu tự tính).</summary>
        public bool TryCast(LaserShot shot) => TryCast(shot, 0f);

        bool TryCast(LaserShot shot, float cooldownAfter)
        {
            if (Phase != LaserPhase.Ready || !isActiveAndEnabled)
                return false;

            _charge = Mathf.Max(0f, shot.charge);
            _fire = Mathf.Max(0.1f, shot.fire);
            _cooldown = cooldownAfter;
            _sweepFrom = shot.sweepFrom;
            _sweepTo = shot.sweepTo;
            WidthScale = shot.width > 0f ? shot.width : 1f;
            _rangeScale = shot.rangeScale > 0f ? shot.rangeScale : 1f;
            _facing = _controller != null && _controller.Facing < 0f ? -1f : 1f;
            Direction = Aim(_sweepFrom);
            SetPhase(_charge > 0f ? LaserPhase.Charging : LaserPhase.Firing);
            return true;
        }

        /// <summary>Huỷ lần bắn đang dở (vận sức / đang bắn / hồi chiêu): tắt tia, về Ready. Người chơi gục thì gọi.</summary>
        public void Cancel()
        {
            if (Phase == LaserPhase.Ready)
                return;
            _elapsed = 0f;
            SetPhase(LaserPhase.Ready);
        }

        // Hướng tia chếch lên angle độ so với phía trước
        Vector2 Aim(float angle)
        {
            float r = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r) * _facing, Mathf.Sin(r));
        }

        void Update()
        {
            if (Phase == LaserPhase.Ready)
                return;

            _elapsed += Time.deltaTime;
            // Phần thời gian dư chuyển sang giai đoạn sau, nên tổng thời gian luôn đúng dù frame dài
            while (Phase != LaserPhase.Ready && _elapsed >= PhaseDuration)
            {
                float extra = _elapsed - PhaseDuration;
                SetPhase(Phase switch
                {
                    LaserPhase.Charging => LaserPhase.Firing,
                    LaserPhase.Firing => _cooldown > 0f ? LaserPhase.Cooldown : LaserPhase.Ready,
                    _ => LaserPhase.Ready,
                });
                _elapsed = Phase == LaserPhase.Ready ? 0f : extra;
            }

            if (Phase == LaserPhase.Firing && !Mathf.Approximately(_sweepFrom, _sweepTo))
                Direction = Aim(Mathf.Lerp(_sweepFrom, _sweepTo, Mathf.SmoothStep(0f, 1f, PhaseProgress)));
        }

        void LateUpdate()
        {
            if (Phase == LaserPhase.Firing)
                UpdateBeam();
        }

        /// <summary>Tính lại đường tia từ mắt theo hướng đã chốt.</summary>
        public void UpdateBeam()
        {
            Vector2 origin = Origin;
            float length = range * _rangeScale;
            RaycastHit2D hit = Physics2D.Raycast(origin, Direction, length, blockMask);
            BeamHit = hit.collider != null;
            BeamEnd = BeamHit ? hit.point : origin + Direction * length;
            HitNormal = BeamHit ? hit.normal : -Direction;
        }

        void SetPhase(LaserPhase phase)
        {
            Phase = phase;
            _elapsed = 0f;
            if (phase == LaserPhase.Firing)
                UpdateBeam();
            PhaseChanged?.Invoke(phase);
        }
    }
}
