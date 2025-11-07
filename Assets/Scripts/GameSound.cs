using System;
using System.Runtime.InteropServices;
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
    
    /*public void PlayAndYandex()
    {
        if (SaveGame.SoundOn)
        {
            _audioComponent.Play();
            YandexStartGameplay();
        }
    }*/
    
}
