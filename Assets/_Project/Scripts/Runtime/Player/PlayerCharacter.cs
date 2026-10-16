using UnityEngine;
using UnityEngine.SceneManagement;

namespace Octopus
{
    public enum PlayableCharacter
    {
        Octopus,
        Spider,
    }

    /// <summary>Nhân vật người chơi chọn ở màn chọn nhân vật, mang sang màn chơi.</summary>
    public static class CharacterSelection
    {
        public const string SelectScene = "CharacterSelect";

        public static bool HasChoice { get; private set; }
        public static PlayableCharacter Current { get; private set; }

        /// <summary>Scene mở lúc bắt đầu chạy (bấm Play trong Editor, hoặc mở app).</summary>
        public static string StartupScene { get; private set; } = "";

        public static void Choose(PlayableCharacter character)
        {
            Current = character;
            HasChoice = true;
        }

        public static void Clear()
        {
            Current = PlayableCharacter.Octopus;
            HasChoice = false;
        }

        // Editor tắt domain reload nên biến static còn từ lần Play trước: xoá khi bắt đầu Play
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RecordStartup() => StartupScene = SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// Bật hình của nhân vật được chọn (bạch tuộc hoặc nhện) trên người chơi. Cùng một thân vật lý và cách điều khiển:
    /// bò, nhảy, bắn xúc tu / tơ để đu, bám ngược dưới đá, chiêu laze.
    /// Bấm Play ngay trong scene màn chơi thì mở màn chọn nhân vật trước (như khi mở app); tắt ô Choose On Play để vào
    /// thẳng bằng nhân vật ở ô Start With.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)] // tắt hình không dùng trước khi nó kịp chạy
    public class PlayerCharacter : MonoBehaviour
    {
        [Tooltip("Bấm Play ngay trong scene này: bật = mở màn chọn nhân vật trước; tắt = vào thẳng bằng nhân vật ở ô Start With")]
        [SerializeField] bool chooseOnPlay = true;
        [Tooltip("Nhân vật hiện trong Scene (khi không Play) và khi vào thẳng màn chơi (tắt Choose On Play)")]
        [SerializeField] PlayableCharacter startWith = PlayableCharacter.Octopus;
        [SerializeField] GameObject octopusVisual;
        [SerializeField] GameObject spiderVisual;

        public PlayableCharacter Current { get; private set; }

        void Awake()
        {
            if (!Application.isPlaying)
                return;

            // Bấm Play ngay trong màn chơi (chưa qua màn chọn nhân vật): mở màn chọn nhân vật trước
            if (chooseOnPlay && !CharacterSelection.HasChoice && gameObject.scene.name == CharacterSelection.StartupScene &&
                Application.CanStreamedLevelBeLoaded(CharacterSelection.SelectScene))
            {
                SceneManager.LoadScene(CharacterSelection.SelectScene);
                return;
            }

            Apply(CharacterSelection.HasChoice ? CharacterSelection.Current : startWith);
        }

        void Update()
        {
            if (!Application.isPlaying)
                Apply(startWith);
        }

        void Apply(PlayableCharacter character)
        {
            Current = character;
            SetActive(octopusVisual, character == PlayableCharacter.Octopus);
            SetActive(spiderVisual, character == PlayableCharacter.Spider);
        }

        static void SetActive(GameObject visual, bool active)
        {
            if (visual != null && visual.activeSelf != active)
                visual.SetActive(active);
        }
    }
}
