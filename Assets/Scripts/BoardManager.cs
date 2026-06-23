using System;
using System.Collections.Generic;
using UnityEngine;

public enum BoardCellType
{
    Classic,
    Rock,
    Brake
}

public enum BoardOccupantKind
{
    None,
    PlayerCharacter,
    Enemy,
    Obstacle,
    Object,
    Other
}

[System.Serializable]
public class BoardCell
{
    public Vector2Int GridPosition;
    public Vector3 WorldPosition;
    public BoardCellType Type = BoardCellType.Classic;
    public bool Walkable = true;
    public bool IsOccupied;
    public BoardOccupantKind OccupantKind = BoardOccupantKind.None;
    public GameObject Occupant;
    public GameObject StaticObstacle;
    public bool StaticObstacleBlocksMovement;
    public bool StopsCharacterSlide;
    public BoardHazard Hazard;

    public bool HasBlockingTerrain => !Walkable || Type == BoardCellType.Rock || (StaticObstacle != null && StaticObstacleBlocksMovement);
    public bool HasVisibleHazardForEnemies => Hazard != null && Hazard.IsVisibleToEnemies;

    public void SetOccupant(GameObject occupant, BoardOccupantKind occupantKind)
    {
        Occupant = occupant;
        OccupantKind = occupantKind;
        IsOccupied = occupant != null;
    }

    public void ClearOccupant()
    {
        Occupant = null;
        OccupantKind = BoardOccupantKind.None;
        IsOccupied = false;
    }

    public void SetStaticObstacle(GameObject obstacle, BoardCellType cellType = BoardCellType.Rock)
    {
        StaticObstacle = obstacle;
        StaticObstacleBlocksMovement = true;
        StopsCharacterSlide = false;
        Type = cellType;
        Walkable = false;
    }

    public void SetTraversalStopSurface(GameObject obstacle)
    {
        StaticObstacle = obstacle;
        StaticObstacleBlocksMovement = false;
        StopsCharacterSlide = true;
        Type = BoardCellType.Classic;
        Walkable = true;
    }

    public void SetHazard(BoardHazard hazard)
    {
        Hazard = hazard;
    }

    public void ClearHazard(BoardHazard hazard = null)
    {
        if (hazard != null && Hazard != hazard)
        {
            return;
        }

        Hazard = null;
    }
}

public class BoardManager : MonoBehaviour
{
    private const int BoardWidth = 7;
    private const int BoardHeight = 10;

    [Header("Board")]
    [SerializeField] [ReadOnly] private int arenaCount = 1;
    [SerializeField] [ReadOnly] private int currentBiomeIndex;
    [SerializeField] private List<BiomeData> biomes = new List<BiomeData>();
    [SerializeField] private MeshRenderer boardModelRenderer;
    [SerializeField] private MeshRenderer gridRenderer;
    [SerializeField] private SpriteRenderer backgroundDecorsRenderer;

    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private float cellSizeX = 1f;
    [SerializeField] private float cellSizeZ = 1f;
    [SerializeField] private int originColumn = 3;
    [SerializeField] private int originRow = 4;
    [SerializeField] private float spawnHeight = 0.08f;
    [SerializeField] private float optionalObstacleChance = 0.33f;
    [SerializeField] private bool allowMirrorOnYAxis = true;
    [SerializeField] [Range(0f, 1f)] private float mirrorOnYAxisChance = 0.5f;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerCharacterPrefab;
#if UNITY_EDITOR
    [Header("Editor Debug")]
    [SerializeField] private bool useForcedDatas;
    [SerializeField] private BiomeData forcedBiome;
    [SerializeField] private EnemyPoolDefinition forcedEnemyPoolDefinition;
    [Min(1)]
    [SerializeField] private int cheatRewardsCount = 1;
#endif

    [Header("Rewards")]
    [SerializeField] private List<ItemRewardDefinition> itemRewardDefinitions = new List<ItemRewardDefinition>();

    [Header("Special Encounters")]
    [SerializeField] private GameObject defaultEnemySpawnFxPrefab;
    [SerializeField] private float defaultEnemySpawnFxLifetime = 2f;
    [SerializeField] private float extraEnemySpawnDelay = 1f;

    private BoardCell[,] cells;
    private Transform generatedRoot;
    private Transform obstaclesRoot;
    private Transform enemiesRoot;
    private Transform playersRoot;
    private Player player;
    private readonly List<Enemy> spawnedEnemies = new List<Enemy>();
    private Texture2D currentArenaLayout;
    private bool currentArenaMirroredOnYAxis;
    private bool hasGeneratedBoardThisSession;
    private PlayerRunRewardState runRewardState = new PlayerRunRewardState();
    private readonly List<Vector2Int> currentEnemySpawnCells = new List<Vector2Int>();
    private readonly List<GameObject> currentArenaEnemyPrefabs = new List<GameObject>();
    private bool bonusRewardForCurrentArena;
    private readonly List<BoardHazard> spawnedHazards = new List<BoardHazard>();
    private readonly List<SkullObject> activeSkullObjects = new List<SkullObject>();
    private readonly Dictionary<Enemy, HashSet<Enemy>> linkedSummonsByOwner = new Dictionary<Enemy, HashSet<Enemy>>();
    private readonly Dictionary<Enemy, Enemy> summonOwnerByMinion = new Dictionary<Enemy, Enemy>();
    private readonly HashSet<EnemyPoolDefinition> encounteredEnemyPools = new HashSet<EnemyPoolDefinition>();
    private EnemyPoolDefinition currentSelectedEnemyPool;
    private bool isResolvingSkullsForVictory;
    private static readonly int ColorShaderProperty = Shader.PropertyToID("_Color");
    public int Width => BoardWidth;
    public int Height => BoardHeight;
    public BoardCell[,] Cells => cells;
    public Player Player => player;
    public IReadOnlyList<Enemy> SpawnedEnemies => spawnedEnemies;
    public int ArenaCount => arenaCount;
    public int CurrentBiomeIndex => currentBiomeIndex;
    public BiomeData CurrentBiome => GetCurrentBiomeData();
    public AudioClip CurrentCombatMusic => GetCurrentBiomeData() != null ? GetCurrentBiomeData().CombatMusic : null;
    public PlayerRunRewardState RunRewardState => runRewardState;
    public float ExtraEnemySpawnDelay => Mathf.Max(0f, extraEnemySpawnDelay);
    public GameObject DefaultSpawnFxPrefab => defaultEnemySpawnFxPrefab;
    public float DefaultSpawnFxLifetime => Mathf.Max(0f, defaultEnemySpawnFxLifetime);
    public event Action AllEnemiesDefeated;

#if UNITY_EDITOR
    public int CheatRewardsCount
    {
        get => Mathf.Max(1, cheatRewardsCount);
        set => cheatRewardsCount = Mathf.Max(1, value);
    }

    public void RequestCheatRewardsFromInspector()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        GameTurnManager turnManager = GetComponent<GameTurnManager>();
        turnManager?.RequestDebugRewardChoices(CheatRewardsCount);
    }
