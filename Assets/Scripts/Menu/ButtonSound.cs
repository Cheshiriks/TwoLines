using UnityEngine;

public class ButtonSound : MonoBehaviour
{
    public static ButtonSound Instance;
    private AudioSource _audioComponent;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
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
