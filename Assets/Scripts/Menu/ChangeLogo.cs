using UnityEngine;
using Image = UnityEngine.UI.Image;

public class ChangeLogo : MonoBehaviour
{
    public Sprite rusLogoCanvas;
    public Sprite engLogoCanvas;

    private Image _logo;
    private bool _isRus;

    void Start()
    {
        _logo = GetComponent<Image>();
        
        if (Language.Instance.currentLanguage == "ru")
        {
            _logo.sprite = rusLogoCanvas;
            _isRus = true;
        }
        else
        {
            _logo.sprite = engLogoCanvas;
        }
    }
    
    private void Update()
    {
        if (Language.Instance.currentLanguage == "ru")
        {
            if (!_isRus)
            {
                _logo.sprite = rusLogoCanvas;
                _isRus = true;
            }
        }
        else
        {
            if (_isRus)
            {
                _logo.sprite = engLogoCanvas;
                _isRus = false;
            }
        }
    }
}
