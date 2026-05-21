namespace MapleClaude.Domain;

/// <summary>
/// Per-session account state populated by <c>CheckPasswordResult(0)</c>.
/// The 8-byte <see cref="ClientKey"/> is echoed back to the channel server in
/// <c>MigrateIn(20)</c> to bind the new session to the original login.
/// </summary>
public sealed class Account
{
    public int AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public byte Gender { get; set; }
    public byte GradeCode { get; set; }
    public short SubGradeCode { get; set; }
    public byte CountryId { get; set; }
    public string NexonClubId { get; set; } = string.Empty;
    public byte PurchaseExp { get; set; }
    public byte ChatBlockReason { get; set; }
    public long ChatUnblockDate { get; set; }
    public long RegisterDate { get; set; }
    public int CharacterSlotCount { get; set; }
    public bool SkipPinCode { get; set; }
    public byte LoginOpt { get; set; }
    public byte[] ClientKey { get; set; } = new byte[8];

    /// <summary>Server-assigned world index, set when SelectWorld succeeds.</summary>
    public byte SelectedWorldId { get; set; }
    /// <summary>Server-assigned channel index, set when SelectWorld succeeds.</summary>
    public byte SelectedChannelId { get; set; }
}
