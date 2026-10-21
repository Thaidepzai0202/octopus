using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Màn chọn nhân vật (scene CharacterSelect, mở đầu tiên): 5 thẻ (bạch tuộc, nhện, RED, PINK, BLUE), chạm vào
    /// thẻ để chọn, bấm Bắt đầu để vào màn hang động, hoặc Đánh boss để vào thẳng màn đánh boss sư tử. Thẻ đang chọn to lên,
    /// viền sáng nhấp nháy, chân dung nhún nhẹ; thẻ khác mờ đi; dòng hướng dẫn đổi theo cách điều khiển của nhân vật đang chọn.
    /// Các thẻ chia đều theo bề rộng hàng (màn 16:9 tới 4:3 đều vừa), rộng tối đa Card Max Width.
    /// </summary>
    public class CharacterSelectMenu : MonoBehaviour
    {
        [Serializable]
        public class Card
        {
            public PlayableCharacter character;
            public Button button;
            public RectTransform frame;
            public Image glow;
            public RectTransform portrait;
            public CanvasGroup group;
            [Tooltip("Dòng hướng dẫn điều khiển hiện khi chọn thẻ này")]
            [TextArea(1, 2)] public string hint;
        }

        [SerializeField] Card[] cards = Array.Empty<Card>();
        [Tooltip("Dòng hướng dẫn dưới tiêu đề (đổi theo thẻ đang chọn)")]
        [SerializeField] Text hint;
        [Tooltip("Hàng thẻ: các thẻ chia đều theo bề rộng hàng")]
        [SerializeField] RectTransform cardRow;
        [SerializeField, Min(50f)] float cardMaxWidth = 330f;
        [SerializeField, Min(0f)] float cardGap = 24f;
        [SerializeField] Button startButton;
        [Tooltip("Scene màn chơi")]
        [SerializeField] string gameScene = "Playground";
        [Tooltip("Nút Đánh boss (cạnh nút Bắt đầu)")]
        [SerializeField] Button bossButton;
        [Tooltip("Scene màn đánh boss")]
        [SerializeField] string bossScene = "BossArena";

        Vector2[] _portraitHome = Array.Empty<Vector2>();
        float _laidOutWidth = -1f;

        public PlayableCharacter Selected { get; private set; }

        public int CardCount => cards.Length;

        void Start()
        {
            _portraitHome = new Vector2[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                Card card = cards[i];
                card.button.onClick.AddListener(() => Select(card.character));
                _portraitHome[i] = card.portrait.anchoredPosition;
            }

            startButton.onClick.AddListener(StartGame);
            if (bossButton != null)
                bossButton.onClick.AddListener(StartBoss);
            Select(CharacterSelection.HasChoice ? CharacterSelection.Current : PlayableCharacter.Octopus);
            LayoutCards();
        }

        public void Select(PlayableCharacter character)
        {
            Selected = character;
            if (hint == null)
                return;
            foreach (Card card in cards)
            {
                if (card.character == character && !string.IsNullOrEmpty(card.hint))
                    hint.text = card.hint;
            }
        }

        // Chia đều các thẻ theo bề rộng hàng: màn rộng thì thẻ to tới cardMaxWidth, màn 4:3 thì thẻ hẹp lại cho vừa
        void LayoutCards()
        {
            if (cardRow == null || cards.Length == 0)
                return;
            float width = cardRow.rect.width;
            if (Mathf.Abs(width - _laidOutWidth) < 0.5f)
                return;
            _laidOutWidth = width;

            int n = cards.Length;
            float w = Mathf.Min(cardMaxWidth, (width - cardGap * (n - 1)) / n);
            float total = w * n + cardGap * (n - 1);
            for (int i = 0; i < n; i++)
            {
                RectTransform frame = cards[i].frame;
                frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
                frame.sizeDelta = new Vector2(w, frame.sizeDelta.y);
                frame.anchoredPosition = new Vector2(-total * 0.5f + w * 0.5f + i * (w + cardGap), 0f);
            }
        }

        /// <summary>Vào màn chơi với nhân vật đang chọn.</summary>
        public void StartGame()
        {
            CharacterSelection.Choose(Selected);
            SceneManager.LoadScene(gameScene);
        }

        /// <summary>Vào màn đánh boss với nhân vật đang chọn.</summary>
        public void StartBoss()
        {
            CharacterSelection.Choose(Selected);
            SceneManager.LoadScene(bossScene);
        }

        void Update()
        {
            LayoutCards();
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            float follow = 1f - Mathf.Exp(-12f * dt);
            for (int i = 0; i < cards.Length; i++)
            {
                Card card = cards[i];
                bool on = card.character == Selected;
                float scale = Mathf.Lerp(card.frame.localScale.x, on ? 1.05f : 0.93f, follow);
                card.frame.localScale = Vector3.one * scale;
                card.group.alpha = Mathf.Lerp(card.group.alpha, on ? 1f : 0.55f, follow);

                Color glow = card.glow.color;
                glow.a = Mathf.Lerp(glow.a, on ? 0.55f + 0.25f * Mathf.Sin(time * 3f) : 0f, follow);
                card.glow.color = glow;

                float bob = on ? 10f * Mathf.Sin(time * 2.2f) : 0f;
                card.portrait.anchoredPosition = _portraitHome[i] + new Vector2(0f, bob);
            }
        }
    }
}
