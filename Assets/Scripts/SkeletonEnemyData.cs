using UnityEngine;

[CreateAssetMenu(fileName = "SkeletonEnemyData", menuName = "RogueSliders/Enemies/Skeleton Enemy Data")]
public class SkeletonEnemyData : EnemyData
{
    [Header("Revive")]
    [SerializeField] private SkullObject skullObjectPrefab;
    [Min(1)]
    [SerializeField] private int reviveTurns = 3;

    public SkullObject SkullObjectPrefab => skullObjectPrefab;
    public int ReviveTurns => Mathf.Max(1, reviveTurns);
}
