using System;
using System.Collections;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Bộ chiêu của người chơi: 3 nút × 3 kiểu = 9 chiêu cho mỗi nhân vật (bạch tuộc / nhện, theo PlayerCharacter).
    ///  - Nút 1 (đánh thường): chạm = combo 3 đòn; vuốt sang 2 / sang 3 = đòn đánh thường kiểu khác.
    ///  - Nút 2 (chiêu): chạm / vuốt sang 1 / vuốt lên 3. Dùng xong thì hồi chiêu (mỗi chiêu hồi riêng: Phun mực đang hồi
    ///    vẫn bắn laze được; vồ mồi đang hồi vẫn đánh thường được).
    ///  - Nút 3 (chiêu cuối): cần nộ qua mốc 1; nộ càng cao càng đau, dùng là xả hết nộ. Chạm / vuốt về 1 / vuốt về 2.
    /// Mỗi lúc chỉ một chiêu; bấm đánh thường trong lúc đòn trước chưa xong thì xếp hàng (tối đa 2 đòn, đủ một combo 3 đòn).
    /// Bàn phím (Editor): 1 / 2 / 3 = chạm nút; giữ một số rồi bấm số khác = vuốt sang nút đó; R = thêm 100 nộ để thử.
    /// </summary>
    [RequireComponent(typeof(OctopusController), typeof(RageMeter))]
    public class CombatController : MonoBehaviour
    {
        public const int Slots = 3;
        public const int Variants = 3;

        [Tooltip("Lớp của mục tiêu đánh được (bia tập, quái)")]
        [SerializeField] LayerMask targetMask;
        [Tooltip("9 chiêu của bạch tuộc: nút 1 (chạm, →2, →3), nút 2 (chạm, →1, →3), nút 3 (chạm, →1, →2)")]
        [SerializeReference] Skill[] octopusSkills = Array.Empty<Skill>();
        [Tooltip("9 chiêu của nhện, cùng thứ tự")]
        [SerializeReference] Skill[] spiderSkills = Array.Empty<Skill>();

        OctopusController _player;
        PlayerInputReader _input;
        PlayerCharacter _character;
        RageMeter _rage;
        SkillContext _ctx;
        readonly float[] _cooldown = new float[Slots * Variants];
        readonly float[] _cooldownTotal = new float[Slots * Variants];
        Skill _running;
        int _queued; // số đòn đánh thường đang xếp hàng
        int _heldKey = -1;
        bool _heldUsed;

        public RageMeter Rage => _rage;
        public bool Busy => _running != null;
        public Skill Running => _running;

        /// <summary>Vừa dùng chiêu: nút, kiểu, chiêu.</summary>
        public event Action<int, int, Skill> Used;

        public PlayableCharacter Character => _character != null ? _character.Current : PlayableCharacter.Octopus;
        Skill[] ActiveSet => Character == PlayableCharacter.Spider ? spiderSkills : octopusSkills;

        /// <summary>Chiêu ở nút slot (0..2), kiểu variant (0 = chạm, 1 / 2 = vuốt sang nút khác theo thứ tự).</summary>
        public Skill Get(int slot, int variant)
        {
            Skill[] set = ActiveSet;
            int index = Index(slot, variant);
            return index >= 0 && index < set.Length ? set[index] : null;
        }

        static int Index(int slot, int variant) => slot * Variants + variant;

        /// <summary>Số giây hồi chiêu còn lại của chiêu ở nút slot, kiểu variant.</summary>
        public float CooldownRemaining(int slot, int variant = 0) => _cooldown[Index(slot, variant)];

        public float CooldownFraction(int slot, int variant = 0)
        {
            int i = Index(slot, variant);
            return _cooldownTotal[i] > 0f ? Mathf.Clamp01(_cooldown[i] / _cooldownTotal[i]) : 0f;
        }

        /// <summary>Kiểu chiêu khi vuốt từ nút from sang nút to (0..2): 1 hoặc 2.</summary>
        public static int VariantToward(int from, int to)
        {
            int order = 0;
            for (int other = 0; other < Slots; other++)
            {
                if (other == from)
                    continue;
                order++;
                if (other == to)
                    return order;
            }

            return 0;
        }

        void Awake()
        {
            _player = GetComponent<OctopusController>();
            _input = GetComponent<PlayerInputReader>();
            _character = GetComponent<PlayerCharacter>();
            _rage = GetComponent<RageMeter>();
            _ctx = new SkillContext
            {
                Combat = this,
                Player = _player,
                Body = GetComponent<Rigidbody2D>(),
                Rage = _rage,
                Laser = GetComponent<LaserSkill>(),
                Targets = targetMask,
            };
        }

        /// <summary>Chiêu này dùng được ngay không (không bận, đã hồi, đủ nộ với chiêu cuối).</summary>
        public bool CanUse(int slot, int variant)
        {
            if (Get(slot, variant) == null || CooldownRemaining(slot, variant) > 0f)
                return false;
            if (slot == 2 && _rage.Tier < 1)
                return false;
            return true;
        }

        /// <summary>Dùng chiêu. Đang bận thì chỉ đánh thường được xếp hàng để nối combo.</summary>
        public bool Use(int slot, int variant)
        {
            if (!CanUse(slot, variant))
                return false;

            if (_running != null)
            {
                bool combo = slot == 0 && variant == 0 && _running == Get(0, 0) && _queued < 2;
                if (combo)
                    _queued++;
                return combo;
            }

            Skill skill = Get(slot, variant);
            _ctx.Anim = GetComponentInChildren<ICombatAnimator>();
            _ctx.Laser = GetComponent<LaserSkill>();
            _ctx.Tier = 0;
            _ctx.Power = 1f;
            _ctx.GainsRage = slot != 2;
            if (slot == 2)
            {
                if (!_rage.TrySpend(out int tier))
                    return false;
                _ctx.Tier = tier;
                _ctx.Power = _rage.Multiplier(tier);
            }

            StartCoroutine(Run(slot, variant, skill));
            return true;
        }

        IEnumerator Run(int slot, int variant, Skill skill)
        {
            _running = skill;
            Used?.Invoke(slot, variant, skill);
            yield return skill.Run(_ctx);
            _running = null;
            if (skill.cooldown > 0f)
            {
                _cooldown[Index(slot, variant)] = skill.cooldown;
                _cooldownTotal[Index(slot, variant)] = skill.cooldown;
            }

            if (_queued > 0)
            {
                _queued--;
                Use(0, 0);
            }
        }

        /// <summary>Chạy một việc kéo dài sau khi chiêu đã xong (vùng mực, nhện con…) mà không giữ người chơi bận.</summary>
        public void Linger(IEnumerator routine) => StartCoroutine(routine);

        void Update()
        {
            for (int i = 0; i < _cooldown.Length; i++)
                _cooldown[i] = Mathf.Max(0f, _cooldown[i] - Time.deltaTime);
            ReadKeyboard();
        }

        // Giữ một số rồi bấm số khác = vuốt từ nút này sang nút kia; nhả số mà chưa bấm số nào khác = chạm
        void ReadKeyboard()
        {
            if (_input == null || !_input.isActiveAndEnabled)
                return;

            if (_input.RageCheatPressed)
                _rage.Add(RageMeter.PerTier);

            for (int slot = 0; slot < Slots; slot++)
            {
                if (!_input.SkillKeyDown(slot))
                    continue;
                if (_heldKey < 0)
                {
                    _heldKey = slot;
                    _heldUsed = false;
                }
                else if (slot != _heldKey && !_heldUsed)
                {
                    Use(_heldKey, VariantToward(_heldKey, slot));
                    _heldUsed = true;
                }
            }

            if (_heldKey >= 0 && _input.SkillKeyUp(_heldKey))
            {
                if (!_heldUsed)
                    Use(_heldKey, 0);
                _heldKey = -1;
            }
        }

        /// <summary>Bộ chiêu mặc định (Editor dựng màn chơi gọi để điền sẵn 18 chiêu).</summary>
        public void SetDefaultSkills(Skill[] octopus, Skill[] spider)
        {
            octopusSkills = octopus;
            spiderSkills = spider;
        }
    }
}
