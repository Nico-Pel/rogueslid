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

    public void SetOccupant(GameObject occupant, BoardOccupantKind occupantKind, bool walkable)
    {
        Occupant = occupant;
        OccupantKind = occupantKind;
        IsOccupied = occupant != null;
        Walkable = walkable;
    }

    public void ClearOccupant()
    {
        Occupant = null;
        OccupantKind = BoardOccupantKind.None;
        IsOccupied = false;
        Walkable = Type != BoardCellType.Rock;
    }
}

public class BoardManager : MonoBehaviour
{
    private const int BoardWidth = 7;
    private const int BoardHeight = 10;

    [Header("Board")]
    [SerializeField] private Texture2D arenaLayout;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private float cellSizeX = 1f;
    [SerializeField] private float cellSizeZ = 1f;
    [SerializeField] private int originColumn = 3;
    [SerializeField] private int originRow = 4;
    [SerializeField] private float spawnHeight = 0.08f;

    [Header("Prefabs")]
    [SerializeField] private List<GameObject> obstacles = new List<GameObject>();
    [SerializeField] private List<GameObject> enemies = new List<GameObject>();
    [SerializeField] private List<GameObject> playerCharacters = new List<GameObject>();

    private BoardCell[,] cells;
    private Transform generatedRoot;
    private Transform obstaclesRoot;
    private Transform enemiesRoot;
    private Transform playersRoot;
    private Player player;

    public int Width => BoardWidth;
    public int Height => BoardHeight;
    public BoardCell[,] Cells => cells;

    private enum ArenaMarker
    {
        Empty,
        Obstacle,
        Enemy,
        PlayerStart
    }

    private void Awake()
    {
        if (generateOnStart)
        {
            GenerateBoard();
        }
    }

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        if (arenaLayout == null)
        {
            Debug.LogError("BoardManager requires an arena layout texture.", this);
            return;
        }

        if (!arenaLayout.isReadable)
        {
            Debug.LogError("Arena texture must be readable. Enable Read/Write on the imported texture.", arenaLayout);
            return;
        }

        if (arenaLayout.width != BoardWidth || arenaLayout.height != BoardHeight)
        {
            Debug.LogWarning(
                $"Arena texture should be {BoardWidth}x{BoardHeight}, but is {arenaLayout.width}x{arenaLayout.height}. " +
                "Generation continues with the expected board size.",
                arenaLayout);
        }

        EnsureGeneratedRoots();
        ClearGeneratedContent();
        InitializeCells();

        List<Vector2Int> playerSpawnPoints = new List<Vector2Int>();

        for (int row = 0; row < BoardHeight; row++)
        {
            for (int column = 0; column < BoardWidth; column++)
            {
                BoardCell cell = cells[column, row];
                ArenaMarker marker = GetMarkerForCell(column, row);

                switch (marker)
                {
                    case ArenaMarker.Obstacle:
                        SpawnObstacle(cell);
                        break;
                    case ArenaMarker.Enemy:
                        SpawnEnemy(cell);
                        break;
                    case ArenaMarker.PlayerStart:
                        playerSpawnPoints.Add(cell.GridPosition);
                        break;
                }
            }
        }

        SpawnPlayerCharacters(playerSpawnPoints);
    }

    public bool TryGetCell(Vector2Int gridPosition, out BoardCell cell)
    {
        if (cells == null)
        {
            cell = null;
            return false;
        }

        if (gridPosition.x < 0 || gridPosition.x >= BoardWidth || gridPosition.y < 0 || gridPosition.y >= BoardHeight)
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

    private ArenaMarker GetMarkerForCell(int column, int row)
    {
        Color pixel = arenaLayout.GetPixel(column, (BoardHeight - 1) - row);

        float max = Mathf.Max(pixel.r, pixel.g, pixel.b);
        if (max < 0.2f)
        {
            return ArenaMarker.Obstacle;
        }

        if (pixel.g > 0.45f && pixel.g > pixel.r * 1.2f && pixel.g > pixel.b * 1.2f)
        {
            return ArenaMarker.PlayerStart;
        }

        if (pixel.r > 0.45f && pixel.r > pixel.g * 1.2f && pixel.r > pixel.b * 1.2f)
        {
            return ArenaMarker.Enemy;
        }

        return ArenaMarker.Empty;
    }

    private void SpawnObstacle(BoardCell cell)
    {
        cell.Type = BoardCellType.Rock;
        GameObject obstacle = InstantiateFromList(obstacles, $"Obstacle_{cell.GridPosition.x}_{cell.GridPosition.y}", obstaclesRoot, cell.WorldPosition);
        cell.SetOccupant(obstacle, BoardOccupantKind.Obstacle, false);
    }

    private void SpawnEnemy(BoardCell cell)
    {
        GameObject enemyObject = InstantiateFromList(enemies, $"Enemy_{cell.GridPosition.x}_{cell.GridPosition.y}", enemiesRoot, cell.WorldPosition + Vector3.up * spawnHeight);
        Enemy enemy = GetOrAddComponent<Enemy>(enemyObject);
        enemy.Assign(cell.GridPosition, this);
        cell.SetOccupant(enemyObject, BoardOccupantKind.Enemy, false);
    }

    private void SpawnPlayerCharacters(List<Vector2Int> spawnPoints)
    {
        if (spawnPoints.Count == 0)
        {
            return;
        }

        GameObject playerObject = new GameObject("Player");
        playerObject.transform.SetParent(playersRoot, false);
        player = playerObject.AddComponent<Player>();

        for (int index = 0; index < spawnPoints.Count; index++)
        {
            Vector2Int spawnPoint = spawnPoints[index];
            BoardCell cell = cells[spawnPoint.x, spawnPoint.y];

            GameObject prefab = playerCharacters.Count > 0
                ? playerCharacters[Mathf.Min(index, playerCharacters.Count - 1)]
                : null;

            string fallbackName = $"Character_{index + 1}_{spawnPoint.x}_{spawnPoint.y}";
            GameObject characterObject = InstantiateOrCreate(prefab, fallbackName, playerObject.transform, cell.WorldPosition + Vector3.up * spawnHeight);

            Character character = GetOrAddComponent<Character>(characterObject);
            character.Assign(player, index, spawnPoint, this);
            player.RegisterCharacter(character);

            cell.SetOccupant(characterObject, BoardOccupantKind.PlayerCharacter, false);
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
        GameObject prefab = prefabs.Count > 0 ? prefabs[Random.Range(0, prefabs.Count)] : null;
        return InstantiateOrCreate(prefab, fallbackName, parent, position);
    }

    private GameObject InstantiateOrCreate(GameObject prefab, string fallbackName, Transform parent, Vector3 position)
    {
        GameObject instance;
        if (prefab != null)
        {
            instance = Instantiate(prefab, position, prefab.transform.rotation, parent);
            instance.name = prefab.name;
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
}
