using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Số sát thương bay lên ở chỗ trúng đòn (canvas World Space): bật nảy (phóng to quá cỡ rồi co về), loé trắng rồi về màu,
    /// bay lên theo đường cong lệch sang một bên rồi mờ dần. Đòn nặng số to hơn, nghiêng ngẫu nhiên một chút. Đòn liên tục
    /// vào cùng một mục tiêu (laze, độc) thì cộng dồn vào một số đang hiện thay vì nhả ra cả chục số.
    /// Chạy theo thời gian thật nên vẫn bật nảy trong lúc khựng hình (HitStop).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class DamageNumbers : MonoBehaviour
    {
        const int PoolSize = 32;

        [SerializeField] Font font;
        [SerializeField] int fontSize = 64;

        [Header("Bật nảy")]
        [Tooltip("Số hiện bấy nhiêu giây (sau lần cộng dồn cuối) rồi tắt")]
        [SerializeField, Min(0.2f)] float life = 0.9f;
        [Tooltip("Cỡ lúc bật lên to nhất so với cỡ thường (vượt quá rồi co về)")]
        [SerializeField, Range(1f, 2.5f)] float popOvershoot = 1.5f;
        [Tooltip("Thời gian bật nảy rồi đứng cỡ (giây)")]
        [SerializeField, Min(0.05f)] float popTime = 0.28f;
        [Tooltip("Thời gian loé trắng lúc vừa hiện (giây) rồi chuyển về màu của đòn")]
        [SerializeField, Min(0f)] float whiteFlash = 0.08f;

        [Header("Đòn nặng")]
        [Tooltip("Đòn nặng: số to gấp mấy lần")]
        [SerializeField, Min(1f)] float heavyScale = 1.5f;
        [Tooltip("Đòn nặng: nghiêng ngẫu nhiên tối đa bấy nhiêu độ (đòn thường nghiêng một phần ba)")]
        [SerializeField, Range(0f, 30f)] float heavyTilt = 12f;

        [Header("Bay lên")]
        [Tooltip("Bay lên cao bấy nhiêu unit")]
        [SerializeField] float rise = 1.15f;
        [Tooltip("Trôi ngang bấy nhiêu unit (sang trái hoặc phải ngẫu nhiên), lên nhanh trước rồi mới trôi ngang: thành đường cong")]
        [SerializeField] float drift = 0.5f;
        [Tooltip("Đòn liên tục vào cùng mục tiêu, cùng màu trong bấy nhiêu giây thì cộng dồn vào một số")]
        [SerializeField, Min(0f)] float mergeWindow = 0.3f;

        class Number
        {
            public Text Text;
            public Outline Outline;
            public Object Key;
            public float Amount;
            public float Age;
            public float Since; // giây kể từ lần cộng dồn gần nhất
            public float Bump;  // nảy nhẹ khi cộng dồn thêm
            public Vector2 At;
            public float Side;  // hướng trôi ngang: -1..1
            public float Tilt;
            public float Size;
            public Color Color;
            public bool Big;
            public bool Active;
        }

        readonly List<Number> _numbers = new List<Number>(PoolSize);

        public static DamageNumbers Instance { get; private set; }

        // Domain reload tắt: static còn giữ từ lần Play trước
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Number", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<Text>();
                text.font = font;
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.01f, 0.12f, 0.9f);
                outline.effectDistance = new Vector2(3f, -3f);
                go.SetActive(false);
                _numbers.Add(new Number { Text = text, Outline = outline });
            }
        }

        /// <summary>
        /// Hiện số sát thương ở at (world). big: đòn nặng; key: mục tiêu, để cộng dồn đòn liên tục; size: phóng to thêm
        /// (chiêu cuối).
        /// </summary>
        public static void Show(Vector2 at, float amount, Color color, bool big = false, Object key = null, float size = 1f)
        {
            if (Instance != null && amount > 0.01f)
                Instance.Add(at, amount, color, big, key, size);
        }

        void Add(Vector2 at, float amount, Color color, bool big, Object key, float size)
        {
            Number number = null;
            if (key != null)
            {
                foreach (Number n in _numbers)
                {
                    if (n.Active && n.Key == key && n.Since < mergeWindow && n.Color == color)
                    {
                        number = n;
                        break;
                    }
                }
            }

            if (number == null)
            {
                number = Oldest();
                number.Active = true;
                number.Key = key;
                number.Amount = 0f;
                number.Age = 0f;
                number.Bump = 0f;
                number.Big = false;
                number.Size = size;
                number.Side = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.5f, 1f);
                number.Tilt = Random.Range(-1f, 1f) * (big ? heavyTilt : heavyTilt / 3f);
                number.At = at + new Vector2(Random.Range(-0.15f, 0.15f), 0f);
                number.Text.gameObject.SetActive(true);
                number.Text.transform.SetAsLastSibling(); // số mới vẽ đè lên số cũ
            }
            else
            {
                // Cộng dồn: nảy nhẹ theo phần mới cộng thêm (laze mỗi khung một ít thì gần như không nảy)
                number.Bump = Mathf.Max(number.Bump, Mathf.Clamp(amount / Mathf.Max(number.Amount, 1f), 0.03f, 0.3f));
                number.Size = Mathf.Max(number.Size, size);
            }

            number.Amount += amount;
            number.Since = 0f;
            number.Big |= big;
            number.Color = color;
            number.Text.text = Mathf.RoundToInt(number.Amount).ToString();
            number.Outline.effectDistance = number.Big ? new Vector2(4f, -4f) : new Vector2(3f, -3f);
            Layout(number); // đặt chỗ ngay, kẻo số mới hiện một khung hình ở chỗ cũ
        }

        Number Oldest()
        {
            Number oldest = _numbers[0];
            foreach (Number n in _numbers)
            {
                if (!n.Active)
                    return n;
                if (n.Age > oldest.Age)
                    oldest = n;
            }

            return oldest;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            foreach (Number n in _numbers)
            {
                if (!n.Active)
                    continue;

                n.Age += dt;
                n.Since += dt;
                n.Bump = Mathf.MoveTowards(n.Bump, 0f, dt * 2.5f);
                if (n.Since > life || n.Age > life + 1.5f) // tắt sau lần cộng dồn cuối, không treo mãi
                {
                    n.Active = false;
                    n.Big = false;
                    n.Text.gameObject.SetActive(false);
                    continue;
                }

                Layout(n);
            }
        }

        // Bật nảy lúc hiện, bay lên theo đường cong rồi mờ dần
        void Layout(Number n)
        {
            float k = Mathf.Clamp01(n.Since / life);
            float up = 1f - Mathf.Exp(-n.Age * 4.5f);   // lên nhanh rồi chậm dần
            float side = 1f - Mathf.Exp(-n.Age * 1.6f); // trôi ngang chậm hơn: đường bay cong dần sang bên
            n.Text.rectTransform.position = n.At + new Vector2(n.Side * drift * side, 0.25f + rise * up);
            float scale = Pop(n.Age / popTime, popOvershoot) * (1f + n.Bump) * (1f - 0.2f * k * k);
            n.Text.rectTransform.localScale = Vector3.one * (scale * n.Size * (n.Big ? heavyScale : 1f));
            n.Text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, n.Tilt);
            Color c = whiteFlash > 0f ? Color.Lerp(Color.white, n.Color, n.Age / whiteFlash) : n.Color;
            c.a = 1f - k * k * k;
            n.Text.color = c;
        }

        /// <summary>Cỡ bật nảy theo t = 0..1: vọt từ nhỏ lên peak (một phần tư đầu) rồi nảy tắt dần về 1.</summary>
        static float Pop(float t, float peak)
        {
            if (t >= 1f)
                return 1f;
            if (t < 0.25f)
            {
                float a = t / 0.25f;
                return Mathf.Lerp(0.35f, peak, 1f - (1f - a) * (1f - a));
            }

            float u = (t - 0.25f) / 0.75f;
            return 1f + (peak - 1f) * Mathf.Cos(u * Mathf.PI * 1.5f) * (1f - u) * (1f - u);
        }
    }
}
