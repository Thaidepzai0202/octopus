using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Cảm giác đánh: khựng hình khi trúng đòn (HitStop trên object "Game Feel"), rung camera (CameraShake gắn vào Follow
    /// Camera, extension của Cinemachine) và vòng sao vàng quay quanh đầu bia tập khi choáng (thay bộ sao cũ của
    /// CombatBuilder). Gọi sau CombatBuilder và sau khi đã có Follow Camera.
    /// </summary>
    static class GameFeelBuilder
    {
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);

        public static void Build()
        {
            new GameObject("Game Feel").AddComponent<HitStop>();

            foreach (CinemachineCamera camera in Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                if (camera.GetComponent<CameraShake>() == null)
                    camera.gameObject.AddComponent<CameraShake>();
            }

            Sprite star = StarSprite();
            Sprite ring = RingSprite();
            foreach (TrainingDummy dummy in Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
                BuildDizzy(dummy, star, ring);
        }

        // Vòng sao choáng: một vòng elip vàng mảnh + 4 ngôi sao 5 cánh chạy quanh (TrainingDummy cho sao chạy vòng)
        static void BuildDizzy(TrainingDummy dummy, Sprite star, Sprite ring)
        {
            var so = new SerializedObject(dummy);
            var stars = so.FindProperty("stars").objectReferenceValue as Transform;
            if (stars == null)
            {
                stars = RigArt.NewChild("Stars", dummy.transform);
                stars.localPosition = new Vector3(0f, 2.15f, 0f);
                so.FindProperty("stars").objectReferenceValue = stars;
            }

            for (int i = stars.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(stars.GetChild(i).gameObject);
            stars.localPosition = new Vector3(0f, 2.1f, 0f);

            SpriteRenderer halo = RigArt.NewSprite("Ring", stars, ring, 21);
            halo.color = Gold;
            SerializedProperty list = so.FindProperty("dizzyStars");
            list.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                SpriteRenderer s = RigArt.NewSprite($"Star {i}", stars, star, 22);
                s.transform.localScale = Vector3.one * 0.3f;
                list.GetArrayElementAtIndex(i).objectReferenceValue = s;
            }

            so.FindProperty("dizzyRing").objectReferenceValue = halo;
            so.ApplyModifiedPropertiesWithoutUndo();
            stars.gameObject.SetActive(false);
        }

        // Sao 5 cánh vàng: lõi sáng gần trắng, viền cam đậm, quầng sáng mờ bao ngoài
        static Sprite StarSprite() => ArtFactory.Sprite("DizzyStar", 96, 96, 192f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            const float sector = 2f * Mathf.PI / 5f;
            float phi = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(x, y) + sector / 2f, sector) - sector / 2f); // 0 = hướng mũi sao
            // Bán kính mép sao theo góc: đường thẳng nối mũi (bán kính 0.86) với chỗ lõm (0.38)
            var tip = new Vector2(0.86f, 0f);
            var notch = new Vector2(0.38f * Mathf.Cos(sector / 2f), 0.38f * Mathf.Sin(sector / 2f));
            Vector2 edge = notch - tip;
            var ray = new Vector2(Mathf.Cos(phi), Mathf.Sin(phi));
            float edgeR = (tip.x * edge.y - tip.y * edge.x) / (ray.x * edge.y - ray.y * edge.x);
            float inside = (edgeR - r) * 48f; // px tới mép (dương = trong sao)
            float body = Mathf.Clamp01(inside + 0.5f);
            float glow = 0.4f * Mathf.Exp(-Mathf.Max(0f, -inside) / 7f) * (1f - body);
            float core = Mathf.Clamp01(1f - r / Mathf.Max(edgeR, 0.01f));
            Color fill = Color.Lerp(new Color(0.95f, 0.55f, 0.08f), new Color(1f, 0.98f, 0.8f), Mathf.SmoothStep(0f, 1f, core * 1.6f));
            if (inside < 4f)
                fill = Color.Lerp(new Color(0.8f, 0.42f, 0.05f), fill, Mathf.Clamp01(inside / 4f)); // viền đậm
            Color c = fill * body + new Color(1f, 0.85f, 0.35f) * glow;
            c.a = Mathf.Clamp01(body + glow);
            if (c.a > 0f)
            {
                c.r /= c.a;
                c.g /= c.a;
                c.b /= c.a;
            }

            return c;
        }, 2, false);

        // Vòng elip mảnh (90% khung ảnh, tỉ lệ 3.5 : 1): nửa trước sáng, nửa sau mờ hơn cho ra vòng nằm ngang
        static Sprite RingSprite() => ArtFactory.Sprite("DizzyRing", 224, 64, 224f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float px = (u - 0.5f) * 224f, py = (v - 0.5f) * 64f;
            float a = 112f * 0.9f, b = 32f * 0.9f;
            float f = px * px / (a * a) + py * py / (b * b) - 1f;
            float grad = 2f * Mathf.Sqrt(px * px / (a * a * a * a) + py * py / (b * b * b * b));
            float d = Mathf.Abs(f) / Mathf.Max(grad, 1e-4f); // khoảng cách (px) tới đường elip
            float line = Mathf.Clamp01(4.5f - d);
            float halo = 0.35f * Mathf.Exp(-d * d / 60f);
            float front = py < 0f ? 1f : 0.55f;
            float alpha = Mathf.Clamp01(line + halo) * front;
            Color c = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.6f), 1f - line);
            c.a = alpha;
            return c;
        }, 2, false);
    }
}
