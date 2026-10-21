using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Bật cần điều hướng + nút NHẢY khi chơi nhân vật người (RED, PINK, BLUE); bạch tuộc / nhện điều khiển bằng
    /// cử chỉ nên ẩn đi.
    /// </summary>
    public class HeroControls : MonoBehaviour
    {
        [SerializeField] CombatController combat;
        [Tooltip("Cần điều hướng, nút nhảy… (chỉ hiện với nhân vật người)")]
        [SerializeField] GameObject[] heroOnly = new GameObject[0];

        void Start() => Apply();

        void Update() => Apply();

        void Apply()
        {
            bool hero = combat != null && combat.IsHero;
            foreach (GameObject go in heroOnly)
            {
                if (go != null && go.activeSelf != hero)
                    go.SetActive(hero);
            }
        }
    }
}
