using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Lamellar Step", fileName = "LamellarStep")]
public class LamellarStepAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int daggerDamage = 2;
    [SerializeField] private GameObject daggerProjectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float daggerProjectileSpeed = 14f;
    [SerializeField] private Vector3 daggerProjectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 daggerProjectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private GameObject daggerMissImpactSoundPrefab;
    [SerializeField] private GameObject daggerMissImpactFxPrefab;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null || targetCell == character.GridPosition)
        {
            return false;
        }

        if (!character.Board.TryGetCell(targetCell, out BoardCell targetBoardCell) || !targetBoardCell.Walkable || targetBoardCell.IsOccupied)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        int absX = Mathf.Abs(delta.x);
        int absY = Mathf.Abs(delta.y);
        int orthogonalRange = 1 + character.GetUpgradeStacks(AbilityUpgradeKey.LamellarStepExtendedStride);

        bool validOrthogonalMove = (delta.x == 0 || delta.y == 0) && (absX + absY) <= orthogonalRange;
        bool validDiagonalMove = absX == 1 && absY == 1;
        if (!validOrthogonalMove && !validDiagonalMove)
        {
            return false;
        }

        if (validOrthogonalMove)
        {
            Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
            Vector2Int scan = character.GridPosition + direction;
            while (scan != targetCell)
            {
                if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
                {
                    return false;
                }

                scan += direction;
            }
        }

        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        character.StartCoroutine(ResolveLamellarStepSequence(character, runtime, targetCell.Value));
        return true;
    }

    private IEnumerator ResolveLamellarStepSequence(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null)
        {
            yield break;
        }

        character.BeginActionLock();

        bool success = character.TryTeleportTo(targetCell);
        if (!success)
        {
            character.EndActionLock();
            yield break;
        }

        yield return new WaitUntil(() => !character.IsMoving);

        bool suppressDaggers = runtime.ConsumeSuppressNextPrimaryEffectOnce();
        bool hitAnyTarget = false;
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        if (!suppressDaggers)
        {
            bool spectralDaggers = character.GetUpgradeStacks(AbilityUpgradeKey.LamellarStepSpectralDaggers) > 0;
            bool venomDaggers = character.GetUpgradeStacks(AbilityUpgradeKey.LamellarStepVenomDaggers) > 0;
            Vector2Int[] directions =
            {
                new Vector2Int(1, 1),
                new Vector2Int(1, -1),
                new Vector2Int(-1, 1),
                new Vector2Int(-1, -1)
            };

            for (int index = 0; index < directions.Length; index++)
            {
                Vector2Int direction = directions[index];
                Vector2Int impactCell = GetLamellarDaggerImpactCell(character, direction, spectralDaggers);
                bool daggerHitEnemy = false;

                Vector2Int scan = character.GridPosition + direction;
                while (character.Board.IsInsideBoard(scan))
                {
                    if (character.Board.TryGetEnemy(scan, out Enemy enemy) && enemy != null)
                    {
                        int appliedDamage = character.DealDamageToEnemy(enemy, daggerDamage, true, true, DamageSoundType.Sword, this);
                        if (appliedDamage > 0)
                        {
                            hitAnyTarget = true;
                            hitEnemies.Add(enemy);
                            daggerHitEnemy = true;
                            if (venomDaggers)
                            {
                                enemy.ApplyStatusEffect(CombatStatusType.Poisoned, -1, 1);
                            }
                        }

                        if (!spectralDaggers)
                        {
                            break;
                        }
                    }
                    else if (character.Board.TryGetBarrel(scan, out BarrelObstacle barrel) && barrel != null)
                    {
                        barrel.TakeHit();
                        if (!spectralDaggers)
                        {
                            break;
                        }
                    }
                    else if (character.Board.TryGetLichSkullObject(scan, out LichSkullObject lichSkull) && lichSkull != null)
                    {
                        int appliedDamage = character.DealDamageToLichSkull(lichSkull, daggerDamage, true, DamageSoundType.Sword, this);
                        if (appliedDamage > 0)
                        {
                            hitAnyTarget = true;
                            daggerHitEnemy = true;
                        }

                        if (!spectralDaggers)
                        {
                            break;
                        }
                    }
                    else if (character.Board.TryGetCell(scan, out BoardCell cell) && cell.HasBlockingTerrain)
                    {
                        if (!spectralDaggers)
                        {
                            break;
                        }
                    }

                    scan += directions[index];
                }

                TryPlayDaggerProjectile(character, direction, impactCell, !daggerHitEnemy);
            }
        }

        if (hitAnyTarget && character.GetUpgradeStacks(AbilityUpgradeKey.LamellarStepTacticalRetreat) > 0)
        {
            runtime.GrantBonusTurnUse(1);
            runtime.SuppressNextPrimaryEffectOnce();
        }

        PlayConfiguredFx(character, hitEnemies);
        character.EndActionLock();
    }

    private Vector2Int GetLamellarDaggerImpactCell(Character character, Vector2Int direction, bool spectralDaggers)
    {
        if (character == null || character.Board == null || direction == Vector2Int.zero)
        {
            return character != null ? character.GridPosition : Vector2Int.zero;
        }

        Vector2Int lastInsideBoardCell = character.GridPosition;
        Vector2Int scan = character.GridPosition + direction;
        while (character.Board.IsInsideBoard(scan))
        {
            lastInsideBoardCell = scan;

            bool hasEnemy = character.Board.TryGetEnemy(scan, out Enemy enemy) && enemy != null;
            bool hasBarrel = character.Board.TryGetBarrel(scan, out BarrelObstacle barrel) && barrel != null;
            bool hasBlockingTerrain = character.Board.TryGetCell(scan, out BoardCell cell) && cell.HasBlockingTerrain;

            if (hasEnemy || hasBarrel || hasBlockingTerrain)
            {
                if (!spectralDaggers)
                {
                    return scan;
                }
            }

            scan += direction;
        }

        return lastInsideBoardCell;
    }

    private void TryPlayDaggerProjectile(Character character, Vector2Int direction, Vector2Int impactCell, bool playMissImpactSound)
    {
        if (daggerProjectilePrefab == null || character == null || character.Board == null || direction == Vector2Int.zero)
        {
            return;
        }

        character.StartCoroutine(PlayDaggerProjectileRoutine(character, direction, impactCell, playMissImpactSound));
    }

    private IEnumerator PlayDaggerProjectileRoutine(Character character, Vector2Int direction, Vector2Int impactCell, bool playMissImpactSound)
    {
        if (character == null || character.Board == null || daggerProjectilePrefab == null)
        {
            yield break;
        }

        Vector3 startPosition = character.transform.position + daggerProjectileSpawnOffset;
        Vector3 targetPosition = character.Board.GridToWorldPosition(impactCell) + daggerProjectileImpactOffset;
        GameObject projectile = Instantiate(daggerProjectilePrefab, startPosition, Quaternion.identity);
        projectile.transform.localScale = daggerProjectilePrefab.transform.localScale;

        Vector3 travelDirection = targetPosition - startPosition;
        if (travelDirection.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
        }

        float duration = Mathf.Max(0.05f, travelDirection.magnitude / Mathf.Max(0.01f, daggerProjectileSpeed));
        Tween projectileTween = projectile.transform.DOMove(targetPosition, duration).SetEase(Ease.Linear);
        yield return projectileTween.WaitForCompletion();

        if (playMissImpactSound)
        {
            if (daggerMissImpactFxPrefab != null)
            {
                GameObject impactFx = Instantiate(daggerMissImpactFxPrefab, targetPosition, daggerMissImpactFxPrefab.transform.rotation);
                impactFx.transform.localScale = daggerMissImpactFxPrefab.transform.localScale;
            }

            if (daggerMissImpactSoundPrefab != null)
            {
                GameObject soundObject = Instantiate(daggerMissImpactSoundPrefab, targetPosition, daggerMissImpactSoundPrefab.transform.rotation);
                soundObject.transform.localScale = daggerMissImpactSoundPrefab.transform.localScale;

                SoundParameters soundParameters = soundObject.GetComponent<SoundParameters>();
                if (soundParameters != null)
                {
                    soundParameters.PlaySound(targetPosition);
                }
            }
        }

        if (projectile != null)
        {
            Destroy(projectile);
        }
    }
}
