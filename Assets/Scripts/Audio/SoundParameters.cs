using UnityEngine;
using Random = UnityEngine.Random;

public class SoundParameters : MonoBehaviour
{
    [SerializeField] private bool playOnEnabled;
    [SerializeField] private bool isUiSound;
    [SerializeField] private AudioClip[] possibleClips;
    [SerializeField] private float volume = 1f;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;
    [SerializeField] private float duration = -1f;
    [SerializeField] private float range = 15f;
    [SerializeField] private bool loop;
    [SerializeField] private Transform sourceParent;

    private int lastPlayedClipIndex = -1;

    private void OnEnable()
    {
        if (playOnEnabled)
        {
            PlaySound();
        }
    }

    public void PlaySound()
    {
        PlaySound(transform.position);
    }

    public void PlaySound(Vector3 position, float forcedVolume = -1f)
    {
        AudioClip clip = GetRandomClip();
        if (clip == null || SoundManager.Instance == null)
        {
            return;
        }

        float appliedVolume = forcedVolume >= 0f ? forcedVolume : volume;
        float pitch = Random.Range(pitchMin, pitchMax);

        if (isUiSound)
        {
            SoundManager.Instance.PlayUiSound(clip, appliedVolume, pitch);
            return;
        }

        SoundManager.Instance.PlaySound(clip, position, appliedVolume, pitch, duration, range, loop, sourceParent);
    }

    private AudioClip GetRandomClip()
    {
        if (possibleClips == null || possibleClips.Length == 0)
        {
            return null;
        }

        int validClipCount = 0;
        for (int index = 0; index < possibleClips.Length; index++)
        {
            if (possibleClips[index] != null)
            {
                validClipCount++;
            }
        }

        if (validClipCount == 0)
        {
            return null;
        }

        int selectedClipOrder = Random.Range(0, validClipCount);
        if (validClipCount > 1 && selectedClipOrder == lastPlayedClipIndex)
        {
            selectedClipOrder = (selectedClipOrder + 1) % validClipCount;
        }

        int currentValidOrder = 0;
        for (int index = 0; index < possibleClips.Length; index++)
        {
            if (possibleClips[index] == null)
            {
                continue;
            }

            if (currentValidOrder == selectedClipOrder)
            {
                lastPlayedClipIndex = currentValidOrder;
                return possibleClips[index];
            }

            currentValidOrder++;
        }

        return null;
    }
}
