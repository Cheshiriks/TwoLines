using UnityEngine;

public class ButtonColor : MonoBehaviour
{
    [SerializeField] private GameObject tick1;
    [SerializeField] private GameObject tick2;
    [SerializeField] private GameObject tick3;
    [SerializeField] private int id;

    void Start()
    {
        StartColor();
    }

    public void StartColor()
    {
        switch (SaveGame.Instance.colorsId)
        {
            case 1:
                tick1.SetActive(true);
                tick2.SetActive(false);
                tick3.SetActive(false);
                break;
            case 2:
                tick1.SetActive(false);
                tick2.SetActive(true);
                tick3.SetActive(false);
                break;
            case 3:
                tick1.SetActive(false);
                tick2.SetActive(false);
                tick3.SetActive(true);
                break;
            default:
                tick1.SetActive(true);
                tick2.SetActive(false);
                tick3.SetActive(false);
                break;
        }
    }

    public void ChangeColor()
    {
        SaveGame.Instance.colorsId = id;
        StartColor();
    }
}
