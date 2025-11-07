using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class Language : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern string GetLang();
    
    public static Language Instance;
    public string currentLanguage = "ru";

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
            currentLanguage = GetLang();
        }
        catch (Exception e)
        {
            currentLanguage = "ru";
        }
    }
}
