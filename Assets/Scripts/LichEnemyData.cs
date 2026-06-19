using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData_Lich", menuName = "RogueSliders/Enemies/Lich Enemy Data")]
public class LichEnemyData : EnemyData
{
    [Header("Summoning")]
    [SerializeField] private LichSkullObject lichSkullObjectPrefab;
    [SerializeField] private List<GameObject> summonableEnemyPrefabs = new List<GameObject>();
    [Min(1)]
    [SerializeField] private int summonedSkullCount = 3;
    [Min(1)]
    [SerializeField] private int summonIntervalTurns = 3;
    [Min(0f)]
    [SerializeField] private float summonDelayBeforeSkulls = 0.15f;
    [Min(0f)]
    [SerializeField] private float delayBetweenSummonedSkulls = 0.1f;

    public LichSkullObject LichSkullObjectPrefab => lichSkullObjectPrefab;
    public IReadOnlyList<GameObject> SummonableEnemyPrefabs => summonableEnemyPrefabs;
    public int SummonedSkullCount => Mathf.Max(1, summonedSkullCount);
    public int SummonIntervalTurns => Mathf.Max(1, summonIntervalTurns);
    public float SummonDelayBeforeSkulls => Mathf.Max(0f, summonDelayBeforeSkulls);
    public float DelayBetweenSummonedSkulls => Mathf.Max(0f, delayBetweenSummonedSkulls);
}
