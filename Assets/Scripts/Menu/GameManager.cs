using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public void ClassicGame()
    {
        SaveGame.Instance.ChangeBonusSystem(false);
        SceneManager.LoadScene(1);
    }
    
    public void ArcadeGame()
    {
        SaveGame.Instance.ChangeBonusSystem(true);
        SceneManager.LoadScene(1);
    }
}
