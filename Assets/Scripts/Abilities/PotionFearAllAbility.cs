using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Potion Fear All", fileName = "PotionFearAll")]
public class PotionFearAllAbility : AbilityDefinition
{
    public override bool IsBonusAbility => true;
    public override bool IsConsumableBonusAbility => true;
    public override AbilityButtonTypeIconKind GetButtonTypeIconKind(Character character, CharacterAbilityRuntime runtime) => AbilityButtonTypeIconKind.Potion;

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        character?.Board?.ApplyFearToAllEnemies(1);
        PlayDrinkSound();
        return character != null;
    }

    private void PlayDrinkSound()
    {
        SoundManager.Instance?.Play2DSound(GetPotionDrinkClip(), 1f, 1f);
    }

    private static AudioClip GetPotionDrinkClip()
    {
        AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
        for (int index = 0; index < clips.Length; index++)
        {
            AudioClip clip = clips[index];
            if (clip != null && clip.name == "PotionDrink")
            {
                return clip;
            }
        }

        return null;
    }
}
