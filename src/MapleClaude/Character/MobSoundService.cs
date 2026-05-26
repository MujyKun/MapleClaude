using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Character;

/// <summary>
/// Plays mob sound effects from <c>Sound.wz/Mob.img/&lt;id:D7&gt;/&lt;event&gt;</c>.
///
/// <para>Authoritative v95 client mapping (via the IDB — <c>play_mob_sound</c> +
/// <c>CSoundMan::PlaySE</c>):</para>
/// <list type="bullet">
///  <item><c>Damage</c> — fired on every observer when the mob takes damage. We trigger
///        locally on our melee swings (<c>DoMeleeAttack</c>) and on inbound
///        <c>MobDamaged</c> broadcasts (other players' attacks).</item>
///  <item><c>Die</c> — fired on death-anim start. We trigger from
///        <c>OnMobHpIndicator(pct=0)</c> or <c>OnMobLeave(leaveType=2)</c>; per-mob
///        dedup is done by the caller via <see cref="MobLook.IsDying"/>.</item>
///  <item><c>Attack1</c>..<c>Attack8</c>, <c>AttackF</c> (index &gt;= 8) — fired by the
///        controller per attack tick.</item>
///  <item><c>Skill1</c>..<c>Skill16</c>, <c>SkillF</c> — fired per mob-skill cast.</item>
///  <item><c>CharDam1</c>, <c>CharDam2</c>, <c>CharDamF</c> — fired when the mob is hit
///        by a SKILL (not a basic attack); distinguished by the player's nHitAction.</item>
///  <item><c>Regen</c> — fired ONLY on respawn (<c>nAppearType == -4</c>), NOT initial
///        spawn.</item>
///  <item><c>Bomb</c> — fired on time-bomb-mob detonation.</item>
/// </list>
///
/// <para>WZ path format: <c>Sound/Mob.img/%07d/%s</c> (StringPool id 2237). The id is
/// always zero-padded to 7 digits; <c>info/link</c> redirects in Mob.wz do NOT apply
/// to sounds (real client uses the original mob id for the sound lookup, per the IDB).
/// </para>
///
/// <para>Missing-sound behavior matches the real client: silent skip, no fallback, no
/// log. We cache the resolved <see cref="WzSound"/> (or null) per (templateId, event)
/// so repeat hits don't re-walk the WZ tree.</para>
/// </summary>
public sealed class MobSoundService
{
    private readonly WzPackage?     _soundWz;
    private readonly WzAudioPlayer? _audio;
    private readonly ILogger?       _logger;
    private readonly Dictionary<(int templateId, string evt), WzSound?> _cache = new();

    public MobSoundService(WzPackage? soundWz, WzAudioPlayer? audio, ILogger? logger = null)
    {
        _soundWz = soundWz;
        _audio   = audio;
        _logger  = logger;
    }

    /// <summary>Damage sound when the mob takes a hit (basic attack or contact).</summary>
    public void PlayDamage(int templateId) => Play(templateId, "Damage");

    /// <summary>Death sound, fired on death-anim start. Caller should gate via
    /// <see cref="MobLook.IsDying"/> to avoid double-play when both MobHPIndicator(0%)
    /// and MobLeaveField(type=2) arrive.</summary>
    public void PlayDie(int templateId) => Play(templateId, "Die");

    /// <summary>Per-attack sound. <paramref name="attackIndex"/> is 0-based
    /// (<c>attack1</c> in Mob.wz = index 0 = node "Attack1"). Indices &gt;= 8 fall back
    /// to "AttackF".</summary>
    public void PlayAttack(int templateId, int attackIndex)
        => Play(templateId, attackIndex < 8 ? $"Attack{attackIndex + 1}" : "AttackF");

    /// <summary>Per-skill sound for mob skill casts. <paramref name="skillIndex"/> is
    /// 0-based (skill 0 = node "Skill1"). Indices &gt;= 16 fall back to "SkillF".</summary>
    public void PlaySkill(int templateId, int skillIndex)
        => Play(templateId, skillIndex < 16 ? $"Skill{skillIndex + 1}" : "SkillF");

    /// <summary>Sound played when the mob is hit by a SKILL (not basic attack).
    /// <paramref name="index"/> 0 -&gt; CharDam1, 1 -&gt; CharDam2, anything else -&gt;
    /// CharDamF.</summary>
    public void PlayCharDamage(int templateId, int index)
        => Play(templateId, index switch { 0 => "CharDam1", 1 => "CharDam2", _ => "CharDamF" });

    /// <summary>Respawn (regen) sound — fired only when the mob re-appears, not on
    /// initial spawn (matches the v95 client's <c>nAppearType == -4</c> gate).</summary>
    public void PlayRegen(int templateId) => Play(templateId, "Regen");

    /// <summary>Time-bomb mob detonation sound.</summary>
    public void PlayBomb(int templateId) => Play(templateId, "Bomb");

    private void Play(int templateId, string evt)
    {
        if (_audio is null || _soundWz is null) return;
        var key = (templateId, evt);
        if (!_cache.TryGetValue(key, out var sound))
        {
            // v95 client path (StringPool id 2237): Sound/Mob.img/%07d/%s
            // Many entries are UOL symlinks to a shared base sound (e.g. every
            // Orange Mushroom variant's Damage links to 0100100/Damage); resolve
            // through the link so the cast doesn't silently drop them. ~54% of
            // the 1.5k Damage nodes in v95 Sound.wz are UOLs.
            var raw = _soundWz.GetItem($"Mob.img/{templateId:D7}/{evt}");
            sound = raw switch
            {
                WzSound s => s,
                WzUol u   => u.Resolve() as WzSound,
                _         => null,
            };
            _cache[key] = sound;   // cache misses too so we don't re-walk WZ
            if (sound is null) _logger?.LogTrace("Mob sound missing: {Id}/{Evt}", templateId, evt);
        }
        if (sound is not null) _audio.PlayEffect(sound);
    }
}
