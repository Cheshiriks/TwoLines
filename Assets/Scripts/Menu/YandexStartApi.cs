using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class YandexStartApi : MonoBehaviour
{
    
    [DllImport("__Internal")]
    private static extern void GetStart();
    
    [DllImport("__Internal")]
    private static extern string ShowAdv();
    
    public static YandexStartApi Instance;
    
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
    
    void Start()
    {
        try
        {
            GetStart();
        }
        catch (Exception e)
        {
            Debug.Log("YandexStartApi Error: " + e.Message);
        }

        ShowFirstAd();
    }

    private void ShowFirstAd()
    {
        try
        {
            FindObjectOfType<GameSound>().Pause();
            ShowAdv();
        }
        catch
        {
            FindObjectOfType<GameSound>().Play();
            Debug.Log("Yandex ShowFirstAd Error");
        }
    }

}
