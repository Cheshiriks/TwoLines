using UnityEngine;

public class SaveGame : MonoBehaviour
{
    public static bool SoundOn = true;
    public static bool IsBonusSystem = true;
    
    public int scoreFirst = 0;
    public int scoreSecond = 0;

    public static SaveGame Instance;
    
    private void Awake()
    {
        if (Instance == null)
        {
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
            Instance = this;
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
}
