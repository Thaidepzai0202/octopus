using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng tia laze bắn từ mắt: LaserSkill + hiệu ứng (Laser FX) trên người chơi. Các chiêu laze của bộ chiêu
    /// (CombatController) gọi nó. Cũng giữ các ảnh / vật liệu dùng chung cho hiệu ứng (vệt sáng, sao chớp, khói, đĩa, vòng).
    /// </summary>
    static class LaserSkillBuilder
    {
        public static void Build(GameObject player, Sprite glow, LayerMask blockMask)
        {
            var skill = player.AddComponent<LaserSkill>();
            var settings = new SerializedObject(skill);
            settings.FindProperty("blockMask").intValue = blockMask;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var fx = new GameObject("Laser FX");
            fx.transform.SetParent(player.transform, false);
            var view = fx.AddComponent<LaserBeamView>();
            var viewSettings = new SerializedObject(view);
            viewSettings.FindProperty("skill").objectReferenceValue = skill;
            viewSettings.FindProperty("beamMaterial").objectReferenceValue = BeamMaterial();
            viewSettings.FindProperty("glow").objectReferenceValue = glow;
            viewSettings.FindProperty("flare").objectReferenceValue = FlareSprite();
            viewSettings.FindProperty("smoke").objectReferenceValue = SmokeSprite();
            viewSettings.FindProperty("ring").objectReferenceValue = RingSprite(); // vòng sáng co vào mắt khi vận sức
            viewSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Art ----------

        public static Sprite DiscSprite() => ArtFactory.Sprite("UiDisc", 256, 256, 100f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - r) * 128f));
        }, 2, false);

        // Viền tròn sáng: nét giữa đậm, hai bên mờ dần
        public static Sprite RingSprite() => ArtFactory.Sprite("UiRing", 256, 256, 100f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
            float d = Mathf.Abs(r - 0.93f);
            float core = Mathf.Clamp01((0.035f - d) * 128f);
            float halo = 0.45f * Mathf.Exp(-d * d / (0.04f * 0.04f));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(core, halo)) * Mathf.Clamp01((1f - r) * 128f));
        }, 2, false);

        // Mặt cắt ngang của tia: đặc ở giữa, mềm dần ra hai mép (khác vệt viền đá chỉ có lõi mảnh)
        public static Material BeamMaterial()
        {
            Texture2D profile = ArtFactory.Texture("LaserBeam", 8, 64, TextureWrapMode.Clamp, (u, v) =>
            {
                float d = Mathf.Abs(v * 2f - 1f);
                return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Max(0f, 1f - d * d), 2f));
            });
            return ArtFactory.Material("LaserBeam", profile);
        }

        // Ngôi sao 4 cánh có quầng: chớp sáng ở mắt và chỗ tia trúng
        public static Sprite FlareSprite() => ArtFactory.Sprite("LaserFlare", 128, 128, 128f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float x = Mathf.Abs(u * 2f - 1f), y = Mathf.Abs(v * 2f - 1f);
            float star = Mathf.Exp(-x * 22f) * Mathf.Pow(Mathf.Max(0f, 1f - y), 2f) +
                         Mathf.Exp(-y * 22f) * Mathf.Pow(Mathf.Max(0f, 1f - x), 2f);
            float glow = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Sqrt(x * x + y * y)), 3f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(star + 0.8f * glow));
        }, 1, false);

        // Cụm khói mềm, mép loang lổ
        public static Sprite SmokeSprite() => ArtFactory.Sprite("LaserSmoke", 64, 64, 64f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            Vector2 p = new Vector2(u * 2f - 1f, v * 2f - 1f);
            float noise = 0.65f + 0.35f * Noise.Fbm(u * 4f, v * 4f, 11);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Pow(Mathf.Max(0f, 1f - p.magnitude), 1.5f) * noise));
        }, 1, false);
    }
}
