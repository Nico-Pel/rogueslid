using UnityEngine;

public sealed class ZhuangJadeColumn : MonoBehaviour
{
    private Character owner;
    private ZhuangJadeColumnAbility ability;
    private Rock rock;
    private Vector2Int cell;
    public void Initialize(Character character, ZhuangJadeColumnAbility source, Rock obstacle, Vector2Int position)
    {
        owner = character;
        ability = source;
        rock = obstacle;
        cell = position;
        rock.Broken += OnBroken;
    }
    public void Expire() { if (rock != null) rock.TakeHit(); }
    private void OnBroken() { if (owner != null && owner.CurrentHealth > 0) ability.Collapse(owner, cell); }
    private void OnDestroy() { if (rock != null) rock.Broken -= OnBroken; }
}
