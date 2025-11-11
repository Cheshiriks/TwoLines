using UnityEngine;

public class GameSound : MonoBehaviour
{
    private AudioSource _audioComponent;

    private void Start()
    {
        _audioComponent = GetComponent<AudioSource>();
        if (SaveGame.SoundOn)
        {
            _audioComponent.Play();
        }
    }

    public void Pause()
    {
        _audioComponent.Pause();
    }

    public void Play()
    {
        if (SaveGame.SoundOn)
        {
            _audioComponent.Play();
        }
    }
    
}
