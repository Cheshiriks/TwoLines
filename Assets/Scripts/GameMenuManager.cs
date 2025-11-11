using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMenuManager : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern string ShowAdv();
    
    [DllImport("__Internal")]
    private static extern string ShowAdvMenu();
    
    public void Continue()
    {
        ShowAdYandexContinue();
    }
    
    public void Menu()
    {
        SaveGame.Instance.NewGame();
        ShowAdYandexMenu();
    }
    
    public void PlayButtonSound()
    {
        FindObjectOfType<ButtonSound>().Play();
    }
    
    public void ShowAdYandexContinue()
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
            SceneManager.LoadScene(1);
        }
    }
    
    public void ShowAdYandexMenu()
    {
        try
        {
            FindObjectOfType<GameSound>().Pause();
            ShowAdvMenu();
        }
        catch
        {
            FindObjectOfType<GameSound>().Play();
            Debug.Log("Реклама не доступна");
            SceneManager.LoadScene(0);
        }
    }
    
    public void AdContinue()
    {
        FindObjectOfType<GameSound>().Play();
        SceneManager.LoadScene(1);
    }
    
    public void AdMenu()
    {
        FindObjectOfType<GameSound>().Play();
        SceneManager.LoadScene(0);
    }
    
}
