using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMenuManager : MonoBehaviour
{
    
    public void Continue()
    {
        SceneManager.LoadScene(1);
    }
    
    public void Menu()
    {
        SaveGame.Instance.NewGame();
        SceneManager.LoadScene(0);
    }
}
