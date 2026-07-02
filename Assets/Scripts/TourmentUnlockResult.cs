public enum TourmentUnlockResultKind
{
    Tourment,
    Character,
    Ability,
    Item
}

public sealed class TourmentUnlockResult
{
    public TourmentUnlockResultKind Kind;
    public int TourmentLevel;
    public RewardDefinition RewardDefinition;
    public CharacterData CharacterData;
    public CharacterData UnlockedCharacterData;
}
