using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BiomeEnemyPoolAvailability
{
    [SerializeField] private List<EnemyPoolDefinition> pools = new List<EnemyPoolDefinition>();
    [Min(1)]
    [SerializeField] private int minArenaCount = 1;
    [Min(1)]
    [SerializeField] private int maxArenaCount = 1;

    public IReadOnlyList<EnemyPoolDefinition> Pools => pools;
    public int MinArenaCount => minArenaCount;
    public int MaxArenaCount => Mathf.Max(minArenaCount, maxArenaCount);

    public bool MatchesArenaCount(int arenaCount)
    {
        return arenaCount >= MinArenaCount && arenaCount <= MaxArenaCount;
    }
}

[CreateAssetMenu(fileName = "BiomeData", menuName = "RogueSliders/Biomes/Biome Data")]
public class BiomeData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string biomeName;

    [Header("Arena Visuals")]
    [SerializeField] private Color groundColor = Color.white;
    [SerializeField] private Color cliffColor = Color.white;
    [SerializeField] private Color rockColor = Color.white;
    [SerializeField] private Sprite backgroundDecorSprite;
    [SerializeField] private AudioClip combatMusic;

    [Header("Arena Layouts")]
    [SerializeField] private List<Texture2D> arenaLayouts = new List<Texture2D>();

    [Header("Arena Obstacles")]
    [SerializeField] private List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("Enemy Pools")]
    [SerializeField] private List<BiomeEnemyPoolAvailability> enemyPoolsByArenaCount = new List<BiomeEnemyPoolAvailability>();

    [Header("Special Encounters")]
    [SerializeField] private List<GameObject> spawnableEnemies = new List<GameObject>();

    public string BiomeName => string.IsNullOrWhiteSpace(biomeName) ? name : biomeName;
    public Color GroundColor => groundColor;
    public Color CliffColor => cliffColor;
    public Color RockColor => rockColor;
    public Sprite BackgroundDecorSprite => backgroundDecorSprite;
    public AudioClip CombatMusic => combatMusic;
    public IReadOnlyList<Texture2D> ArenaLayouts => arenaLayouts;
    public IReadOnlyList<GameObject> ObstaclePrefabs => obstaclePrefabs;
    public IReadOnlyList<BiomeEnemyPoolAvailability> EnemyPoolsByArenaCount => enemyPoolsByArenaCount;
    public IReadOnlyList<GameObject> SpawnableEnemies => spawnableEnemies;
}
