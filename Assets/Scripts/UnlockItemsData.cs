using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UnlockItemsData", menuName = "RogueSliders/Tourments/Unlock Items Data")]
public class UnlockItemsData : ScriptableObject
{
    [SerializeField] private List<TourmentRewardUnlockDefinition> rewardUnlocks = new List<TourmentRewardUnlockDefinition>();

    public IReadOnlyList<TourmentRewardUnlockDefinition> RewardUnlocks => rewardUnlocks;
}
