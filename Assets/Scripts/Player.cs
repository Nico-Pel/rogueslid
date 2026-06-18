using UnityEngine;

public class Player : MonoBehaviour
{
    public Character ControlledCharacter { get; private set; }

    public bool HasCharacter => ControlledCharacter != null;

    public void AssignCharacter(Character character)
    {
        if (ControlledCharacter != null && ControlledCharacter != character)
        {
            ControlledCharacter.SetSelected(false);
        }

        ControlledCharacter = character;
        if (ControlledCharacter != null)
        {
            ControlledCharacter.SetSelected(true);
        }
    }

    public void ResetTurn()
    {
        if (ControlledCharacter != null)
        {
            ControlledCharacter.ResetTurn();
        }
    }

    public bool CanStillAct()
    {
        return ControlledCharacter != null && ControlledCharacter.CanAct;
    }
}
