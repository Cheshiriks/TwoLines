using TMPro;
using UnityEngine;

public class TextLang : MonoBehaviour
{
    public string ButtonRus;
    public string ButtonTur;
    public string ButtonEng;

    private TextMeshProUGUI textMash;
    
    private void Start()
    {
        textMash = GetComponent<TextMeshProUGUI>();
    }
    
    private void Update()
    {
        if (Language.Instance.currentLanguage == "ru")
        {
            textMash.text = ButtonRus;
        }
        else
        {
            textMash.text = ButtonEng;
        }
    }
}
