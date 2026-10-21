using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Bộ chiêu của người chơi: 3 nút × 3 kiểu = 9 chiêu cho mỗi nhân vật (bạch tuộc, nhện, RED, PINK, BLUE, theo
    /// PlayerCharacter).
    ///  - Nút 1 (đánh thường): chạm = combo 3 đòn; vuốt sang 2 / sang 3 = đòn đánh thường kiểu khác.
    ///  - Nút 2 (chiêu): chạm / vuốt sang 1 / vuốt lên 3. Dùng xong thì hồi chiêu (mỗi chiêu hồi riêng: Phun mực đang hồi
    ///    vẫn bắn laze được; vồ mồi đang hồi vẫn đánh thường được).
    ///  - Nút 3 (chiêu cuối): cần nộ qua mốc 1; nộ càng cao càng đau, dùng là xả hết nộ. Chạm / vuốt về 1 / vuốt về 2.
    /// Mỗi lúc chỉ một chiêu; bấm đánh thường trong lúc đòn trước chưa xong thì xếp hàng (tối đa 2 đòn, đủ một combo 3 đòn).
    /// Bàn phím (Editor): J / K / L = chạm nút 1 / 2 / 3; giữ một phím rồi bấm phím kia = vuốt sang nút đó (nhân vật người: giữ
    /// W / S rồi bấm J / K / L = kiểu ↑ / ↓); R = thêm 100 nộ để thử.
    ///
    /// Nhân vật người không vuốt: kiểu chiêu theo cần điều hướng lúc bấm nút (thả / ngang = thường, kéo lên = ↑, kéo xuống = ↓;
    /// thứ tự trong bộ 9 chiêu: nút 1 thường, ↑, ↓, nút 2 thường, ↑, ↓, nút 3 thường, ↑, ↓). Bàn phím: giữ W / S rồi bấm J / K / L.
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
        [Tooltip("9 chiêu của RED: nút 1 (thường, ↑, ↓), nút 2 (thường, ↑, ↓), nút 3 (thường, ↑, ↓)")]
        [SerializeReference] Skill[] redSkills = Array.Empty<Skill>();
        [Tooltip("9 chiêu của PINK, cùng thứ tự")]
        [SerializeReference] Skill[] pinkSkills = Array.Empty<Skill>();
        [Tooltip("9 chiêu của BLUE, cùng thứ tự")]
        [SerializeReference] Skill[] blueSkills = Array.Empty<Skill>();

        [Header("Cỡ / tầm chiêu (nhân với mọi chiêu của nhân vật đó)")]
        [SerializeField] SkillReach octopusReach = SkillReach.Double;
        [SerializeField] SkillReach spiderReach = SkillReach.Double;
        [SerializeField] SkillReach redReach = SkillReach.Double;
        [SerializeField] SkillReach pinkReach = SkillReach.Double;
        [SerializeField] SkillReach blueReach = SkillReach.Double;

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
        readonly Dictionary<string, (float until, float value)> _buffs = new Dictionary<string, (float, float)>();

        /// <summary>Hệ số cỡ / tầm chiêu của nhân vật c (ô Cỡ / tầm chiêu trong Inspector).</summary>
        public SkillReach ReachOf(PlayableCharacter c) => c switch
        {
            PlayableCharacter.Spider => spiderReach,
            PlayableCharacter.Red => redReach,
            PlayableCharacter.Pink => pinkReach,
            PlayableCharacter.Blue => blueReach,
            _ => octopusReach,
        };

        public RageMeter Rage => _rage;
        public bool Busy => _running != null;
        public Skill Running => _running;

        /// <summary>Vừa dùng chiêu: nút, kiểu, chiêu.</summary>
        public event Action<int, int, Skill> Used;

        public PlayableCharacter Character => _character != null ? _character.Current : PlayableCharacter.Octopus;

        /// <summary>Đang chơi nhân vật người: kiểu chiêu theo cần điều hướng thay vì vuốt.</summary>
        public bool IsHero => Character.IsHero();

        Skill[] ActiveSet => SkillsOf(Character);

        /// <summary>Bộ 9 chiêu của một nhân vật.</summary>
        public Skill[] SkillsOf(PlayableCharacter character) => character switch
        {
            PlayableCharacter.Spider => spiderSkills,
            PlayableCharacter.Red => redSkills,
            PlayableCharacter.Pink => pinkSkills,
            PlayableCharacter.Blue => blueSkills,
            _ => octopusSkills,
        } ?? Array.Empty<Skill>();

        /// <summary>
        /// Chiêu ở nút slot (0..2), kiểu variant: bạch tuộc / nhện 0 = chạm, 1 / 2 = vuốt sang nút khác theo thứ tự; nhân vật
        /// người 0 = thường, 1 = ↑, 2 = ↓.
        /// </summary>
        public Skill Get(int slot, int variant)
        {
            Skill[] set = ActiveSet;
            int index = Index(slot, variant);
            return index >= 0 && index < set.Length ? set[index] : null;
        }

        /// <summary>Nhân vật người: kiểu chiêu đang chọn bằng cần điều hướng (0 = thường, 1 = ↑, 2 = ↓).</summary>
        public int HeroVariant
        {
            get
            {
                HeroMotor hero = _player != null ? _player.Hero : null;
                return hero != null ? HeroMotor.VariantOf(hero.Aim) : 0;
            }
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
            _ctx = CreateContext();
        }

        /// <summary>
        /// SkillContext mới gắn với người chơi này (chiêu chạy riêng, đòn của động tác di chuyển như nhảy đúp xoay kiếm).
        /// Mỗi chiêu dùng chung một SkillContext do CombatController giữ.
        /// </summary>
        public SkillContext CreateContext() => new SkillContext
        {
            Combat = this,
            Player = _player != null ? _player : GetComponent<OctopusController>(),
            Body = GetComponent<Rigidbody2D>(),
            Rage = _rage != null ? _rage : GetComponent<RageMeter>(),
            Laser = GetComponent<LaserSkill>(),
            Targets = targetMask,
            Health = GetComponent<PlayerHealth>(),
        };

        /// <summary>Chiêu này dùng được ngay không (không bận, đã hồi, đủ nộ với chiêu cuối).</summary>
        public bool CanUse(int slot, int variant)
        {
            if (!isActiveAndEnabled) // người chơi gục (PlayerHealth tắt component này) thì thôi nhận chiêu
                return false;
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
            _ctx.Hero = _player.Hero;
            _ctx.Anim = _ctx.Hero != null && _ctx.Hero.Visual != null ? _ctx.Hero.Visual : GetComponentInChildren<ICombatAnimator>();
            _ctx.Laser = GetComponent<LaserSkill>();
            _ctx.Slot = slot;
            _ctx.Variant = variant;
            _ctx.Tier = 0;
            _ctx.Power = 1f;
            _ctx.GainsRage = slot != 2;
            SkillReach reach = ReachOf(Character);
            _ctx.Size = reach.size;
            _ctx.Range = reach.range;
            if (slot == 2)
            {
                if (!_rage.TrySpend(out int tier))
                    return false;
                _ctx.Tier = tier;
                _ctx.Power = _rage.Multiplier(tier);
            }

            // Mỗi lần dùng chiêu một bản ngữ cảnh riêng: phần kéo dài sau khi chiêu xong (Linger: lồng xích, vùng lửa…)
            // giữ đúng mốc nộ / cờ cộng nộ của lần dùng đó, không bị chiêu dùng sau ghi đè
            StartCoroutine(Run(slot, variant, skill, _ctx.Snapshot()));
            return true;
        }

        IEnumerator Run(int slot, int variant, Skill skill, SkillContext ctx)
        {
            _running = skill;
            Used?.Invoke(slot, variant, skill);
            yield return skill.Run(ctx);
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

        // Bị tắt (người chơi gục, rời màn): huỷ chiêu đang chạy, đòn đang xếp hàng và mọi phần kéo dài (coroutine của
        // MonoBehaviour bị tắt vẫn chạy tiếp nếu không dừng), để người chơi đã gục không còn gây sát thương
        void OnDisable()
        {
            StopAllCoroutines();
            _running = null;
            _queued = 0;
            _heldKey = -1;
            // Laze chạy máy trạng thái riêng (LaserSkill): tắt luôn, kẻo tia vẫn bắn ra từ cái xác đã ẩn
            var laser = GetComponent<LaserSkill>();
            if (laser != null)
                laser.Cancel();
        }

        /// <summary>Bật trạng thái key (dùng chung giữa các chiêu) trong seconds giây với giá trị value; seconds ≤ 0 = tắt.</summary>
        public void SetBuff(string key, float seconds, float value = 1f)
        {
            if (seconds <= 0f || value == 0f)
                _buffs.Remove(key);
            else
                _buffs[key] = (Time.time + seconds, value);
        }

        /// <summary>Giá trị trạng thái key còn hiệu lực; 0 nếu đã hết hoặc chưa bật.</summary>
        public float Buff(string key) => _buffs.TryGetValue(key, out var b) && Time.time < b.until ? b.value : 0f;

        /// <summary>Số giây trạng thái key còn lại.</summary>
        public float BuffRemaining(string key) => _buffs.TryGetValue(key, out var b) ? Mathf.Max(0f, b.until - Time.time) : 0f;

        void Update()
        {
            for (int i = 0; i < _cooldown.Length; i++)
                _cooldown[i] = Mathf.Max(0f, _cooldown[i] - Time.deltaTime);
            ReadKeyboard();
        }

        // Giữ một phím chiêu (J / K / L) rồi bấm phím chiêu khác = vuốt từ nút này sang nút kia; nhả mà chưa bấm phím nào khác = chạm
        void ReadKeyboard()
        {
            if (_input == null || !_input.isActiveAndEnabled)
                return;

            if (_input.RageCheatPressed)
                _rage.Add(RageMeter.PerTier);

            // Nhân vật người: bấm J / K / L là ra chiêu ngay, kiểu theo W / S (cần điều hướng) đang giữ
            if (IsHero)
            {
                for (int slot = 0; slot < Slots; slot++)
                {
                    if (_input.SkillKeyDown(slot))
                        Use(slot, HeroMotor.VariantOf(_input.Aim));
                }

                _heldKey = -1;
                return;
            }

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

        /// <summary>Bộ 9 chiêu mặc định của một nhân vật (Editor dựng màn chơi gọi).</summary>
        public void SetDefaultSkills(PlayableCharacter character, Skill[] skills)
        {
            skills ??= Array.Empty<Skill>();
            switch (character)
            {
                case PlayableCharacter.Spider: spiderSkills = skills; break;
                case PlayableCharacter.Red: redSkills = skills; break;
                case PlayableCharacter.Pink: pinkSkills = skills; break;
                case PlayableCharacter.Blue: blueSkills = skills; break;
                default: octopusSkills = skills; break;
            }
        }
    }
}
