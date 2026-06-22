using UnityEngine;

[CreateAssetMenu(fileName = "SkeletonEnemyData", menuName = "RogueSliders/Enemies/Skeleton Enemy Data")]
public class SkeletonEnemyData : EnemyData
{
    [Header("Revive")]
    [SerializeField] private bool canSpawnSkullOnDeath = true;
    [SerializeField] private SkullObject skullObjectPrefab;
    [Min(1)]
    [SerializeField] private int reviveTurns = 3;

    public bool CanSpawnSkullOnDeath => canSpawnSkullOnDeath;
    public SkullObject SkullObjectPrefab => skullObjectPrefab;
    public int ReviveTurns => Mathf.Max(1, reviveTurns);
}
