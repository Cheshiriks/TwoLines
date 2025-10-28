using UnityEngine;

public class SaveGame : MonoBehaviour
{
    public static bool SoundOn = true;
    public static bool IsBonusSystem = true;

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
}
