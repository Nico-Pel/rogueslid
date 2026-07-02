using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Potion Heal", fileName = "PotionHeal")]
public class PotionHealAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int healAmount = 5;

    public override bool IsBonusAbility => true;
    public override bool IsConsumableBonusAbility => true;
    public override AbilityButtonTypeIconKind GetButtonTypeIconKind(Character character, CharacterAbilityRuntime runtime) => AbilityButtonTypeIconKind.Potion;

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null)
        {
            return false;
        }

        character.Heal(healAmount, null, true);
        PlayPotionDrink();
        return true;
    }

    private static void PlayPotionDrink()
    {
        AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
        for (int index = 0; index < clips.Length; index++)
        {
            if (clips[index] != null && clips[index].name == "PotionDrink")
            {
                SoundManager.Instance?.Play2DSound(clips[index], 1f, 1f);
                break;
            }
        }
    }
}