#endif

    private enum ArenaMarker
    {
        Empty,
        Obstacle,
        OptionalObstacle,
        EnemySpawn,
        PlayerSpawn
    }

    private void Awake()
    {
        if (generateOnStart)
        {
            ResetArenaProgression();
            GenerateBoard();
        }
    }

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        ApplyCurrentBiomeVisuals();
        currentSelectedEnemyPool = ResolveSelectedEnemyPoolForCurrentArena();
        Texture2D selectedArenaLayout = SelectArenaLayout(currentSelectedEnemyPool);
        if (selectedArenaLayout == null)
        {
            Debug.LogError("BoardManager requires at least one arena layout texture.", this);
            return;
        }

        if (!selectedArenaLayout.isReadable)
        {
            Debug.LogError("Arena texture must be readable. Enable Read/Write on the imported texture.", selectedArenaLayout);
            return;
        }

        currentArenaLayout = selectedArenaLayout;
        currentArenaMirroredOnYAxis = allowMirrorOnYAxis && UnityEngine.Random.value < mirrorOnYAxisChance;
        if (currentSelectedEnemyPool != null && currentSelectedEnemyPool.ArenaLayoutOverride != null)
        {
            currentArenaMirroredOnYAxis = false;
        }
        EnsureGeneratedRoots();
        ClearGeneratedContent();
        DestroyAllSpawnedHazards();
        InitializeCells();
        currentEnemySpawnCells.Clear();
        currentArenaEnemyPrefabs.Clear();
        bonusRewardForCurrentArena = false;
        spawnedHazards.Clear();
        activeSkullObjects.Clear();
        linkedSummonsByOwner.Clear();
        summonOwnerByMinion.Clear();
        isResolvingSkullsForVictory = false;

        List<Vector2Int> playerSpawnCandidates = new List<Vector2Int>();
        List<Vector2Int> enemySpawnCandidates = new List<Vector2Int>();

        for (int row = 0; row < BoardHeight; row++)
        {
            for (int column = 0; column < BoardWidth; column++)
            {
                BoardCell cell = cells[column, row];
                ArenaMarker marker = GetMarkerForCell(column, row, currentArenaLayout, currentArenaMirroredOnYAxis);

                switch (marker)
                {
                    case ArenaMarker.Obstacle:
                        SpawnObstacle(cell);
                        break;
                    case ArenaMarker.OptionalObstacle:
                        if (UnityEngine.Random.value < optionalObstacleChance)
                        {
                            SpawnObstacle(cell);
                        }
                        break;
                    case ArenaMarker.EnemySpawn:
                        enemySpawnCandidates.Add(cell.GridPosition);
                        break;
                    case ArenaMarker.PlayerSpawn:
                        playerSpawnCandidates.Add(cell.GridPosition);
                        break;
                }
            }
        }

        currentEnemySpawnCells.AddRange(enemySpawnCandidates);

        SpawnPlayerCharacter(playerSpawnCandidates);
        SpawnEnemies(enemySpawnCandidates);

        hasGeneratedBoardThisSession = true;

        if (Application.isPlaying)
        {
            GameTurnManager turnManager = GetComponent<GameTurnManager>();
            turnManager?.RestartForNewBoard();
        }
    }

    public void ResetArenaProgression()
    {
        currentBiomeIndex = 0;
        arenaCount = 1;
        hasGeneratedBoardThisSession = false;
        currentSelectedEnemyPool = null;
        activeSkullObjects.Clear();
        linkedSummonsByOwner.Clear();
        summonOwnerByMinion.Clear();
        isResolvingSkullsForVictory = false;
        encounteredEnemyPools.Clear();
        runRewardState = new PlayerRunRewardState();
        ApplyCurrentBiomeVisuals();
    }

    public void GenerateNextArena()
    {
        if (hasGeneratedBoardThisSession)
        {
            if (IsCurrentArenaBossBattle())
            {
                AdvanceToNextBiome();
            }
            else
            {
                arenaCount++;
            }
        }

        GenerateBoard();
    }

    public bool TryGetCell(Vector2Int gridPosition, out BoardCell cell)
    {
        if (cells == null)
        {
            cell = null;
            return false;
        }

        if (!IsInsideBoard(gridPosition))
        {
            cell = null;
            return false;
        }

        cell = cells[gridPosition.x, gridPosition.y];
        return true;
    }

    public Vector3 GridToWorldPosition(Vector2Int gridPosition)
    {
        float x = (gridPosition.x - originColumn) * cellSizeX;
        float z = (originRow - gridPosition.y) * cellSizeZ;
        return transform.TransformPoint(new Vector3(x, 0f, z));
    }

    public bool IsInsideBoard(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0 && gridPosition.x < BoardWidth && gridPosition.y >= 0 && gridPosition.y < BoardHeight;
    }

    public bool IsCellWalkable(Vector2Int gridPosition)
    {
        return TryGetCell(gridPosition, out BoardCell cell) && cell.Walkable && !cell.IsOccupied;
    }

    public bool IsCellAvailableForMovement(Vector2Int gridPosition, bool ignoreBlockingTerrain = false)
    {
        if (!TryGetCell(gridPosition, out BoardCell cell) || cell.IsOccupied)
        {
            return false;
        }

        return cell.Walkable || ignoreBlockingTerrain;
    }

    public Vector2Int GetSlideDestination(Vector2Int startPosition, Vector2Int direction)
    {
        return GetSlideDestination(startPosition, direction, false, null);
    }

    public Vector2Int GetSlideDestination(
        Vector2Int startPosition,
        Vector2Int direction,
        bool allowUnitTraversal,
        List<Enemy> traversedEnemies)
    {
        traversedEnemies?.Clear();
        List<Enemy> pendingTraversedEnemies = allowUnitTraversal ? new List<Enemy>() : null;

        if (direction == Vector2Int.zero)
        {
            return startPosition;
        }

        Vector2Int lastFreeCell = startPosition;
        Vector2Int next = startPosition + direction;

        while (TryGetCell(next, out BoardCell cell))
        {
            if (cell.HasBlockingTerrain)
            {
                if (allowUnitTraversal && IsTraversableGhostStepObstacle(cell))
                {
                    next += direction;
                    continue;
                }

                break;
            }

            if (cell.IsOccupied)
            {
                if (allowUnitTraversal
                    && cell.OccupantKind == BoardOccupantKind.Enemy
                    && TryGetEnemy(next, out Enemy traversedEnemy))
                {
                    pendingTraversedEnemies?.Add(traversedEnemy);
                    next += direction;
                    continue;
                }

                break;
            }

            if (pendingTraversedEnemies != null && pendingTraversedEnemies.Count > 0)
            {
                traversedEnemies?.AddRange(pendingTraversedEnemies);
                pendingTraversedEnemies.Clear();
            }

            lastFreeCell = next;
            if (cell.StopsCharacterSlide)
            {
                break;
            }

            next += direction;
        }

        return lastFreeCell;
    }

    private static bool IsTraversableGhostStepObstacle(BoardCell cell)
    {
        if (cell == null || cell.StaticObstacle == null)
        {
            return false;
        }

        SkullObject skullObject = cell.StaticObstacle.GetComponent<SkullObject>();
        return skullObject != null && !skullObject.IsResolving;
    }

    public bool MoveOccupant(Vector2Int from, Vector2Int to, BoardOccupantKind occupantKind, bool allowBlockedDestination = false)
    {
        if (!TryGetCell(from, out BoardCell fromCell) || !TryGetCell(to, out BoardCell toCell))
        {
            return false;
        }

        if (from == to || fromCell.Occupant == null || toCell.IsOccupied || (!toCell.Walkable && !allowBlockedDestination))
        {
            return false;
        }

        GameObject occupant = fromCell.Occupant;
        fromCell.ClearOccupant();
        toCell.SetOccupant(occupant, occupantKind);
        return true;
    }

    public int GetPathDistance(Vector2Int start, Vector2Int goal, bool allowOccupiedGoal = false, bool ignoreBlockingTerrain = false)
    {
        if (!IsInsideBoard(start) || !IsInsideBoard(goal))
        {
            return int.MaxValue;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        frontier.Enqueue(start);
        distances[start] = 0;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == goal)
            {
                return distances[current];
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (!IsInsideBoard(next) || distances.ContainsKey(next))
                {
                    continue;
                }

                bool canVisit = IsCellAvailableForMovement(next, ignoreBlockingTerrain) || (allowOccupiedGoal && next == goal);
                if (!canVisit)
                {
                    continue;
                }

                distances[next] = distances[current] + 1;
                frontier.Enqueue(next);
            }
        }

        return int.MaxValue;
    }

    public bool TryGetNextPathStep(Vector2Int start, Vector2Int goal, out Vector2Int nextStep, bool allowOccupiedGoal = false, bool ignoreBlockingTerrain = false)
    {
        nextStep = start;
        if (!IsInsideBoard(start) || !IsInsideBoard(goal) || start == goal)
        {
            return false;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == goal)
            {
                break;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (!IsInsideBoard(next) || cameFrom.ContainsKey(next))
                {
                    continue;
                }

                bool canVisit = IsCellAvailableForMovement(next, ignoreBlockingTerrain) || (allowOccupiedGoal && next == goal);
                if (!canVisit)
                {
                    continue;
                }

                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            return false;
        }

        Vector2Int currentStep = goal;
        while (cameFrom[currentStep] != start)
        {
            currentStep = cameFrom[currentStep];
        }

        nextStep = currentStep;
        return true;
    }

    public void RegisterHazard(BoardHazard hazard, Vector2Int gridPosition)
    {
        if (hazard == null || !TryGetCell(gridPosition, out BoardCell cell))
        {
            return;
        }

        cell.SetHazard(hazard);
        if (!spawnedHazards.Contains(hazard))
        {
            spawnedHazards.Add(hazard);
        }
    }

    public void UnregisterHazard(BoardHazard hazard)
    {
        if (hazard == null)
        {
            return;
        }

        if (TryGetCell(hazard.GridPosition, out BoardCell cell))
        {
            cell.ClearHazard(hazard);
        }

        spawnedHazards.Remove(hazard);
    }

    public bool TryGetHazard(Vector2Int gridPosition, out BoardHazard hazard)
    {
        hazard = null;
        if (!TryGetCell(gridPosition, out BoardCell cell))
        {
            return false;
        }

        hazard = cell.Hazard;
        return hazard != null;
    }

    public void HandlePlayerTurnStarted(Character playerCharacter)
    {
        if (activeSkullObjects.Count > 0)
        {
            List<SkullObject> skullSnapshot = new List<SkullObject>(activeSkullObjects);
            for (int index = 0; index < skullSnapshot.Count; index++)
            {
                SkullObject skullObject = skullSnapshot[index];
                if (skullObject == null)
                {
                    continue;
                }

                skullObject.HandlePlayerTurnStarted();
            }
        }

        if (spawnedHazards.Count == 0)
        {
            return;
        }

        List<BoardHazard> hazardsSnapshot = new List<BoardHazard>(spawnedHazards);
        for (int index = 0; index < hazardsSnapshot.Count; index++)
        {
            BoardHazard hazard = hazardsSnapshot[index];
            if (hazard == null)
            {
                continue;
            }

            hazard.HandlePlayerTurnStarted();
        }
    }

    public void NotifyEnemyEnteredCell(Enemy enemy)
    {
        if (enemy == null || !TryGetCell(enemy.GridPosition, out BoardCell cell))
        {
            return;
        }

        cell.Hazard?.HandleEnemyEntered(enemy);
    }

    public void NotifyCharacterTraversedPath(Character character, Vector2Int start, Vector2Int destination)
    {
        if (character == null || start == destination)
        {
            return;
        }

        Vector2Int delta = destination - start;
        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        bool isStraightPath = delta.x == 0 || delta.y == 0;
        if (!isStraightPath)
        {
            if (TryGetCell(destination, out BoardCell destinationCell))
            {
                destinationCell.Hazard?.HandleCharacterEntered(character);
            }

            return;
        }

        Vector2Int current = start + direction;
        while (true)
        {
            if (TryGetCell(current, out BoardCell cell))
            {
                cell.Hazard?.HandleCharacterEntered(character);
            }

            if (current == destination)
            {
                break;
            }

            current += direction;
        }
    }

    private void DestroyAllSpawnedHazards()
    {
        if (spawnedHazards.Count == 0)
        {
            return;
        }

        List<BoardHazard> hazardsSnapshot = new List<BoardHazard>(spawnedHazards);
        spawnedHazards.Clear();
        for (int index = 0; index < hazardsSnapshot.Count; index++)
        {
            BoardHazard hazard = hazardsSnapshot[index];
            if (hazard == null)
            {
                continue;
            }

            if (TryGetCell(hazard.GridPosition, out BoardCell cell))
            {
                cell.ClearHazard(hazard);
            }

            if (Application.isPlaying)
            {
                Destroy(hazard.gameObject);
            }
            else
            {
                DestroyImmediate(hazard.gameObject);
            }
        }
    }

    public int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public bool HasLineOfSight(Vector2Int from, Vector2Int to)
    {
        if (!IsInsideBoard(from) || !IsInsideBoard(to))
        {
            return false;
        }

        if (from == to)
        {
            return true;
        }

        int currentX = from.x;
        int currentY = from.y;
        int targetX = to.x;
        int targetY = to.y;

        float deltaX = targetX - currentX;
        float deltaY = targetY - currentY;
        int stepX = deltaX > 0f ? 1 : -1;
        int stepY = deltaY > 0f ? 1 : -1;

        float tDeltaX = deltaX != 0f ? 1f / Mathf.Abs(deltaX) : float.PositiveInfinity;
        float tDeltaY = deltaY != 0f ? 1f / Mathf.Abs(deltaY) : float.PositiveInfinity;
        float tMaxX = deltaX != 0f ? 0.5f / Mathf.Abs(deltaX) : float.PositiveInfinity;
        float tMaxY = deltaY != 0f ? 0.5f / Mathf.Abs(deltaY) : float.PositiveInfinity;

        while (currentX != targetX || currentY != targetY)
        {
            int previousX = currentX;
            int previousY = currentY;

            if (tMaxX < tMaxY)
            {
                currentX += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxX)
            {
                currentY += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                Vector2Int horizontalCell = new Vector2Int(currentX + stepX, currentY);
                Vector2Int verticalCell = new Vector2Int(currentX, currentY + stepY);

                if (IsSightBlockedCell(horizontalCell, to) || IsSightBlockedCell(verticalCell, to))
                {
                    return false;
                }

                currentX += stepX;
                currentY += stepY;
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }

            Vector2Int currentCell = new Vector2Int(currentX, currentY);
            if (currentCell == to)
            {
                break;
            }

            if (previousX != currentX && previousY != currentY)
            {
                Vector2Int previousHorizontal = new Vector2Int(currentX, previousY);
                Vector2Int previousVertical = new Vector2Int(previousX, currentY);

                if (IsSightBlockedCell(previousHorizontal, to) || IsSightBlockedCell(previousVertical, to))
                {
                    return false;
                }
            }

            if (IsSightBlockedCell(currentCell, to))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryGetEnemy(Vector2Int gridPosition, out Enemy enemy)
    {
        enemy = null;
        if (!TryGetCell(gridPosition, out BoardCell cell) || cell.OccupantKind != BoardOccupantKind.Enemy || cell.Occupant == null)
        {
            return false;
        }

        enemy = cell.Occupant.GetComponent<Enemy>();
        return enemy != null;
    }

    public bool TryGetBarrel(Vector2Int gridPosition, out BarrelObstacle barrel)
    {
        barrel = null;
        if (!TryGetCell(gridPosition, out BoardCell cell) || cell.StaticObstacle == null)
        {
            return false;
        }

        barrel = cell.StaticObstacle.GetComponent<BarrelObstacle>();
        return barrel != null && !barrel.IsDestroyed;
    }

    public bool TryGetSkullObject(Vector2Int gridPosition, out SkullObject skullObject)
    {
        skullObject = null;
        if (!TryGetCell(gridPosition, out BoardCell cell) || cell.StaticObstacle == null)
        {
            return false;
        }

        skullObject = cell.StaticObstacle.GetComponent<SkullObject>();
        return skullObject != null && !skullObject.IsResolving;
    }

    public bool TryGetLichSkullObject(Vector2Int gridPosition, out LichSkullObject lichSkullObject)
    {
        lichSkullObject = null;
        if (!TryGetCell(gridPosition, out BoardCell cell) || cell.StaticObstacle == null)
        {
            return false;
        }

        lichSkullObject = cell.StaticObstacle.GetComponent<LichSkullObject>();
        return lichSkullObject != null && !lichSkullObject.IsResolving;
    }

    public bool TryGetEnemyLikeTarget(Vector2Int gridPosition, out Enemy enemy, out LichSkullObject lichSkullObject)
    {
        if (TryGetEnemy(gridPosition, out enemy) && enemy != null)
        {
            lichSkullObject = null;
            return true;
        }

        if (TryGetLichSkullObject(gridPosition, out lichSkullObject) && lichSkullObject != null)
        {
            enemy = null;
            return true;
        }

        enemy = null;
        lichSkullObject = null;
        return false;
    }

    public void ClearStaticObstacle(Vector2Int gridPosition, GameObject expectedObstacle = null)
    {
        if (!TryGetCell(gridPosition, out BoardCell cell))
        {
            return;
        }

        if (expectedObstacle != null && cell.StaticObstacle != expectedObstacle)
        {
            return;
        }

        cell.StaticObstacle = null;
        cell.StaticObstacleBlocksMovement = false;
        cell.StopsCharacterSlide = false;
        cell.Type = BoardCellType.Classic;
        cell.Walkable = true;
    }

    public bool TryWorldToGridPosition(Vector3 worldPosition, out Vector2Int gridPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        int column = Mathf.RoundToInt((localPosition.x / cellSizeX) + originColumn);
        int row = Mathf.RoundToInt(originRow - (localPosition.z / cellSizeZ));
        gridPosition = new Vector2Int(column, row);
        return IsInsideBoard(gridPosition);
    }

    public void RemoveEnemy(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        spawnedEnemies.Remove(enemy);
        UnregisterLinkedSummon(enemy);
        linkedSummonsByOwner.Remove(enemy);

        Vector2Int enemyPosition = enemy.GridPosition;
        if (TryGetCell(enemyPosition, out BoardCell cell) && cell.Occupant == enemy.gameObject)
        {
            cell.ClearOccupant();
        }

        if (player != null && player.ControlledCharacter != null)
        {
            player.ControlledCharacter.HandleEnemyCountChanged(spawnedEnemies.Count);
        }

        if (!Application.isPlaying || spawnedEnemies.Count != 0)
        {
            return;
        }

        if (activeSkullObjects.Count > 0)
        {
            ResolveRemainingSkullsForVictory();
            return;
        }

        AllEnemiesDefeated?.Invoke();
    }

    public void RegisterSkullObject(SkullObject skullObject)
    {
        if (skullObject == null || activeSkullObjects.Contains(skullObject))
        {
            return;
        }

        activeSkullObjects.Add(skullObject);
    }

    public void UnregisterSkullObject(SkullObject skullObject)
    {
        if (skullObject == null)
        {
            return;
        }

        activeSkullObjects.Remove(skullObject);
        if (Application.isPlaying && spawnedEnemies.Count == 0 && activeSkullObjects.Count == 0)
        {
            isResolvingSkullsForVictory = false;
            AllEnemiesDefeated?.Invoke();
        }
    }

    public bool SpawnSkeletonSkull(Enemy enemy)
    {
        if (enemy == null
            || enemy.Data is not SkeletonEnemyData skeletonData
            || !skeletonData.CanSpawnSkullOnDeath
            || skeletonData.SkullObjectPrefab == null)
        {
            return false;
        }

        if (!TryGetCell(enemy.GridPosition, out BoardCell cell))
        {
            return false;
        }

        Vector3 spawnWorldPosition = cell.WorldPosition + Vector3.up * spawnHeight;
        GameObject skullObjectInstance = InstantiateOrCreate(
            skeletonData.SkullObjectPrefab.gameObject,
            $"Skull_{enemy.GridPosition.x}_{enemy.GridPosition.y}",
            obstaclesRoot,
            spawnWorldPosition);
        SkullObject skullObject = GetOrAddComponent<SkullObject>(skullObjectInstance);
        skullObject.Assign(this, enemy.GridPosition, skeletonData);
        cell.SetStaticObstacle(skullObjectInstance, BoardCellType.Rock);
        RegisterSkullObject(skullObject);
        return true;
    }

    public bool SpawnLichSkull(Vector2Int gridPosition, LichEnemyData lichData, GameObject enemyPrefab, Enemy summoner)
    {
        if (lichData == null || lichData.LichSkullObjectPrefab == null || enemyPrefab == null)
        {
            return false;
        }

        if (!TryGetCell(gridPosition, out BoardCell cell) || cell.IsOccupied || cell.HasBlockingTerrain)
        {
            return false;
        }

        Vector3 spawnWorldPosition = cell.WorldPosition + Vector3.up * spawnHeight;
        if (defaultEnemySpawnFxPrefab != null)
        {
            GameObject spawnFx = Instantiate(defaultEnemySpawnFxPrefab, spawnWorldPosition, defaultEnemySpawnFxPrefab.transform.rotation, generatedRoot);
            if (spawnFx != null && defaultEnemySpawnFxLifetime > 0f)
            {
                Destroy(spawnFx, defaultEnemySpawnFxLifetime);
            }
        }

        GameObject skullObjectInstance = InstantiateOrCreate(
            lichData.LichSkullObjectPrefab.gameObject,
            $"LichSkull_{gridPosition.x}_{gridPosition.y}",
            obstaclesRoot,
            spawnWorldPosition);
        LichSkullObject lichSkullObject = GetOrAddComponent<LichSkullObject>(skullObjectInstance);
        lichSkullObject.Assign(this, gridPosition, enemyPrefab, summoner);
        cell.SetStaticObstacle(skullObjectInstance, BoardCellType.Rock);
        RegisterSkullObject(lichSkullObject);
        return true;
    }

    public bool ReviveSkeletonFromSkull(SkullObject skullObject, SkeletonEnemyData skeletonData)
    {
        if (skullObject == null || skeletonData == null)
        {
            return false;
        }

        GameObject enemyPrefab = ResolveEnemyPrefabForData(skeletonData);
        if (enemyPrefab == null || !TryGetCell(skullObject.GridPosition, out BoardCell cell) || cell.IsOccupied)
        {
            return false;
        }

        Vector3 spawnWorldPosition = cell.WorldPosition + Vector3.up * spawnHeight;
        if (defaultEnemySpawnFxPrefab != null)
        {
            GameObject spawnFx = Instantiate(defaultEnemySpawnFxPrefab, spawnWorldPosition, defaultEnemySpawnFxPrefab.transform.rotation, generatedRoot);
            if (spawnFx != null && defaultEnemySpawnFxLifetime > 0f)
            {
                Destroy(spawnFx, defaultEnemySpawnFxLifetime);
            }
        }

        GameObject enemyObject = InstantiateOrCreate(
            enemyPrefab,
            $"Enemy_{skullObject.GridPosition.x}_{skullObject.GridPosition.y}_Revived",
            enemiesRoot,
            spawnWorldPosition);
        Enemy enemy = GetOrAddComponent<Enemy>(enemyObject);
        enemy.Assign(skullObject.GridPosition, this);
        enemy.PlayReviveAnimation();
        spawnedEnemies.Add(enemy);
        cell.SetOccupant(enemyObject, BoardOccupantKind.Enemy);
        player?.ControlledCharacter?.HandleEnemyCountChanged(spawnedEnemies.Count);
        return true;
    }

    public bool ReviveEnemyFromLichSkull(LichSkullObject skullObject, GameObject enemyPrefab, Enemy summoner)
    {
        if (skullObject == null || enemyPrefab == null)
        {
            return false;
        }

        if (!TryGetCell(skullObject.GridPosition, out BoardCell cell) || cell.IsOccupied)
        {
            return false;
        }

        Vector3 spawnWorldPosition = cell.WorldPosition + Vector3.up * spawnHeight;
        if (defaultEnemySpawnFxPrefab != null)
        {
            GameObject spawnFx = Instantiate(defaultEnemySpawnFxPrefab, spawnWorldPosition, defaultEnemySpawnFxPrefab.transform.rotation, generatedRoot);
            if (spawnFx != null && defaultEnemySpawnFxLifetime > 0f)
            {
                Destroy(spawnFx, defaultEnemySpawnFxLifetime);
            }
        }

        GameObject enemyObject = InstantiateOrCreate(
            enemyPrefab,
            $"Enemy_{skullObject.GridPosition.x}_{skullObject.GridPosition.y}_LichSummon",
            enemiesRoot,
            spawnWorldPosition);
        Enemy enemy = GetOrAddComponent<Enemy>(enemyObject);
        enemy.Assign(skullObject.GridPosition, this);
        enemy.SetLinkedSummoner(summoner);
        enemy.PlayReviveAnimation();
        spawnedEnemies.Add(enemy);
        cell.SetOccupant(enemyObject, BoardOccupantKind.Enemy);
        RegisterLinkedSummon(summoner, enemy);
        player?.ControlledCharacter?.HandleEnemyCountChanged(spawnedEnemies.Count);
        return true;
    }

    public void RegisterLinkedSummon(Enemy owner, Enemy summon)
    {
        if (owner == null || summon == null)
        {
            return;
        }

        if (!linkedSummonsByOwner.TryGetValue(owner, out HashSet<Enemy> summons))
        {
            summons = new HashSet<Enemy>();
            linkedSummonsByOwner[owner] = summons;
        }

        summons.Add(summon);
        summonOwnerByMinion[summon] = owner;
    }

    public void HandleLinkedSummonsForDeadOwner(Enemy owner)
    {
        if (owner == null || !linkedSummonsByOwner.TryGetValue(owner, out HashSet<Enemy> summons) || summons == null)
        {
            return;
        }

        linkedSummonsByOwner.Remove(owner);
        List<Enemy> summonSnapshot = new List<Enemy>(summons);
        for (int index = 0; index < summonSnapshot.Count; index++)
        {
            Enemy summon = summonSnapshot[index];
            if (summon == null)
            {
                continue;
            }

            summon.ForceEliminateLinkedSummon();
        }
    }

    private bool IsSightBlockedCell(Vector2Int gridPosition, Vector2Int targetCell)
    {
        if (gridPosition == targetCell)
        {
            return false;
        }

        if (!TryGetCell(gridPosition, out BoardCell cell))
        {
            return true;
        }

        return cell.HasBlockingTerrain;
    }

    private void InitializeCells()
    {
        cells = new BoardCell[BoardWidth, BoardHeight];

        for (int row = 0; row < BoardHeight; row++)
        {
            for (int column = 0; column < BoardWidth; column++)
            {
                cells[column, row] = new BoardCell
                {
                    GridPosition = new Vector2Int(column, row),
                    WorldPosition = GridToWorldPosition(new Vector2Int(column, row)),
                    Type = BoardCellType.Classic,
                    Walkable = true,
                    IsOccupied = false,
                    OccupantKind = BoardOccupantKind.None,
                    Occupant = null,
                    StaticObstacle = null,
                    StaticObstacleBlocksMovement = false,
                    StopsCharacterSlide = false,
                    Hazard = null
                };
            }
        }
    }

    private Texture2D SelectArenaLayout(EnemyPoolDefinition selectedPool)
    {
        if (selectedPool != null && selectedPool.ArenaLayoutOverride != null)
        {
            return selectedPool.ArenaLayoutOverride;
        }

        IReadOnlyList<Texture2D> biomeArenaLayouts = GetArenaLayoutsForCurrentBiome();
        if (biomeArenaLayouts != null && biomeArenaLayouts.Count > 0)
        {
            List<Texture2D> validArenas = new List<Texture2D>();
            for (int index = 0; index < biomeArenaLayouts.Count; index++)
            {
                if (biomeArenaLayouts[index] != null)
                {
                    validArenas.Add(biomeArenaLayouts[index]);
                }
            }

            if (validArenas.Count > 0)
            {
                return validArenas[UnityEngine.Random.Range(0, validArenas.Count)];
            }
        }

        return null;
    }

    private ArenaMarker GetMarkerForCell(int column, int row, Texture2D layout, bool mirrorOnYAxis)
    {
        int sampledColumn = mirrorOnYAxis ? (BoardWidth - 1) - column : column;
        Color pixel = layout.GetPixel(sampledColumn, (BoardHeight - 1) - row);
        float max = Mathf.Max(pixel.r, pixel.g, pixel.b);

        if (max < 0.2f)
        {
            return ArenaMarker.Obstacle;
        }

        bool isGray = Mathf.Abs(pixel.r - pixel.g) < 0.08f
            && Mathf.Abs(pixel.g - pixel.b) < 0.08f
            && pixel.r > 0.2f
            && pixel.r < 0.8f;

        if (isGray)
        {
            return ArenaMarker.OptionalObstacle;
        }

        if (pixel.g > 0.45f && pixel.g > pixel.r * 1.2f && pixel.g > pixel.b * 1.2f)
        {
            return ArenaMarker.PlayerSpawn;
        }

        if (pixel.r > 0.45f && pixel.r > pixel.g * 1.2f && pixel.r > pixel.b * 1.2f)
        {
            return ArenaMarker.EnemySpawn;
        }

        return ArenaMarker.Empty;
    }

    private void SpawnObstacle(BoardCell cell)
    {
        GameObject obstacle = InstantiateFromList(GetObstaclePrefabsForCurrentBiome(), $"Obstacle_{cell.GridPosition.x}_{cell.GridPosition.y}", obstaclesRoot, cell.WorldPosition);
        if (obstacle == null)
        {
            return;
        }

        StonePlatformObstacle stonePlatform = obstacle.GetComponent<StonePlatformObstacle>();
        if (stonePlatform != null)
        {
            stonePlatform.ApplySpawnPresentation();
            cell.SetTraversalStopSurface(obstacle);
            return;
        }

        ApplyBiomeRockColor(obstacle);
        cell.SetStaticObstacle(obstacle, BoardCellType.Rock);

        BarrelObstacle barrel = obstacle.GetComponent<BarrelObstacle>();
        if (barrel != null)
        {
            barrel.Assign(this, cell.GridPosition);
        }
    }

    private void SpawnPlayerCharacter(List<Vector2Int> spawnCandidates)
    {
        if (spawnCandidates.Count == 0)
        {
            Debug.LogWarning("No player spawn marker found in arena layout.", this);
            return;
        }

        Vector2Int spawnPoint = spawnCandidates[UnityEngine.Random.Range(0, spawnCandidates.Count)];
        GameObject playerObject = new GameObject("Player");
        playerObject.transform.SetParent(playersRoot, false);
        player = playerObject.AddComponent<Player>();

        GameObject characterObject = InstantiateOrCreate(
            playerCharacterPrefab,
            "PlayerCharacter",
            playerObject.transform,
            cells[spawnPoint.x, spawnPoint.y].WorldPosition + Vector3.up * spawnHeight);

        Character character = GetOrAddComponent<Character>(characterObject);
        character.Assign(player, spawnPoint, this);
        EnsureRunRewardStateInitialized(character);
        character.ApplyRunRewardState(runRewardState);
        player.AssignCharacter(character);
        cells[spawnPoint.x, spawnPoint.y].SetOccupant(characterObject, BoardOccupantKind.PlayerCharacter);
    }

    private void SpawnEnemies(List<Vector2Int> spawnCandidates)
    {
        if (spawnCandidates.Count == 0)
        {
            return;
        }

        List<GameObject> enemyPrefabsToSpawn = ResolveEnemyPrefabsForCurrentArena();
        currentArenaEnemyPrefabs.Clear();
        currentArenaEnemyPrefabs.AddRange(enemyPrefabsToSpawn);
        if (enemyPrefabsToSpawn.Count == 0)
        {
            return;
        }

        Shuffle(spawnCandidates);
        int count = Mathf.Min(enemyPrefabsToSpawn.Count, spawnCandidates.Count);

        for (int index = 0; index < count; index++)
        {
            Vector2Int spawnPoint = spawnCandidates[index];
            BoardCell cell = cells[spawnPoint.x, spawnPoint.y];
            GameObject enemyPrefab = enemyPrefabsToSpawn[index];
            if (enemyPrefab == null)
            {
                continue;
            }

            GameObject enemyObject = InstantiateOrCreate(
                enemyPrefab,
                $"Enemy_{spawnPoint.x}_{spawnPoint.y}",
                enemiesRoot,
                cell.WorldPosition + Vector3.up * spawnHeight);
            Enemy enemy = GetOrAddComponent<Enemy>(enemyObject);
            enemy.Assign(spawnPoint, this);
            spawnedEnemies.Add(enemy);
            cell.SetOccupant(enemyObject, BoardOccupantKind.Enemy);
        }
    }

    private void EnsureGeneratedRoots()
    {
        generatedRoot = GetOrCreateChild(transform, "GeneratedBoard");
        obstaclesRoot = GetOrCreateChild(generatedRoot, "Obstacles");
        enemiesRoot = GetOrCreateChild(generatedRoot, "Enemies");
        playersRoot = GetOrCreateChild(generatedRoot, "Players");
    }

    private void ClearGeneratedContent()
    {
        ClearChildren(obstaclesRoot);
        ClearChildren(enemiesRoot);
        ClearChildren(playersRoot);
        spawnedEnemies.Clear();
        player = null;
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            GameObject child = parent.GetChild(index).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private GameObject InstantiateFromList(IReadOnlyList<GameObject> prefabs, string fallbackName, Transform parent, Vector3 position)
    {
        GameObject prefab = prefabs != null && prefabs.Count > 0 ? prefabs[UnityEngine.Random.Range(0, prefabs.Count)] : null;
        return InstantiateOrCreate(prefab, fallbackName, parent, position);
    }

    private List<GameObject> ResolveEnemyPrefabsForCurrentArena()
    {
        EnemyPoolDefinition selectedPool = currentSelectedEnemyPool != null
            ? currentSelectedEnemyPool
            : ResolveSelectedEnemyPoolForCurrentArena();
        return ExtractValidEnemyPrefabs(selectedPool);
    }

    private EnemyPoolDefinition ResolveSelectedEnemyPoolForCurrentArena()
    {
#if UNITY_EDITOR
        if (useForcedDatas && forcedEnemyPoolDefinition != null)
        {
            return forcedEnemyPoolDefinition;
        }
#endif

        BiomeEnemyPoolAvailability selectedAvailability = null;
        int selectedRangeSize = int.MaxValue;

        IReadOnlyList<BiomeEnemyPoolAvailability> poolAvailabilities = GetEnemyPoolAvailabilitiesForCurrentBiome();
        for (int index = 0; index < poolAvailabilities.Count; index++)
        {
            BiomeEnemyPoolAvailability availability = poolAvailabilities[index];
            if (availability == null || !availability.MatchesArenaCount(arenaCount))
            {
                continue;
            }

            int rangeSize = availability.MaxArenaCount - availability.MinArenaCount;
            bool isMoreSpecificRange = rangeSize < selectedRangeSize;
            bool isSameSpecificityButLaterStart = selectedAvailability != null
                && rangeSize == selectedRangeSize
                && availability.MinArenaCount > selectedAvailability.MinArenaCount;

            if (selectedAvailability == null || isMoreSpecificRange || isSameSpecificityButLaterStart)
            {
                selectedAvailability = availability;
                selectedRangeSize = rangeSize;
            }
        }

        if (selectedAvailability == null)
        {
            return null;
        }

        List<EnemyPoolDefinition> eligiblePools = new List<EnemyPoolDefinition>();
        IReadOnlyList<EnemyPoolDefinition> pools = selectedAvailability.Pools;
        for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
        {
            EnemyPoolDefinition pool = pools[poolIndex];
            if (pool != null)
            {
                eligiblePools.Add(pool);
            }
        }

        if (eligiblePools.Count == 0)
        {
            return null;
        }

        List<EnemyPoolDefinition> availablePools = GetAvailableEnemyPools(eligiblePools);
        if (availablePools.Count == 0)
        {
            ResetEncounteredEnemyPools(eligiblePools);
            availablePools.AddRange(eligiblePools);
        }

        EnemyPoolDefinition selectedPool = availablePools[UnityEngine.Random.Range(0, availablePools.Count)];
        encounteredEnemyPools.Add(selectedPool);
        return selectedPool;
    }

    private List<EnemyPoolDefinition> GetAvailableEnemyPools(List<EnemyPoolDefinition> eligiblePools)
    {
        List<EnemyPoolDefinition> availablePools = new List<EnemyPoolDefinition>();
        if (eligiblePools == null)
        {
            return availablePools;
        }

        for (int index = 0; index < eligiblePools.Count; index++)
        {
            EnemyPoolDefinition pool = eligiblePools[index];
            if (pool != null && !encounteredEnemyPools.Contains(pool))
            {
                availablePools.Add(pool);
            }
        }

        return availablePools;
    }

    private void ResetEncounteredEnemyPools(List<EnemyPoolDefinition> poolsToReset)
    {
        if (poolsToReset == null || poolsToReset.Count == 0)
        {
            return;
        }

        for (int index = 0; index < poolsToReset.Count; index++)
        {
            EnemyPoolDefinition pool = poolsToReset[index];
            if (pool != null)
            {
                encounteredEnemyPools.Remove(pool);
            }
        }
    }

    private static List<GameObject> ExtractValidEnemyPrefabs(EnemyPoolDefinition poolDefinition)
    {
        List<GameObject> poolEnemies = new List<GameObject>();
        if (poolDefinition == null)
        {
            return poolEnemies;
        }

        List<GameObject> enemyPrefabs = poolDefinition.GetValidEnemyPrefabs();
        for (int index = 0; index < enemyPrefabs.Count; index++)
        {
            GameObject enemyPrefab = enemyPrefabs[index];
            if (enemyPrefab != null)
            {
                poolEnemies.Add(enemyPrefab);
            }
        }

        return poolEnemies;
    }

    private GameObject InstantiateOrCreate(GameObject prefab, string fallbackName, Transform parent, Vector3 position)
    {
        GameObject instance;
        if (prefab != null)
        {
            try
            {
                instance = Instantiate(prefab, position, prefab.transform.rotation, parent);
                instance.name = prefab.name;
            }
            catch (MissingReferenceException)
            {
                instance = new GameObject(fallbackName);
                instance.transform.SetParent(parent, false);
                instance.transform.position = position;
            }
        }
        else
        {
            instance = new GameObject(fallbackName);
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
        }

        return instance;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (list[index], list[swapIndex]) = (list[swapIndex], list[index]);
        }
    }

    public List<RewardOffer> GenerateRewardChoices()
    {
        EnsureRunRewardStateInitialized(player != null ? player.ControlledCharacter : null);
        List<RewardOffer> powerOffers = BuildAvailablePowerOffers();
        List<RewardOffer> itemOffers = BuildAvailableItemOffers();
        List<RewardOffer> choices = new List<RewardOffer>();

        bool rewardBoostActive = bonusRewardForCurrentArena;
        bonusRewardForCurrentArena = false;

        int desiredPowerCount = rewardBoostActive ? 2 : (UnityEngine.Random.value < 0.5f ? 2 : 1);
        int desiredItemCount = 3 - desiredPowerCount;

        AddRandomUniqueRewards(choices, powerOffers, desiredPowerCount);
        AddRandomUniqueRewards(choices, itemOffers, desiredItemCount);

        if (choices.Count < 3)
        {
            AddRandomUniqueRewards(choices, powerOffers, 3 - choices.Count);
        }

        if (choices.Count < 3)
        {
            AddRandomUniqueRewards(choices, itemOffers, 3 - choices.Count);
        }

        Shuffle(choices);
        return choices;
    }

    public void ApplyReward(RewardOffer rewardOffer)
    {
        if (rewardOffer == null)
        {
            return;
        }

        EnsureRunRewardStateInitialized(player != null ? player.ControlledCharacter : null);
        if (rewardOffer.Definition != null)
        {
            rewardOffer.Definition.Apply(runRewardState);
        }
        else
        {
            switch (rewardOffer.Kind)
            {
                case RewardOfferKind.AbilityUnlock:
                    runRewardState.UnlockAbility(rewardOffer.Ability);
                    break;
                case RewardOfferKind.AbilityUpgrade:
                    runRewardState.AddUpgrade(rewardOffer.UpgradeKey);
                    break;
                case RewardOfferKind.Item:
                    runRewardState.AddItem(rewardOffer.ItemKey);
                    break;
            }
        }

        if (player != null && player.ControlledCharacter != null)
        {
            player.ControlledCharacter.ApplyRunRewardState(runRewardState);
        }
    }

    public List<ItemRewardDefinition> GetCombatStartYesNoItemDefinitions()
    {
        List<ItemRewardDefinition> promptDefinitions = new List<ItemRewardDefinition>();
        if (runRewardState == null || IsCurrentArenaBossBattle())
        {
            return promptDefinitions;
        }

        for (int index = 0; index < itemRewardDefinitions.Count; index++)
        {
            ItemRewardDefinition itemRewardDefinition = itemRewardDefinitions[index];
            if (itemRewardDefinition == null
                || !itemRewardDefinition.ShowCombatStartYesNoPrompt
                || !runRewardState.HasItem(itemRewardDefinition.ItemKey))
            {
                continue;
            }

            promptDefinitions.Add(itemRewardDefinition);
        }

        return promptDefinitions;
    }

    public bool IsCurrentArenaBossBattle()
    {
        EnemyPoolDefinition selectedPool = currentSelectedEnemyPool != null
            ? currentSelectedEnemyPool
            : ResolveSelectedEnemyPoolForCurrentArena();
        if (selectedPool == null)
        {
            return false;
        }

        IReadOnlyList<GameObject> enemyPrefabs = selectedPool.EnemyPrefabs;
        for (int index = 0; index < enemyPrefabs.Count; index++)
        {
            GameObject enemyPrefab = enemyPrefabs[index];
            if (enemyPrefab == null)
            {
                continue;
            }

            if (enemyPrefab.name.StartsWith("Boss-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void MarkBonusRewardForCurrentArena()
    {
        bonusRewardForCurrentArena = true;
    }

    public ItemRewardDefinition GetItemRewardDefinition(ItemRewardKey itemKey)
    {
        for (int index = 0; index < itemRewardDefinitions.Count; index++)
        {
            ItemRewardDefinition itemRewardDefinition = itemRewardDefinitions[index];
            if (itemRewardDefinition != null && itemRewardDefinition.ItemKey == itemKey)
            {
                return itemRewardDefinition;
            }
        }

        return null;
    }

    public string GetCurrentCharacterId()
    {
        Character character = player != null ? player.ControlledCharacter : null;
        CharacterData characterData = character != null ? character.Data : null;
        return characterData != null ? characterData.name : string.Empty;
    }

    public EquipmentBuildData CreateEquipmentBuildSnapshot(string buildName)
    {
        Character character = player != null ? player.ControlledCharacter : null;
        CharacterData characterData = character != null ? character.Data : null;
        if (character == null || characterData == null)
        {
            return null;
        }

        EnsureRunRewardStateInitialized(character);
        if (runRewardState == null)
        {
            return null;
        }

        EquipmentBuildData buildData = new EquipmentBuildData
        {
            BuildName = string.IsNullOrWhiteSpace(buildName) ? "Build" : buildName.Trim(),
            CharacterId = characterData.name,
            BasicAttackAbilityId = GetAbilityId(runRewardState.GetEquippedAbility(AbilityCategory.BasicAttack)),
            MobilityAbilityId = GetAbilityId(runRewardState.GetEquippedAbility(AbilityCategory.MobilitySkill)),
            SpecialAbilityId = GetAbilityId(runRewardState.GetEquippedAbility(AbilityCategory.SpecialPower))
        };

        Dictionary<AbilityUpgradeKey, int> upgradeSnapshot = runRewardState.GetAllUpgradeStacks();
        foreach (KeyValuePair<AbilityUpgradeKey, int> pair in upgradeSnapshot)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            buildData.Upgrades.Add(new EquipmentBuildUpgradeEntry
            {
                UpgradeKey = pair.Key.ToString(),
                Stacks = pair.Value
            });
        }

        List<ItemRewardKey> ownedItems = runRewardState.GetOwnedItems();
        for (int index = 0; index < ownedItems.Count; index++)
        {
            buildData.OwnedItems.Add(ownedItems[index].ToString());
        }

        return buildData;
    }

    public bool ApplyEquipmentBuild(EquipmentBuildData buildData)
    {
        Character character = player != null ? player.ControlledCharacter : null;
        CharacterData characterData = character != null ? character.Data : null;
        if (character == null || characterData == null || buildData == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(buildData.CharacterId)
            && !string.Equals(buildData.CharacterId, characterData.name, StringComparison.Ordinal))
        {
            return false;
        }

        PlayerRunRewardState loadedState = new PlayerRunRewardState();
        loadedState.InitializeFrom(characterData.StartingAbilities, character.CurrentHealth);

        Dictionary<string, AbilityDefinition> potentialAbilitiesById = BuildPotentialAbilityLookup(characterData);
        ApplyBuiltAbilitySlot(loadedState, AbilityCategory.BasicAttack, buildData.BasicAttackAbilityId, potentialAbilitiesById);
        ApplyBuiltAbilitySlot(loadedState, AbilityCategory.MobilitySkill, buildData.MobilityAbilityId, potentialAbilitiesById);
        ApplyBuiltAbilitySlot(loadedState, AbilityCategory.SpecialPower, buildData.SpecialAbilityId, potentialAbilitiesById);

        if (buildData.Upgrades != null)
        {
            for (int index = 0; index < buildData.Upgrades.Count; index++)
            {
                EquipmentBuildUpgradeEntry entry = buildData.Upgrades[index];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.UpgradeKey)
                    || entry.Stacks <= 0
                    || !Enum.TryParse(entry.UpgradeKey, out AbilityUpgradeKey upgradeKey))
                {
                    continue;
                }

                for (int stackIndex = 0; stackIndex < entry.Stacks; stackIndex++)
                {
                    loadedState.AddUpgrade(upgradeKey);
                }
            }
        }

        if (buildData.OwnedItems != null)
        {
            for (int index = 0; index < buildData.OwnedItems.Count; index++)
            {
                string itemKeyString = buildData.OwnedItems[index];
                if (string.IsNullOrWhiteSpace(itemKeyString) || !Enum.TryParse(itemKeyString, out ItemRewardKey itemKey))
                {
                    continue;
                }

                loadedState.AddItem(itemKey);
            }
        }

        runRewardState = loadedState;
        player.ControlledCharacter.ApplyRunRewardState(runRewardState);
        return true;
    }

    private void EnsureRunRewardStateInitialized(Character character)
    {
        if (runRewardState == null)
        {
            runRewardState = new PlayerRunRewardState();
        }

        if (runRewardState.IsInitialized || character == null)
        {
            return;
        }

        runRewardState.InitializeFrom(character.GetCurrentAbilityDefinitions(), character.CurrentHealth);
    }

    private static string GetAbilityId(AbilityDefinition ability)
    {
        return ability != null ? ability.name : string.Empty;
    }

    private static Dictionary<string, AbilityDefinition> BuildPotentialAbilityLookup(CharacterData characterData)
    {
        Dictionary<string, AbilityDefinition> lookup = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
        if (characterData == null)
        {
            return lookup;
        }

        List<AbilityDefinition> potentialAbilities = characterData.GetAllPotentialAbilities();
        for (int index = 0; index < potentialAbilities.Count; index++)
        {
            AbilityDefinition ability = potentialAbilities[index];
            if (ability == null || string.IsNullOrWhiteSpace(ability.name) || lookup.ContainsKey(ability.name))
            {
                continue;
            }

            lookup.Add(ability.name, ability);
        }

        return lookup;
    }

    private static void ApplyBuiltAbilitySlot(
        PlayerRunRewardState loadedState,
        AbilityCategory category,
        string abilityId,
        Dictionary<string, AbilityDefinition> potentialAbilitiesById)
    {
        if (loadedState == null
            || string.IsNullOrWhiteSpace(abilityId)
            || potentialAbilitiesById == null
            || !potentialAbilitiesById.TryGetValue(abilityId, out AbilityDefinition ability)
            || ability == null
            || ability.Category != category)
        {
            return;
        }

        loadedState.UnlockAbility(ability);
    }

    private List<RewardOffer> BuildAvailablePowerOffers()
    {
        List<RewardOffer> offers = new List<RewardOffer>();
        Character character = player != null ? player.ControlledCharacter : null;
        CharacterData characterData = character != null ? character.Data : null;
        if (characterData == null)
        {
            return offers;
        }

        AddRewardOffers(offers, characterData.UnlockableAbilityRewards);

        HashSet<AbilityDefinition> trackedAbilities = new HashSet<AbilityDefinition>();
        AddAbilityUpgradeOffers(offers, characterData.GetAllPotentialAbilities(), trackedAbilities);
        return offers;
    }

    private List<RewardOffer> BuildAvailableItemOffers()
    {
        List<RewardOffer> offers = new List<RewardOffer>();
        AddRewardOffers(offers, itemRewardDefinitions);
        return offers;
    }

    private void AddRewardOffers<TRewardDefinition>(List<RewardOffer> offers, IReadOnlyList<TRewardDefinition> rewardDefinitions)
        where TRewardDefinition : RewardDefinition
    {
        if (rewardDefinitions == null)
        {
            return;
        }

        for (int index = 0; index < rewardDefinitions.Count; index++)
        {
            RewardDefinition rewardDefinition = rewardDefinitions[index];
            if (rewardDefinition == null || !rewardDefinition.CanOffer(runRewardState))
            {
                continue;
            }

            offers.Add(rewardDefinition.CreateOffer());
        }
    }

    private void AddAbilityUpgradeOffers(
        List<RewardOffer> offers,
        IReadOnlyList<AbilityDefinition> abilities,
        HashSet<AbilityDefinition> trackedAbilities)
    {
        if (abilities == null)
        {
            return;
        }

        for (int abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
        {
            AbilityDefinition ability = abilities[abilityIndex];
            if (ability == null || !trackedAbilities.Add(ability))
            {
                continue;
            }

            IReadOnlyList<AbilityUpgradeRewardDefinition> linkedUpgradeRewards = ability.LinkedUpgradeRewards;
            if (linkedUpgradeRewards == null)
            {
                continue;
            }

            for (int rewardIndex = 0; rewardIndex < linkedUpgradeRewards.Count; rewardIndex++)
            {
                AbilityUpgradeRewardDefinition rewardDefinition = linkedUpgradeRewards[rewardIndex];
                if (rewardDefinition == null || !rewardDefinition.CanOffer(runRewardState))
                {
                    continue;
                }

                offers.Add(rewardDefinition.CreateOffer());
            }
        }
    }

    private void AddRandomUniqueRewards(List<RewardOffer> target, List<RewardOffer> source, int desiredCount)
    {
        if (desiredCount <= 0 || source == null || source.Count == 0)
        {
            return;
        }

        List<RewardOffer> workingSource = new List<RewardOffer>(source);
        Shuffle(workingSource);

        for (int index = 0; index < workingSource.Count && desiredCount > 0; index++)
        {
            RewardOffer rewardOffer = workingSource[index];
            bool alreadyAdded = false;
            for (int targetIndex = 0; targetIndex < target.Count; targetIndex++)
            {
                if (target[targetIndex].Id == rewardOffer.Id)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (alreadyAdded)
            {
                continue;
            }

            target.Add(rewardOffer);
            desiredCount--;
        }
    }

    public bool TrySpawnExtraEnemyWithDefaultFx(out Enemy enemy)
    {
        enemy = null;

        List<GameObject> candidateEnemies = new List<GameObject>();
        AddNonNullPrefabs(candidateEnemies, GetSpawnableEnemiesForCurrentBiome());
        if (candidateEnemies.Count == 0)
        {
            AddNonNullPrefabs(candidateEnemies, currentArenaEnemyPrefabs);
        }

        if (candidateEnemies.Count == 0)
        {
            return false;
        }

        List<Vector2Int> candidateCells = GetAvailableExtraEnemySpawnCells();
        if (candidateCells.Count == 0)
        {
            return false;
        }

        Vector2Int spawnPoint = candidateCells[UnityEngine.Random.Range(0, candidateCells.Count)];
        GameObject enemyPrefab = candidateEnemies[UnityEngine.Random.Range(0, candidateEnemies.Count)];
        if (enemyPrefab == null)
        {
            return false;
        }

        BoardCell cell = cells[spawnPoint.x, spawnPoint.y];
        Vector3 spawnWorldPosition = cell.WorldPosition + Vector3.up * spawnHeight;

        if (defaultEnemySpawnFxPrefab != null)
        {
            GameObject spawnFx = Instantiate(defaultEnemySpawnFxPrefab, spawnWorldPosition, defaultEnemySpawnFxPrefab.transform.rotation, generatedRoot);
            if (spawnFx != null && defaultEnemySpawnFxLifetime > 0f)
            {
                Destroy(spawnFx, defaultEnemySpawnFxLifetime);
            }
        }

        GameObject enemyObject = InstantiateOrCreate(
            enemyPrefab,
            $"Enemy_{spawnPoint.x}_{spawnPoint.y}_Extra",
            enemiesRoot,
            spawnWorldPosition);
        enemy = GetOrAddComponent<Enemy>(enemyObject);
        enemy.Assign(spawnPoint, this);
        spawnedEnemies.Add(enemy);
        cell.SetOccupant(enemyObject, BoardOccupantKind.Enemy);
        player?.ControlledCharacter?.HandleEnemyCountChanged(spawnedEnemies.Count);
        return true;
    }

    private void ResolveRemainingSkullsForVictory()
    {
        if (isResolvingSkullsForVictory || activeSkullObjects.Count == 0)
        {
            return;
        }

        isResolvingSkullsForVictory = true;
        List<SkullObject> skullSnapshot = new List<SkullObject>(activeSkullObjects);
        for (int index = 0; index < skullSnapshot.Count; index++)
        {
            SkullObject skullObject = skullSnapshot[index];
            if (skullObject == null)
            {
                continue;
            }

            skullObject.ShatterForVictory();
        }
    }

    private void UnregisterLinkedSummon(Enemy summon)
    {
        if (summon == null || !summonOwnerByMinion.TryGetValue(summon, out Enemy owner))
        {
            return;
        }

        summonOwnerByMinion.Remove(summon);
        if (owner == null || !linkedSummonsByOwner.TryGetValue(owner, out HashSet<Enemy> summons))
        {
            return;
        }

        summons.Remove(summon);
        if (summons.Count == 0)
        {
            linkedSummonsByOwner.Remove(owner);
        }
    }

    private GameObject ResolveEnemyPrefabForData(EnemyData enemyData)
    {
        if (enemyData == null)
        {
            return null;
        }

        for (int index = 0; index < currentArenaEnemyPrefabs.Count; index++)
        {
            GameObject prefab = currentArenaEnemyPrefabs[index];
            if (PrefabMatchesEnemyData(prefab, enemyData))
            {
                return prefab;
            }
        }

        EnemyPoolDefinition selectedPool = currentSelectedEnemyPool != null
            ? currentSelectedEnemyPool
            : ResolveSelectedEnemyPoolForCurrentArena();
        if (selectedPool != null)
        {
            IReadOnlyList<GameObject> poolPrefabs = selectedPool.EnemyPrefabs;
            for (int index = 0; index < poolPrefabs.Count; index++)
            {
                GameObject prefab = poolPrefabs[index];
                if (PrefabMatchesEnemyData(prefab, enemyData))
                {
                    return prefab;
                }
            }
        }

        IReadOnlyList<GameObject> biomeSpawnables = GetSpawnableEnemiesForCurrentBiome();
        for (int index = 0; index < biomeSpawnables.Count; index++)
        {
            GameObject prefab = biomeSpawnables[index];
            if (PrefabMatchesEnemyData(prefab, enemyData))
            {
                return prefab;
            }
        }

        return null;
    }

    private static bool PrefabMatchesEnemyData(GameObject prefab, EnemyData enemyData)
    {
        if (prefab == null || enemyData == null)
        {
            return false;
        }

        Enemy enemy = prefab.GetComponent<Enemy>();
        return enemy != null && enemy.Data == enemyData;
    }

    private void AdvanceToNextBiome()
    {
        int biomeCount = GetValidBiomeCount();
        if (biomeCount > 0)
        {
            currentBiomeIndex = Mathf.Min(currentBiomeIndex + 1, biomeCount - 1);
        }

        arenaCount = 1;
        currentSelectedEnemyPool = null;
        ApplyCurrentBiomeVisuals();
    }

    private int GetValidBiomeCount()
    {
        if (biomes == null)
        {
            return 0;
        }

        int validBiomeCount = 0;
        for (int index = 0; index < biomes.Count; index++)
        {
            if (biomes[index] != null)
            {
                validBiomeCount++;
            }
        }

        return validBiomeCount;
    }

    private BiomeData GetCurrentBiomeData()
    {
#if UNITY_EDITOR
        if (useForcedDatas && forcedBiome != null)
        {
            return forcedBiome;
        }
#endif

        if (biomes == null || biomes.Count == 0)
        {
            return null;
        }

        List<BiomeData> validBiomes = new List<BiomeData>();
        for (int index = 0; index < biomes.Count; index++)
        {
            if (biomes[index] != null)
            {
                validBiomes.Add(biomes[index]);
            }
        }

        if (validBiomes.Count == 0)
        {
            return null;
        }

        currentBiomeIndex = Mathf.Clamp(currentBiomeIndex, 0, validBiomes.Count - 1);
        return validBiomes[currentBiomeIndex];
    }

    private IReadOnlyList<Texture2D> GetArenaLayoutsForCurrentBiome()
    {
        BiomeData biomeData = GetCurrentBiomeData();
        if (biomeData != null)
        {
            return biomeData.ArenaLayouts;
        }

        return Array.Empty<Texture2D>();
    }

    private IReadOnlyList<GameObject> GetObstaclePrefabsForCurrentBiome()
    {
        BiomeData biomeData = GetCurrentBiomeData();
        if (biomeData != null)
        {
            return biomeData.ObstaclePrefabs;
        }

        return Array.Empty<GameObject>();
    }

    private IReadOnlyList<BiomeEnemyPoolAvailability> GetEnemyPoolAvailabilitiesForCurrentBiome()
    {
        BiomeData biomeData = GetCurrentBiomeData();
        if (biomeData != null)
        {
            return biomeData.EnemyPoolsByArenaCount;
        }

        return Array.Empty<BiomeEnemyPoolAvailability>();
    }

    private IReadOnlyList<GameObject> GetSpawnableEnemiesForCurrentBiome()
    {
        BiomeData biomeData = GetCurrentBiomeData();
        if (biomeData != null)
        {
            return biomeData.SpawnableEnemies;
        }

        return Array.Empty<GameObject>();
    }

    private void ApplyCurrentBiomeVisuals()
    {
        BiomeData biomeData = GetCurrentBiomeData();
        if (biomeData == null)
        {
            return;
        }

        if (backgroundDecorsRenderer != null && biomeData.BackgroundDecorSprite != null)
        {
            backgroundDecorsRenderer.sprite = biomeData.BackgroundDecorSprite;
        }

        if (boardModelRenderer == null)
        {
            if (gridRenderer != null)
            {
                ApplyColorToRenderer(gridRenderer, 0, biomeData.GroundColor);
            }

            return;
        }

        ApplyColorToRenderer(boardModelRenderer, 0, biomeData.GroundColor);
        ApplyColorToRenderer(boardModelRenderer, 1, biomeData.CliffColor);

        if (gridRenderer != null)
        {
            ApplyColorToRenderer(gridRenderer, 0, biomeData.GroundColor);
        }
    }

    private void ApplyBiomeRockColor(GameObject obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        BiomeData biomeData = GetCurrentBiomeData();
        if (biomeData == null)
        {
            return;
        }

        RockBiomeColorTarget[] colorTargets = obstacle.GetComponentsInChildren<RockBiomeColorTarget>(true);
        for (int index = 0; index < colorTargets.Length; index++)
        {
            RockBiomeColorTarget colorTarget = colorTargets[index];
            if (colorTarget != null)
            {
                colorTarget.ApplyColor(biomeData.RockColor);
            }
        }
    }

    private void ApplyColorToRenderer(Renderer targetRenderer, int materialIndex, Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

        int clampedMaterialIndex = Mathf.Clamp(materialIndex, 0, Mathf.Max(0, targetRenderer.sharedMaterials.Length - 1));
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock, clampedMaterialIndex);
        propertyBlock.SetColor(ColorShaderProperty, color);
        targetRenderer.SetPropertyBlock(propertyBlock, clampedMaterialIndex);
    }

    private static void AddNonNullPrefabs(List<GameObject> target, IReadOnlyList<GameObject> source)
    {
        if (target == null || source == null)
        {
            return;
        }

        for (int index = 0; index < source.Count; index++)
        {
            GameObject prefab = source[index];
            if (prefab != null)
            {
                target.Add(prefab);
            }
        }
    }

    private List<Vector2Int> GetAvailableExtraEnemySpawnCells()
    {
        List<Vector2Int> availableCells = new List<Vector2Int>();
        AddAvailableSpawnCells(currentEnemySpawnCells, availableCells);
        if (availableCells.Count > 0)
        {
            return availableCells;
        }

        for (int x = 0; x < BoardWidth; x++)
        {
            for (int y = 0; y < BoardHeight; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (IsCellWalkable(candidate))
                {
                    availableCells.Add(candidate);
                }
            }
        }

        return availableCells;
    }

    private void AddAvailableSpawnCells(IReadOnlyList<Vector2Int> sourceCells, List<Vector2Int> targetCells)
    {
        if (sourceCells == null || targetCells == null)
        {
            return;
        }

        for (int index = 0; index < sourceCells.Count; index++)
        {
            Vector2Int candidate = sourceCells[index];
            if (IsCellWalkable(candidate))
            {
                targetCells.Add(candidate);
            }
        }
    }
}
