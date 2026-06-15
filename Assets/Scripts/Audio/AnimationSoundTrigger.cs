using UnityEngine;

public class AnimationSoundTrigger : MonoBehaviour
{
    [SerializeField] private SoundParameters soundParameters;
    [SerializeField] private AudioClip clip;
    [SerializeField] private float volume = 1f;
    [SerializeField] private float pitch = 1f;
    [SerializeField] private float range = 20f;
    [SerializeField] private bool playOnCamera = true;

    public void PlaySound()
    {
        if (clip != null && SoundManager.Instance != null)
        {
            if (playOnCamera)
            {
                SoundManager.Instance.Play2DSound(clip, volume, pitch);
            }
            else
            {
                SoundManager.Instance.PlaySound(clip, transform.position, volume, pitch, -1f, range);
            }
        }

        if (soundParameters != null)
        {
            soundParameters.PlaySound();
        }
    }
}
