using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamnedCircleHazard : BoardHazard
{
    private CircleOfTheDamnedAbility sourceAbility;
    private CharacterAbilityRuntime sourceRuntime;
    private GameObject circleVisualPrefab;
    private GameObject circleVisualInstance;
    private TMP_Text magicCountText;
    private GameObject chargeGainFxPrefab;
    private float chargeGainFxDestroyAfterSeconds = 1.5f;
    private GameObject chargeGainSoundParametersPrefab;
    private GameObject explosionFxPrefab;
    private float explosionFxDestroyAfterSeconds = 2f;
    private GameObject damnedBlastShockwaveFxPrefab;
    private float damnedBlastShockwaveFxDestroyAfterSeconds = 2f;
    private int charges;
    private int blastRange = 1;
    private bool waveEnabled;

    public int Charges => Mathf.Max(0, charges);

    public void Configure(
        BoardManager targetBoard,
        Character targetOwner,
        CharacterAbilityRuntime targetRuntime,
        Vector2Int targetGridPosition,
        CircleOfTheDamnedAbility abilityDefinition,
        GameObject visualPrefab,
        GameObject chargeGainFx,
        float chargeGainFxLifetime,
        GameObject chargeGainSoundPrefab,
        GameObject explosionPrefab,
        float explosionFxLifetime,
        GameObject damnedBlastShockwaveFx,
        float damnedBlastShockwaveFxLifetime,
        int startingCharges,
        int explosionRange,
        bool enableWave)
    {
        Assign(targetBoard, targetOwner, targetGridPosition);
        sourceAbility = abilityDefinition;
        sourceRuntime = targetRuntime;
        circleVisualPrefab = visualPrefab;
        chargeGainFxPrefab = chargeGainFx;
        chargeGainFxDestroyAfterSeconds = Mathf.Max(0f, chargeGainFxLifetime);
        chargeGainSoundParametersPrefab = chargeGainSoundPrefab;
        explosionFxPrefab = explosionPrefab;
        explosionFxDestroyAfterSeconds = Mathf.Max(0f, explosionFxLifetime);
        damnedBlastShockwaveFxPrefab = damnedBlastShockwaveFx;
        damnedBlastShockwaveFxDestroyAfterSeconds = Mathf.Max(0f, damnedBlastShockwaveFxLifetime);
        charges = Mathf.Max(1, startingCharges);
        blastRange = Mathf.Max(1, explosionRange);
        waveEnabled = enableWave;

        transform.SetParent(targetBoard.transform, true);
        transform.position = targetBoard.GridToWorldPosition(targetGridPosition);
        targetBoard.RegisterHazard(this, targetGridPosition);
        EnsureVisual();
        RefreshMagicCountText();
    }

    public override void HandleCharacterEntered(Character character)
    {
        if (character == null || owner == null || character != owner)
        {
            return;
        }

        charges++;
        RefreshMagicCountText();
        PlayChargeGainFeedback();
        sourceAbility?.HandleCircleChargesChanged(sourceRuntime);
        if (waveEnabled)
        {
            owner.DamageEnemiesAround(gridPosition, 1, 1, true, sourceAbility);
        }
    }

    public void Explode()
    {
        if (owner == null || board == null)
        {
            DestroyHazard();
            return;
        }

        int damage = Mathf.Max(1, charges);
        PlayExplosionFx();
        PlayDamnedBlastShockwaveFx();
        ApplyExplosionDamage(damage);
        DestroyHazard();
    }

    private void ApplyExplosionDamage(int damage)
    {
        for (int offsetX = -blastRange; offsetX <= blastRange; offsetX++)
        {
            for (int offsetY = -blastRange; offsetY <= blastRange; offsetY++)
            {
                if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) > blastRange)
                {
                    continue;
                }

                Vector2Int targetCell = gridPosition + new Vector2Int(offsetX, offsetY);
                if (board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
                {
                    owner.DealDamageToEnemy(enemy, damage, false, true, DamageSoundType.MagicHit, sourceAbility);
                }
                else if (board.TryGetLichSkullObject(targetCell, out LichSkullObject lichSkull) && lichSkull != null)
                {
                    owner.DealDamageToLichSkull(lichSkull, damage, false, DamageSoundType.MagicHit, sourceAbility);
                }
            }
        }
    }

    private void EnsureVisual()
    {
        if (circleVisualInstance != null || circleVisualPrefab == null)
        {
            return;
        }

        circleVisualInstance = Instantiate(circleVisualPrefab, transform.position, circleVisualPrefab.transform.rotation, transform);
        circleVisualInstance.transform.localScale = circleVisualPrefab.transform.localScale;
        magicCountText = FindMagicCountText(circleVisualInstance.transform);
    }

    private void RefreshMagicCountText()
    {
        if (magicCountText == null && circleVisualInstance != null)
        {
            magicCountText = FindMagicCountText(circleVisualInstance.transform);
        }

        if (magicCountText != null)
        {
            magicCountText.text = Charges.ToString();
        }
    }

    private void PlayExplosionFx()
    {
        if (explosionFxPrefab == null)
        {
            return;
        }

        GameObject spawnedFx = Instantiate(explosionFxPrefab, transform.position, explosionFxPrefab.transform.rotation);
        if (blastRange > 1)
        {
            spawnedFx.transform.localScale = explosionFxPrefab.transform.localScale * 2f;
        }
        else
        {
            spawnedFx.transform.localScale = explosionFxPrefab.transform.localScale;
        }

        if (explosionFxDestroyAfterSeconds > 0f)
        {
            Destroy(spawnedFx, explosionFxDestroyAfterSeconds);
        }
    }

    private void PlayChargeGainFeedback()
    {
        if (circleVisualInstance != null)
        {
            Transform visualTransform = circleVisualInstance.transform;
            visualTransform.DOKill(false);
            visualTransform.DOPunchScale(Vector3.one * 0.18f, 0.28f, 1, 0.5f);
        }

        if (chargeGainFxPrefab != null)
        {
            GameObject spawnedFx = Instantiate(chargeGainFxPrefab, transform.position, chargeGainFxPrefab.transform.rotation);
            if (chargeGainFxDestroyAfterSeconds > 0f)
            {
                Destroy(spawnedFx, chargeGainFxDestroyAfterSeconds);
            }
        }

        if (chargeGainSoundParametersPrefab != null)
        {
            GameObject soundObject = Instantiate(
                chargeGainSoundParametersPrefab,
                transform.position,
                chargeGainSoundParametersPrefab.transform.rotation);
            soundObject.transform.localScale = chargeGainSoundParametersPrefab.transform.localScale;

            SoundParameters soundParameters = soundObject.GetComponent<SoundParameters>();
            if (soundParameters != null)
            {
                soundParameters.PlaySound(transform.position);
            }
        }
    }

    private void PlayDamnedBlastShockwaveFx()
    {
        if (blastRange <= 1 || damnedBlastShockwaveFxPrefab == null)
        {
            return;
        }

        GameObject spawnedFx = Instantiate(damnedBlastShockwaveFxPrefab, transform.position, damnedBlastShockwaveFxPrefab.transform.rotation);
        if (damnedBlastShockwaveFxDestroyAfterSeconds > 0f)
        {
            Destroy(spawnedFx, damnedBlastShockwaveFxDestroyAfterSeconds);
        }
    }

    private static TMP_Text FindMagicCountText(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] textComponents = root.GetComponentsInChildren<TMP_Text>(true);
        for (int index = 0; index < textComponents.Length; index++)
        {
            TMP_Text textComponent = textComponents[index];
            if (textComponent != null && textComponent.name == "tMagicCount")
            {
                return textComponent;
            }
        }

        return null;
    }

    private void DestroyHazard()
    {
        if (board != null)
        {
            board.UnregisterHazard(this);
        }

        sourceAbility?.HandleCircleDestroyed(sourceRuntime, this);

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }
}
