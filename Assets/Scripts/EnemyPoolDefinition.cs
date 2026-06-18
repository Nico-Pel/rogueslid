using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Enemies/Enemy Pool", fileName = "EnemyPool")]
public class EnemyPoolDefinition : ScriptableObject
{
    [SerializeField] private Texture2D arenaLayoutOverride;
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();

    public Texture2D ArenaLayoutOverride => arenaLayoutOverride;
    public IReadOnlyList<GameObject> EnemyPrefabs => enemyPrefabs;
    public int EnemyCount => enemyPrefabs != null ? enemyPrefabs.Count : 0;

    public List<GameObject> GetValidEnemyPrefabs()
    {
        List<GameObject> validEnemyPrefabs = new List<GameObject>();
        if (enemyPrefabs == null)
        {
            return validEnemyPrefabs;
        }

        for (int index = 0; index < enemyPrefabs.Count; index++)
        {
            if (enemyPrefabs[index] != null)
            {
                validEnemyPrefabs.Add(enemyPrefabs[index]);
            }
        }

        return validEnemyPrefabs;
    }
}
