using System.Globalization;
using MapleClaude.Wz;

namespace MapleClaude.Character;

/// <summary>
/// Lazily parses + caches one <see cref="MobInfo"/> per Mob.wz template id. Mirrors the
/// FULL upstream Kinoko <c>MobTemplate.from()</c> parse (so we agree with the server on
/// every combat-relevant field) PLUS the v95 client-side fields the server doesn't track
/// (movement / display / control flags). Resolves the <c>info/link</c> redirect so
/// variant recolours inherit the canonical template's data.
/// </summary>
public sealed class MobInfoService
{
    private readonly WzPackage? _mobWz;
    private readonly Dictionary<int, MobInfo> _cache = new();

    public MobInfoService(WzPackage? mobWz) { _mobWz = mobWz; }

    /// <summary>Returns the (cached) info for a template id. Never null - falls back to a
    /// defaulted <see cref="MobInfo"/> (walk, no aggro, empty collections) when the
    /// template is missing, so the controller can still drive a generic walker without
    /// throwing.</summary>
    public MobInfo Get(int templateId)
    {
        if (_cache.TryGetValue(templateId, out var hit)) return hit;
        var info = Parse(templateId) ?? new MobInfo { TemplateId = templateId };
        _cache[templateId] = info;
        return info;
    }

    private MobInfo? Parse(int templateId)
    {
        if (_mobWz is null) return null;
        if (_mobWz.GetItem($"{templateId:D7}.img") is not WzImage img) return null;
        var root = img.Root;

        // info/link redirect: variants point to a canonical template's animations + info.
        if (root.GetItem("info/link") is string link && int.TryParse(link, out var linkId)
            && _mobWz.GetItem($"{linkId:D7}.img") is WzImage linked)
        {
            root = linked.Root;
        }

        if (root.GetItem("info") is not WzProperty info) return null;

        // Fly speed: info/flySpeed OR info/fs (fs wins if both are present).
        var flySpeed = ReadInt(info, "flySpeed");
        if (flySpeed == 0) flySpeed = ReadInt(info, "fs");

        // -- Per-attack collection (sibling nodes of `info` under the mob root) -----
        // Keys are attack1..attack9 / attackA..attackF (hex digit), so parse as hex.
        var attacks = new Dictionary<int, MobAttack>();
        foreach (var (key, value) in root.Items)
        {
            if (!key.StartsWith("attack")) continue;
            var idxStr = key.Substring("attack".Length);
            if (idxStr.Length == 0) continue;
            if (!int.TryParse(idxStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var attackNumber)) continue;
            if (value is not WzProperty attackProp) continue;
            if (attackProp.GetItem("info") is not WzProperty atkInfo) continue;
            var attackIndex = attackNumber - 1;   // attack1 -> index 0
            attacks[attackIndex] = new MobAttack
            {
                AttackIndex   = attackIndex,
                SkillId       = ReadInt(atkInfo, "disease"),
                SkillLevel    = ReadInt(atkInfo, "level"),
                ConMp         = ReadInt(atkInfo, "conMP"),
                MpBurn        = ReadInt(atkInfo, "mpBurn"),
                Magic         = ReadBool(atkInfo, "magic"),
                DeadlyAttack  = ReadBool(atkInfo, "deadlyAttack"),
                BulletNumber  = Math.Max(1, ReadInt(atkInfo, "bulletNumber", 1)),
                BulletSpeed   = ReadInt(atkInfo, "bulletSpeed"),
                MagicElemAttr = ReadInt(atkInfo, "magicElemAttr"),
                JumpAttack    = ReadBool(atkInfo, "jump"),
                KnockBack     = ReadBool(atkInfo, "knockBack"),
                AttackAfter   = ReadInt(atkInfo, "attackAfter"),
                EffectAfter   = ReadInt(atkInfo, "effectAfter"),
                Tremble       = ReadBool(atkInfo, "tremble"),
                Rush          = ReadBool(atkInfo, "rush"),
                HitAttach     = ReadBool(atkInfo, "hitAttach"),
                FacingAttach  = ReadBool(atkInfo, "facingAttach"),
                Effect        = ReadStr(atkInfo, "effect"),
                Hit           = ReadStr(atkInfo, "hit"),
                Ball          = ReadStr(atkInfo, "ball"),
                AreaWarning   = ReadStr(atkInfo, "areaWarning"),
            };
        }

        // -- Mob skill list (info/skill/<n>/{skill,level}) -------------------------
        var skills = new Dictionary<int, MobSkillRef>();
        if (info.GetItem("skill") is WzProperty skillRoot)
        {
            foreach (var (_, sv) in skillRoot.Items)
            {
                if (sv is not WzProperty sp) continue;
                var skId = ReadInt(sp, "skill");
                var slv  = ReadInt(sp, "level");
                if (skId == 0) continue;
                var type = System.Enum.IsDefined(typeof(MobSkillType), skId)
                    ? (MobSkillType)skId
                    : MobSkillType.Unknown;
                skills[skId] = new MobSkillRef(type, skId, slv);
            }
        }

        // -- Elemental resistances ("P1F2I3" -> { 'P':'1', 'F':'2', 'I':'3' }) ----
        var elemAttr = new Dictionary<char, char>();
        if (info.Get("elemAttr") is string ea && ea.Length >= 2)
        {
            for (var i = 0; i + 1 < ea.Length; i += 2)
            {
                elemAttr[ea[i]] = ea[i + 1];
            }
        }

        // -- Skill-immune list (only these skills can damage the mob; empty = all) -
        var damagedBySkill = new HashSet<int>();
        if (info.GetItem("damagedBySelectedSkill") is WzProperty dbs)
        {
            foreach (var (_, v) in dbs.Items)
            {
                var id = v switch { int i => i, short s => s, long l => (int)l, _ => 0 };
                if (id != 0) damagedBySkill.Add(id);
            }
        }

        // -- Revive list (mob ids spawned in this mob's place on death) -----------
        var revives = new List<int>();
        if (info.GetItem("revive") is WzProperty rv)
        {
            foreach (var (_, v) in rv.Items)
            {
                var id = v switch { int i => i, short s => s, long l => (int)l, _ => 0 };
                if (id != 0) revives.Add(id);
            }
        }

        return new MobInfo
        {
            TemplateId           = templateId,
            // Identity
            Level                = ReadInt(info, "level", 1),
            Exp                  = ReadInt(info, "exp"),
            // Stats
            MaxHp                = Math.Max(1, ReadInt(info, "maxHP", 1)),
            MaxMp                = ReadInt(info, "maxMP"),
            Pad                  = ReadInt(info, "PADamage"),
            Pdr                  = ReadInt(info, "PDRate"),
            Mad                  = ReadInt(info, "MADamage"),
            Mdr                  = ReadInt(info, "MDRate"),
            Acc                  = ReadInt(info, "acc"),
            Eva                  = ReadInt(info, "eva"),
            HpRecovery           = ReadInt(info, "hpRecovery"),
            MpRecovery           = ReadInt(info, "mpRecovery"),
            FixedDamage          = ReadInt(info, "fixedDamage"),
            // Lifecycle
            RemoveAfter          = ReadInt(info, "removeAfter"),
            DropItemPeriod       = ReadInt(info, "dropItemPeriod"),
            // Movement
            MoveAbility          = ReadInt(info, "moveAbility", 1),
            Speed                = ReadInt(info, "speed"),
            FlySpeed             = flySpeed,
            Fly                  = ReadBool(info, "fly"),
            ChaseSpeed           = ReadInt(info, "chaseSpeed"),
            // Combat
            BodyAttack           = ReadBool(info, "bodyAttack"),
            Pushed               = ReadInt(info, "pushed"),
            Undead               = ReadBool(info, "undead"),
            Boss                 = ReadBool(info, "boss"),
            NoFlip               = ReadBool(info, "noFlip"),
            OnlyNormalAttack     = ReadBool(info, "onlyNormalAttack"),
            DamagedByMob         = ReadBool(info, "damagedByMob"),
            PickUp               = ReadBool(info, "pickUp"),
            CannotEvade          = ReadBool(info, "cannotEvade"),
            SelfDestruction      = ReadBool(info, "selfDestruction"),
            FirstSelfDestruction = ReadBool(info, "firstSelfDestruction"),
            Invincible           = ReadBool(info, "invincible"),
            Disable              = ReadBool(info, "disable"),
            NotAttack            = ReadBool(info, "notAttack"),
            FirstAttack          = ReadBool(info, "firstAttack"),
            // Display
            HpTagColor           = ReadInt(info, "hpTagColor"),
            HpTagBgColor         = ReadInt(info, "hpTagBgcolor"),
            HpGaugeHide          = ReadBool(info, "hpTagHide"),
            UpperMostLayer       = ReadBool(info, "upperMostLayer"),
            WeaponID             = ReadInt(info, "weaponID"),
            AngerGauge           = ReadBool(info, "angerGauge"),
            ChargeCount          = ReadInt(info, "chargeCount"),
            // Categorization
            Category             = ReadInt(info, "category"),
            EscortType           = ReadInt(info, "escortType"),
            MobSpeciesCode       = ReadStr(info, "mobSpeciesCode"),
            // Collections
            Attacks              = attacks,
            Skills               = skills,
            DamagedElemAttr      = elemAttr,
            DamagedBySkill       = damagedBySkill,
            Revives              = revives,
        };
    }

    private static int ReadInt(WzProperty p, string key, int def = 0) => p.Get(key) switch
    {
        int i   => i,
        short s => s,
        long l  => (int)l,
        _       => def,
    };

    private static bool ReadBool(WzProperty p, string key) => ReadInt(p, key) != 0;

    private static string ReadStr(WzProperty p, string key) => p.Get(key) switch
    {
        string s => s,
        _        => string.Empty,
    };
}