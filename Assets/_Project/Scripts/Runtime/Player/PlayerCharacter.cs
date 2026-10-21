using UnityEngine;
using UnityEngine.SceneManagement;

namespace Octopus
{
    public enum PlayableCharacter
    {
        Octopus,
        Spider,
        Red,   // nhân vật người RED (Docs/nhan_vat_moi/red)
        Pink,  // nhân vật người PINK (Docs/nhan_vat_moi/pink)
        Blue,  // nhân vật người BLUE (Docs/nhan_vat_moi/blue)
    }

    public static class PlayableCharacterExtensions
    {
        /// <summary>Nhân vật người (RED, PINK, BLUE): điều khiển kiểu đối kháng đi cảnh (HeroMotor), không đu dây.</summary>
        public static bool IsHero(this PlayableCharacter character) =>
            character is PlayableCharacter.Red or PlayableCharacter.Pink or PlayableCharacter.Blue;

        /// <summary>Tên hiện trên màn hình.</summary>
        public static string DisplayName(this PlayableCharacter character) => character switch
        {
            PlayableCharacter.Spider => "Nhện",
            PlayableCharacter.Red => "RED",
            PlayableCharacter.Pink => "PINK",
            PlayableCharacter.Blue => "BLUE",
            _ => "Bạch tuộc",
        };
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
    /// Bật hình của nhân vật được chọn trên người chơi. Bạch tuộc và nhện cùng một thân vật lý và cách điều khiển: bò, nhảy,
    /// bắn xúc tu / tơ để đu, bám ngược dưới đá, chiêu laze. Ba nhân vật người (RED, PINK, BLUE) bật thêm HeroMotor:
    /// thân hình người, chạy / nhảy / nhảy đúp bằng cần điều hướng + nút nhảy, đá là bục một chiều.
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
        [Tooltip("Hình RED (chứa \"Hero Visual\" do HeroRigBuilder dựng)")]
        [SerializeField] GameObject redVisual;
        [Tooltip("Hình PINK")]
        [SerializeField] GameObject pinkVisual;
        [Tooltip("Hình BLUE")]
        [SerializeField] GameObject blueVisual;
        [Tooltip("Bộ di chuyển của nhân vật người (bật khi chọn RED / PINK / BLUE)")]
        [SerializeField] HeroMotor heroMotor;

        public PlayableCharacter Current { get; private set; }

        /// <summary>Object hình của một nhân vật (null nếu chưa dựng).</summary>
        public GameObject VisualOf(PlayableCharacter character) => character switch
        {
            PlayableCharacter.Spider => spiderVisual,
            PlayableCharacter.Red => redVisual,
            PlayableCharacter.Pink => pinkVisual,
            PlayableCharacter.Blue => blueVisual,
            _ => octopusVisual,
        };

        /// <summary>Hình nhân vật người đang chơi (null với bạch tuộc / nhện).</summary>
        public IHeroVisual HeroVisual
        {
            get
            {
                GameObject visual = Current.IsHero() ? VisualOf(Current) : null;
                return visual != null ? visual.GetComponentInChildren<IHeroVisual>(true) : null;
            }
        }

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
            SetActive(redVisual, character == PlayableCharacter.Red);
            SetActive(pinkVisual, character == PlayableCharacter.Pink);
            SetActive(blueVisual, character == PlayableCharacter.Blue);

            if (!Application.isPlaying || heroMotor == null)
                return;
            if (character.IsHero())
                heroMotor.Activate(character, HeroVisual);
            else
                heroMotor.Deactivate();
        }

        static void SetActive(GameObject visual, bool active)
        {
            if (visual != null && visual.activeSelf != active)
                visual.SetActive(active);
        }
    }
}
