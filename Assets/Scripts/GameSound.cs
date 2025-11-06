using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class GameSound : MonoBehaviour
{
    private AudioSource _audioComponent;
    
    [DllImport("__Internal")]
    private static extern string GameplayStart();

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
    
    public void PlayAndYandex()
    {
        if (SaveGame.SoundOn)
        {
            _audioComponent.Play();
            YandexStartGameplay();
        }
    }
    
    private void YandexStartGameplay()
    {
        try
        {
            GameplayStart();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
