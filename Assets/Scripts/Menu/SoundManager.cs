using UnityEngine;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    public Sprite soundOn;
    public Sprite soundOff;

    private Image _image;

    private void Start()
    {
        _image = GetComponent<Image>();
        _image.sprite = SaveGame.SoundOn ? soundOn : soundOff;
    }
    
    public void ChangeSound()
    {
        if (SaveGame.SoundOn)
        {
            _image.sprite = soundOff;
            SaveGame.SoundOn = false;
            //FindObjectOfType<GameSound>().Pause();
        }
        else
        {
            _image.sprite = soundOn;
            SaveGame.SoundOn = true;
            //FindObjectOfType<GameSound>().Play();
        }
    }
}
