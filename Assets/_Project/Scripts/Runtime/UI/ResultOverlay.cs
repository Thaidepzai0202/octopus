using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Màn kết thúc trận đánh boss, phủ cả màn hình:
    ///  - Người chơi gục: "THẤT BẠI", nút Thử lại (đánh lại màn boss) và Đổi nhân vật (về màn chọn nhân vật).
    ///  - Boss gục: "CHIẾN THẮNG" kèm thời gian đánh, nút Đánh lại và Về màn chọn.
    /// Hiện dần lên, chữ tiêu đề bật nảy. Không đổi Time.timeScale (người chơi đã bị tắt điều khiển).
    /// </summary>
    public class ResultOverlay : MonoBehaviour
    {
        [SerializeField] LionBoss boss;
        [SerializeField] PlayerHealth player;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image shade;
        [SerializeField] Text title;
        [SerializeField] Text info;
        [Tooltip("Nút trái: Thử lại / Đánh lại")]
        [SerializeField] Button retryButton;
        [SerializeField] Text retryLabel;
        [Tooltip("Nút phải: Đổi nhân vật / Về màn chọn")]
        [SerializeField] Button menuButton;
        [SerializeField] Text menuLabel;

        [SerializeField] Color victoryColor = new Color(1f, 0.82f, 0.35f);
        [SerializeField] Color defeatColor = new Color(1f, 0.3f, 0.28f);
        [SerializeField] Color victoryShade = new Color(0.12f, 0.06f, 0.02f, 0.72f);
        [SerializeField] Color defeatShade = new Color(0.08f, 0.01f, 0.02f, 0.78f);

        float _shownAt = -1f;

        public bool IsShowing { get; private set; }
        public bool IsVictory { get; private set; }
        public string Title => title != null ? title.text : "";
        public string Info => info != null ? info.text : "";
        public Button RetryButton => retryButton;
        public Button MenuButton => menuButton;

        void Awake()
        {
            if (boss == null)
                boss = FindAnyObjectByType<LionBoss>();
            if (player == null)
                player = FindAnyObjectByType<PlayerHealth>();
            Hide();
        }

        void OnEnable()
        {
            if (boss != null)
                boss.Defeated += OnBossDefeated;
            if (player != null)
                player.Defeated += OnPlayerDefeated;
            retryButton.onClick.AddListener(Retry);
            menuButton.onClick.AddListener(ChangeCharacter);
        }

        void OnDisable()
        {
            if (boss != null)
                boss.Defeated -= OnBossDefeated;
            if (player != null)
                player.Defeated -= OnPlayerDefeated;
            retryButton.onClick.RemoveListener(Retry);
            menuButton.onClick.RemoveListener(ChangeCharacter);
        }

        void OnBossDefeated()
        {
            if (IsShowing)
                return;
            // Thắng rồi thì thôi điều khiển (khỏi bấm nhầm khi chọn nút)
            if (player != null)
            {
                var input = player.GetComponent<PlayerInputReader>();
                if (input != null)
                    input.enabled = false;
                player.Controller.SetIntent(false, 0f);
            }

            ShowVictory(boss != null ? boss.FightTime : 0f);
        }

        void OnPlayerDefeated()
        {
            if (!IsShowing)
                ShowDefeat();
        }

        public void ShowVictory(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            Show(true, "CHIẾN THẮNG", $"Hạ {LionBoss.DisplayName} trong {total / 60}:{total % 60:00}", "Đánh lại", "Về màn chọn");
        }

        public void ShowDefeat()
        {
            string info = boss != null && boss.Health != null
                ? $"{LionBoss.DisplayName} còn {Mathf.CeilToInt(boss.HealthFraction * 100f)}% máu"
                : "";
            Show(false, "THẤT BẠI", info, "Thử lại", "Đổi nhân vật");
        }

        void Show(bool victory, string heading, string details, string retry, string menu)
        {
            IsShowing = true;
            IsVictory = victory;
            gameObject.SetActive(true);
            title.text = heading;
            title.color = victory ? victoryColor : defeatColor;
            info.text = details;
            retryLabel.text = retry;
            menuLabel.text = menu;
            shade.color = victory ? victoryShade : defeatShade;
            group.interactable = group.blocksRaycasts = true;
            _shownAt = Time.unscaledTime;
        }

        void Hide()
        {
            IsShowing = false;
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
        }

        /// <summary>Đánh lại màn boss (cùng nhân vật).</summary>
        public void Retry() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        /// <summary>Về màn chọn nhân vật.</summary>
        public void ChangeCharacter() => SceneManager.LoadScene(CharacterSelection.SelectScene);

        void Update()
        {
            if (!IsShowing)
                return;
            float t = Time.unscaledTime - _shownAt;
            group.alpha = Mathf.Clamp01(t / 0.35f);
            float pop = t < 0.25f ? Mathf.Lerp(1.6f, 0.95f, t / 0.25f) : Mathf.Lerp(0.95f, 1f, Mathf.Clamp01((t - 0.25f) / 0.15f));
            title.rectTransform.localScale = Vector3.one * pop;
        }
    }
}
