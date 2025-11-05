using UnityEngine;

public class SaveGame : MonoBehaviour
{
    public static bool SoundOn = true;
    public static bool IsBonusSystem = true;
    
    public bool isClassicEducation = false;
    public bool isArcadeEducation = false;
    
    public int scoreFirst = 0;
    public int scoreSecond = 0;
    public float maxGameTime = 0f;

    public static SaveGame Instance;
    
    private void Awake()
    {
        if (Instance == null)
        {
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
            Instance = this;
            LoadDate();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeBonusSystem(bool isBonus)
    {
        IsBonusSystem = isBonus;
    }

    public void NewGame()
    {
        scoreFirst = 0;
        scoreSecond = 0;
    }
    
    public void AddScore(int? loserId)
    {
        if (loserId != null)
        {
            if (loserId == 2)
                scoreFirst++;
            else
                scoreSecond++;
        }
    }

    public void SetClassicEducation()
    {
        isClassicEducation = true;
        PlayerPrefs.SetString("isClassicEducation", "true");
    }
    
    public void SetArcadeEducation()
    {
        isArcadeEducation = true;
        PlayerPrefs.SetString("isArcadeEducation", "true");
    }

    public void SetNewGameTime(float matchTime)
    {
        if (matchTime > maxGameTime)
        {
            maxGameTime = matchTime;
            PlayerPrefs.SetFloat("maxGameTime", maxGameTime);
        }
    }
    
    public void LoadDate()
    {
        if (PlayerPrefs.HasKey("maxGameTime"))
        {
            maxGameTime = PlayerPrefs.GetFloat("maxGameTime");
        }
        if (PlayerPrefs.HasKey("isClassicEducation"))
        {
            isClassicEducation = PlayerPrefs.GetString("isClassicEducation") == "true";
        }
        if (PlayerPrefs.HasKey("isArcadeEducation"))
        {
            isArcadeEducation = PlayerPrefs.GetString("isArcadeEducation") == "true";
        }
    }
    
}
