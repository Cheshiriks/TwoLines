using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class Yandex : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void GameplayStart();
    
    [DllImport("__Internal")]
    private static extern string GameplayStop();
    
    void Start()
    {
        try
        {
            GameplayStart();
        }
        catch (Exception e)
        {
            Debug.Log("GameplayStart Error: " + e.Message);
            Console.WriteLine(e);
        }
    }
    
    public void YandexStopGameplay()
    {
        try
        {
            GameplayStop();
        }
        catch (Exception e)
        {
            Debug.Log("GameplayStop Error: " + e.Message);
            Console.WriteLine(e);
        }
    }
}
