using UnityEngine;

public abstract class BoardHazard : MonoBehaviour
{
    protected BoardManager board;
    protected Character owner;
    protected Vector2Int gridPosition;

    public BoardManager Board => board;
    public Character Owner => owner;
    public Vector2Int GridPosition => gridPosition;
    public virtual bool IsVisibleToEnemies => false;

    public virtual void Assign(BoardManager targetBoard, Character targetOwner, Vector2Int targetGridPosition)
    {
        board = targetBoard;
        owner = targetOwner;
        gridPosition = targetGridPosition;
    }

    public virtual void HandlePlayerTurnStarted()
    {
    }

    public virtual void HandleEnemyTurnStarted()
    {
    }

    public virtual void HandleEnemyTurnEnded()
    {
    }

    public virtual int GetEnemyPathPenalty(Enemy enemy)
    {
        return 0;
    }

    public virtual void HandleCharacterEntered(Character character)
    {
    }

    public virtual void HandleCharacterTurnEnded(Character character)
    {
    }

    public virtual bool WouldKillEnemy(Enemy enemy)
    {
        return false;
    }

    public virtual void HandleEnemyEntered(Enemy enemy)
    {
    }

    public virtual void HandleEnemyExited(Enemy enemy)
    {
    }
}
