using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Khựng hình khi đánh trúng (hit-stop): hạ Time.timeScale trong vài chục mili giây rồi trả lại, đếm bằng thời gian thật.
    /// Đòn thường khựng rất ngắn, đòn nặng lâu hơn, đòn kết liễu của chiêu cuối lâu nhất (theo mốc nộ).
    ///  - Cả loạt đòn trúng trong cùng một khung hình (chiêu đánh lan trúng 3 mục tiêu) chỉ khựng một lần.
    ///  - Khựng liên tiếp sát nhau thì ngắn dần (6 xúc tu kraken không làm game giật cục), không bao giờ quá mức trần.
    ///  - Sát thương liên tục (laze, độc, vùng mực, xoáy nước) không khựng: Skill chỉ gọi Hit cho đòn có toé tia lửa.
    /// Nằm trên object "Game Feel" của màn chơi (GameFeelBuilder dựng); không có thì chiêu vẫn chạy, chỉ không khựng.
    /// Thời gian game (Time.time, WaitForSeconds…) chỉ dừng lại chứ không bị lệch, nên combo và hồi chiêu vẫn đúng nhịp.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public class HitStop : MonoBehaviour
    {
        [Tooltip("Bật / tắt khựng hình khi đánh trúng")]
        [SerializeField] bool stopOnHit = true;
        [Tooltip("Đòn thường: khựng bấy nhiêu giây (thời gian thật)")]
        [SerializeField, Range(0f, 0.1f)] float lightStop = 0.035f;
        [Tooltip("Đòn nặng (đòn cuối combo, vồ mồi, phi tơ…): khựng bấy nhiêu giây")]
        [SerializeField, Range(0f, 0.15f)] float heavyStop = 0.08f;
        [Tooltip("Đòn nặng của chiêu cuối ở mốc nộ cao nhất: khựng bấy nhiêu giây (mốc thấp hơn thì ngắn hơn, tới gần mức đòn nặng)")]
        [SerializeField, Range(0f, 0.2f)] float ultimateStop = 0.12f;
        [Tooltip("Hệ số nộ (RageMeter) ứng với mức khựng dài nhất của chiêu cuối")]
        [SerializeField, Min(1.01f)] float fullPower = 2.6f;
        [Tooltip("Trần: một lần khựng không dài hơn bấy nhiêu giây dù nhiều đòn dồn lại")]
        [SerializeField, Range(0f, 0.25f)] float cap = 0.12f;
        [Tooltip("Tốc độ thời gian trong lúc khựng: 0 = đứng hình hẳn, 0.05 = gần như đứng")]
        [SerializeField, Range(0f, 0.5f)] float frozenScale = 0f;
        [Tooltip("Lần khựng mới đến trong bấy nhiêu giây (thời gian thật) sau lần trước thì bị rút ngắn")]
        [SerializeField, Min(0f)] float chainWindow = 0.3f;
        [Tooltip("Mỗi lần khựng nối tiếp trong khoảng trên chỉ dài bằng tỉ lệ này của lần trước (đòn nặng hơn thì tính lại từ đầu)")]
        [SerializeField, Range(0f, 1f)] float chainFalloff = 0.55f;
        [Tooltip("Ngắn hơn bấy nhiêu giây thì bỏ qua, không khựng")]
        [SerializeField, Min(0f)] float minimum = 0.015f;
        [Tooltip("Tổng sát thương cả loạt dưới mức này thì không khựng (đòn rỉa máu nhỏ)")]
        [SerializeField, Min(0f)] float minDamage = 3f;

        static HitStop s_instance;
        static bool s_frozen;
        static float s_restoreScale = 1f;

        float _remaining, _elapsed, _applied;
        float _lastEnd = -10f, _lastSeconds, _factor = 1f;
        int _batchFrame = -1;
        float _batchFactor = 1f;

        /// <summary>Đang khựng hình.</summary>
        public static bool IsFrozen => s_frozen;

        // Domain reload tắt: static còn giữ từ lần Play trước. Lỡ lần trước thoát giữa lúc khựng thì trả lại tốc độ thời gian.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (s_frozen)
                Time.timeScale = s_restoreScale;
            s_instance = null;
            s_frozen = false;
            s_restoreScale = 1f;
        }

        /// <summary>
        /// Một loạt đòn vừa trúng (gọi một lần cho cả loạt). heavy: đòn nặng; ultimate: chiêu cuối, power = hệ số nộ
        /// (1 / 1.7 / 2.6 ở mốc 1 / 2 / 3); damage: tổng sát thương cả loạt.
        /// </summary>
        public static void Hit(bool heavy, bool ultimate, float power, float damage)
        {
            if (s_instance != null)
                s_instance.OnHit(heavy, ultimate, power, damage);
        }

        /// <summary>Khựng hình seconds giây (thời gian thật). Cùng khung hình thì lấy lần dài nhất, không cộng dồn.</summary>
        public static void Request(float seconds)
        {
            if (s_instance != null)
                s_instance.Freeze(seconds);
        }

        void OnEnable() => s_instance = this;

        void OnDisable()
        {
            Release(); // tắt component / đổi màn giữa lúc khựng: luôn trả lại tốc độ thời gian
            if (s_instance == this)
                s_instance = null;
        }

        void OnHit(bool heavy, bool ultimate, float power, float damage)
        {
            if (damage < minDamage)
                return;

            float seconds = heavy ? heavyStop : lightStop;
            if (ultimate)
            {
                float k = Mathf.InverseLerp(1f, fullPower, power);
                seconds = heavy ? Mathf.Lerp(heavyStop, ultimateStop, 0.25f + 0.75f * k) : seconds * Mathf.Lerp(1f, 1.5f, k);
            }

            Freeze(seconds);
        }

        void Freeze(float seconds)
        {
            if (!stopOnHit || !isActiveAndEnabled || seconds <= 0f)
                return;

            int frame = Time.frameCount;
            if (frame != _batchFrame)
            {
                // Lần khựng nối tiếp sát lần trước thì ngắn dần, trừ khi đòn này nặng hơn đòn trước
                bool chained = s_frozen || Time.unscaledTime - _lastEnd < chainWindow;
                _factor = !chained || seconds > _lastSeconds + 1e-4f ? 1f : _factor * chainFalloff;
                _lastSeconds = seconds;
                _batchFrame = frame;
                _batchFactor = _factor;
            }
            else
            {
                _lastSeconds = Mathf.Max(_lastSeconds, seconds);
            }

            float hold = Mathf.Min(seconds * _batchFactor, cap);
            if (hold < minimum)
                return;

            if (!s_frozen)
            {
                if (Time.timeScale <= 0f)
                    return; // game đang tạm dừng: không đụng tới
                s_restoreScale = Time.timeScale;
                s_frozen = true;
                _elapsed = 0f;
                _remaining = 0f;
                Time.timeScale = Mathf.Min(frozenScale, s_restoreScale);
                _applied = Time.timeScale;
            }

            // Kéo dài tới lần dài nhất (không cộng), tổng một lần khựng không quá trần
            _remaining = Mathf.Min(Mathf.Max(_remaining, hold), cap - _elapsed);
        }

        void Update()
        {
            if (!s_frozen)
                return;

            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _remaining -= dt;
            // Time.deltaTime của khung hình sau tính theo timeScale lúc đầu khung, nên nhả sớm nửa khung cho đúng thời lượng
            if (_remaining <= dt * 0.5f)
                Release();
        }

        void Release()
        {
            if (!s_frozen)
                return;

            s_frozen = false;
            if (Mathf.Approximately(Time.timeScale, _applied)) // không ai khác đổi timeScale trong lúc khựng
                Time.timeScale = s_restoreScale;
            _lastEnd = Time.unscaledTime;
        }
    }
}
