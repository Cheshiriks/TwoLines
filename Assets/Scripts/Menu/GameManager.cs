using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    
    [Header("UI")]
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameObject ruleCanvas;
    [SerializeField] private GameObject colorCanvas;
    [SerializeField] private GameObject classicCanvas;
    [SerializeField] private GameObject arcadeCanvas;
    
    public void ClassicGame()
    {
        SaveGame.Instance.ChangeBonusSystem(false);
        if (SaveGame.Instance.isClassicEducation)
        {
            SceneManager.LoadScene(1);
        }
        else
        {
            menuCanvas.SetActive(false);
            colorCanvas.SetActive(false);
            ruleCanvas.SetActive(true);
            classicCanvas.SetActive(true);
            arcadeCanvas.SetActive(false);
            
            SaveGame.Instance.SetClassicEducation();
        }
    }
    
    public void ArcadeGame()
    {
        SaveGame.Instance.ChangeBonusSystem(true);
        if (SaveGame.Instance.isArcadeEducation)
        {
            SceneManager.LoadScene(1);
        }
        else
        {
            menuCanvas.SetActive(false);
            colorCanvas.SetActive(false);
            ruleCanvas.SetActive(true);
            classicCanvas.SetActive(false);
            arcadeCanvas.SetActive(true);
            
            SaveGame.Instance.SetArcadeEducation();
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(1);
    }

    public void PlayButtonSound()
    {
        FindObjectOfType<ButtonSound>().Play();
    }
    
    public void OpenColorCanvas()
    {
        menuCanvas.SetActive(false);
        ruleCanvas.SetActive(false);
        colorCanvas.SetActive(true);
    }
    
    public void CloseColorCanvas()
    {
        menuCanvas.SetActive(true);
        ruleCanvas.SetActive(false);
        colorCanvas.SetActive(false);
    }
}
