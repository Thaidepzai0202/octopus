using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hình tạm của nhân vật người (một khối màu cao height) để làm bộ điều khiển / chiêu trước khi có hoạt ảnh gắn xương.
    /// HeroRigBuilder sẽ thay bằng HeroRigAnimator; code khác chỉ dùng IHeroVisual nên không phải sửa gì.
    /// </summary>
    public class HeroPlaceholderVisual : MonoBehaviour, IHeroVisual
    {
        [SerializeField] float height = 2f;
        [SerializeField] SpriteRenderer body;
        [SerializeField] Color color = Color.white;

        float _facing = 1f, _flash, _ghost, _power, _actLeft;
        CombatPose _pose;
        Vector2 _aim;

        public float Facing
        {
            get => _facing;
            set => _facing = value < 0f ? -1f : 1f;
        }

        public void Setup(float targetHeight, SpriteRenderer renderer, Color tint)
        {
            height = targetHeight;
            body = renderer;
            color = tint;
        }

        public void SetMove(HeroMove move, Vector2 velocity) { }

        public void Act(CombatPose pose, float duration, Vector2 point)
        {
            _pose = pose;
            _aim = point;
            _actLeft = duration;
        }

        public void Flash(float strength) => _flash = Mathf.Max(_flash, strength);
        public void SetGhost(float amount) => _ghost = Mathf.Clamp01(amount);
        public void SetWeaponPower(float amount) => _power = Mathf.Clamp01(amount);

        public Vector2 WeaponTip => (Vector2)transform.position + new Vector2(_facing * height * 0.6f, height * 0.55f);
        public Vector2 CastPoint => (Vector2)transform.position + new Vector2(_facing * height * 0.35f, height * 0.6f);
        public Bounds VisualBounds => new Bounds(transform.position + Vector3.up * height * 0.5f, new Vector3(height * 0.6f, height, 0f));

        void Update()
        {
            _actLeft -= Time.deltaTime;
            if (_actLeft <= 0f)
                _pose = CombatPose.None;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 4f);
            if (body == null)
                return;
            float tilt = _pose switch
            {
                CombatPose.Whip or CombatPose.Lunge or CombatPose.Reach => -12f,
                CombatPose.WhipAlt or CombatPose.Rear => 10f,
                CombatPose.Spin => Time.time * 720f,
                _ => 0f,
            };
            body.transform.localRotation = Quaternion.Euler(0f, 0f, tilt * _facing);
            body.transform.localScale = new Vector3(height * 0.6f * (1f + 0.3f * _power) * _facing, height, 1f);
            Color c = Color.Lerp(color, Color.white, _flash);
            c.a = 1f - 0.6f * _ghost;
            body.color = c;
        }
    }
}
