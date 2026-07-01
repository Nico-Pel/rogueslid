public enum TourmentUnlockResultKind
{
    Tourment,
    Ability,
    Item
}

public sealed class TourmentUnlockResult
{
    public TourmentUnlockResultKind Kind;
    public int TourmentLevel;
    public RewardDefinition RewardDefinition;
    public CharacterData CharacterData;
}
