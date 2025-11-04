using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class Language : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern string GetLang();
    
    [DllImport("__Internal")]
    private static extern string GetDomen();

    [DllImport("__Internal")]
    private static extern void GetStart();
    
    public static Language Instance;
    public string currentLanguage = "ru";
    public string currentDomen = "ru";

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
        try
        {
            GetStartApi();
            currentLanguage = GetLang();
            currentDomen = GetDomen();
        }
        catch (Exception e)
        {
            currentLanguage = "ru";
            currentDomen = "ru";
        }
    }

    private void GetStartApi()
    {
        try
        {
            GetStart();
        }
        catch (Exception e)
        {
            Debug.Log($"Метод ysdk.features.LoadingAPI.ready() недоступен");
        }
    }
}
