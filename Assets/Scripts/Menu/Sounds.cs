using UnityEngine;

public class Sounds : MonoBehaviour
{
    private AudioSource _audioComponent;
    
    private void Start()
    {
        _audioComponent = GetComponent<AudioSource>();
    }
    
    public void Play()
    {
        if (SaveGame.SoundOn)
        {
            _audioComponent.Play();
        }
    }
}
