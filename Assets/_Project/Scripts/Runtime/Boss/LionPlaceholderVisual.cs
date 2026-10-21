using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hình tạm của boss sư tử (khối đỏ sẫm cao height) để làm AI / map trước khi có hoạt ảnh gắn xương.
    /// LionRigBuilder sẽ thay bằng LionRigAnimator; AI chỉ dùng ILionVisual nên không phải sửa gì.
    /// </summary>
    public class LionPlaceholderVisual : MonoBehaviour, ILionVisual
    {
        [SerializeField] float height = 7f;
        [SerializeField] SpriteRenderer body;

        float _facing = 1f, _flash, _enrage, _actLeft;
        LionPose _pose;

        public float Facing
        {
            get => _facing;
            set => _facing = value < 0f ? -1f : 1f;
        }

        public void Setup(float targetHeight, SpriteRenderer renderer)
        {
            height = targetHeight;
            body = renderer;
        }

        public void SetLocomotion(float velocityX, bool grounded) { }

        public void Act(LionPose pose, float duration, bool hold = false)
        {
            _pose = pose;
            _actLeft = hold ? float.PositiveInfinity : duration;
        }

        public void Flash(float strength) => _flash = Mathf.Max(_flash, strength);
        public void SetEnrage(float amount) => _enrage = Mathf.Clamp01(amount);

        public Vector2 MouthPoint => (Vector2)transform.position + new Vector2(_facing * height * 0.55f, height * 0.75f);
        public Vector2 PawPoint => (Vector2)transform.position + new Vector2(_facing * height * 0.6f, height * 0.15f);
        public Bounds VisualBounds => new Bounds(transform.position + Vector3.up * height * 0.5f, new Vector3(height * 1.4f, height, 0f));

        void Update()
        {
            _actLeft -= Time.deltaTime;
            if (_actLeft <= 0f)
                _pose = LionPose.None;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 4f);
            if (body == null)
                return;
            float tilt = _pose switch
            {
                LionPose.RearUp or LionPose.Roar => 12f,
                LionPose.Crouch or LionPose.ChargeWindup => -6f,
                LionPose.Stagger or LionPose.Die => -70f,
                _ => 0f,
            };
            body.transform.localRotation = Quaternion.Euler(0f, 0f, tilt * _facing);
            body.transform.localScale = new Vector3(height * 1.4f * _facing, height, 1f);
            body.color = Color.Lerp(Color.Lerp(new Color(0.35f, 0.05f, 0.08f), new Color(0.8f, 0.1f, 0.1f), _enrage), Color.white, _flash);
        }
    }
}
