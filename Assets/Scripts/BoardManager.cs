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

    public bool HasBlockingTerrain => !Walkable || Type == BoardCellType.Rock || StaticObstacle != null;

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
        Type = cellType;
        Walkable = false;
    }
}

public class BoardManager : MonoBehaviour
{
    [System.Serializable]
    private class EnemyPoolAvailability
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

    private const int BoardWidth = 7;
    private const int BoardHeight = 10;

    [Header("Board")]
    [SerializeField] [ReadOnly] private int arenaCount = 1;
    [SerializeField] private Texture2D arenaLayout;
    [SerializeField] private List<Texture2D> arenaLayouts = new List<Texture2D>();
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
    [SerializeField] private List<GameObject> obstacles = new List<GameObject>();

    [Header("Spawn Rules")]
    [SerializeField] private List<EnemyPoolAvailability> enemyPoolsByArenaCount = new List<EnemyPoolAvailability>();
#if UNITY_EDITOR
    [Header("Editor Debug")]
    [SerializeField] private EnemyPoolDefinition forcedEnemyPoolDefinition;
#endif

    [Header("Rewards")]
    [SerializeField] private List<ItemRewardDefinition> itemRewardDefinitions = new List<ItemRewardDefinition>();

    [Header("Special Encounters")]
    [SerializeField] private List<GameObject> spawnableEnemies = new List<GameObject>();
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

    public int Width => BoardWidth;
    public int Height => BoardHeight;
    public BoardCell[,] Cells => cells;
    public Player Player => player;
    public IReadOnlyList<Enemy> SpawnedEnemies => spawnedEnemies;
    public int ArenaCount => arenaCount;
    public PlayerRunRewardState RunRewardState => runRewardState;
    public float ExtraEnemySpawnDelay => Mathf.Max(0f, extraEnemySpawnDelay);
    public event Action AllEnemiesDefeated;

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
        Texture2D selectedArenaLayout = SelectArenaLayout();
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
        arenaLayout = currentArenaLayout;

        EnsureGeneratedRoots();
        ClearGeneratedContent();
        InitializeCells();
        currentEnemySpawnCells.Clear();
        currentArenaEnemyPrefabs.Clear();
        bonusRewardForCurrentArena = false;

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
        arenaCount = 1;
        hasGeneratedBoardThisSession = false;
        runRewardState = new PlayerRunRewardState();
    }

    public void GenerateNextArena()
    {
        if (hasGeneratedBoardThisSession)
        {
            arenaCount++;
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
            next += direction;
        }

        return lastFreeCell;
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

        Vector2Int enemyPosition = enemy.GridPosition;
        if (TryGetCell(enemyPosition, out BoardCell cell) && cell.Occupant == enemy.gameObject)
        {
            cell.ClearOccupant();
        }

        if (player != null && player.ControlledCharacter != null)
        {
            player.ControlledCharacter.HandleEnemyCountChanged(spawnedEnemies.Count);
        }

        if (Application.isPlaying && spawnedEnemies.Count == 0)
        {
            AllEnemiesDefeated?.Invoke();
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
                    Occupant = null
                };
            }
        }
    }

    private Texture2D SelectArenaLayout()
    {
        if (arenaLayouts != null && arenaLayouts.Count > 0)
        {
            List<Texture2D> validArenas = new List<Texture2D>();
            for (int index = 0; index < arenaLayouts.Count; index++)
            {
                if (arenaLayouts[index] != null)
                {
                    validArenas.Add(arenaLayouts[index]);
                }
            }

            if (validArenas.Count > 0)
            {
                return validArenas[UnityEngine.Random.Range(0, validArenas.Count)];
            }
        }

        return arenaLayout;
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
        GameObject obstacle = InstantiateFromList(obstacles, $"Obstacle_{cell.GridPosition.x}_{cell.GridPosition.y}", obstaclesRoot, cell.WorldPosition);
        cell.SetStaticObstacle(obstacle, BoardCellType.Rock);
        if (obstacle != null)
        {
            BarrelObstacle barrel = obstacle.GetComponent<BarrelObstacle>();
            if (barrel != null)
            {
                barrel.Assign(this, cell.GridPosition);
            }
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

    private GameObject InstantiateFromList(List<GameObject> prefabs, string fallbackName, Transform parent, Vector3 position)
    {
        GameObject prefab = prefabs.Count > 0 ? prefabs[UnityEngine.Random.Range(0, prefabs.Count)] : null;
        return InstantiateOrCreate(prefab, fallbackName, parent, position);
    }

    private List<GameObject> ResolveEnemyPrefabsForCurrentArena()
    {
#if UNITY_EDITOR
        if (forcedEnemyPoolDefinition != null)
        {
            return ExtractValidEnemyPrefabs(forcedEnemyPoolDefinition);
        }
#endif

        EnemyPoolAvailability selectedAvailability = null;
        int selectedRangeSize = int.MaxValue;

        for (int index = 0; index < enemyPoolsByArenaCount.Count; index++)
        {
            EnemyPoolAvailability availability = enemyPoolsByArenaCount[index];
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
            return new List<GameObject>();
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
            return new List<GameObject>();
        }

        EnemyPoolDefinition selectedPool = eligiblePools[UnityEngine.Random.Range(0, eligiblePools.Count)];
        return ExtractValidEnemyPrefabs(selectedPool);
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
        if (runRewardState == null)
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
        AddNonNullPrefabs(candidateEnemies, spawnableEnemies);
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
