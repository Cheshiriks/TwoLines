using System.Runtime.InteropServices;
using UnityEngine;

public class YandexAdApi : MonoBehaviour
{
    
    [DllImport("__Internal")]
    private static extern string ShowAdv();
    
    public void ShowAdYandex()
    {
        try
        {
            FindObjectOfType<GameSound>().Pause();
            ShowAdv();
        }
        catch
        {
            FindObjectOfType<GameSound>().Play();
            Debug.Log("Реклама не доступна");
        }
    }
    
}
