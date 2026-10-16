using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Màn chọn nhân vật (scene CharacterSelect, mở đầu tiên): chạm vào thẻ để chọn bạch tuộc hay nhện, bấm Bắt đầu để
    /// vào màn chơi. Thẻ đang chọn to lên, viền sáng nhấp nháy, nhân vật nhún nhẹ; thẻ kia mờ đi.
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
        }

        [SerializeField] Card[] cards = Array.Empty<Card>();
        [SerializeField] Button startButton;
        [Tooltip("Scene màn chơi")]
        [SerializeField] string gameScene = "Playground";

        Vector2[] _portraitHome = Array.Empty<Vector2>();

        public PlayableCharacter Selected { get; private set; }

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
            Select(CharacterSelection.HasChoice ? CharacterSelection.Current : PlayableCharacter.Octopus);
        }

        public void Select(PlayableCharacter character) => Selected = character;

        /// <summary>Vào màn chơi với nhân vật đang chọn.</summary>
        public void StartGame()
        {
            CharacterSelection.Choose(Selected);
            SceneManager.LoadScene(gameScene);
        }

        void Update()
        {
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
