using System.Collections.Generic;
using UnityEngine;

public class PlayersColor
{
    public List<Color32> playersColorsFirst = new List<Color32>{
        new Color32(31, 255, 0, 255),
        new Color32(255, 0, 174, 255)
    };
    
    public List<Color32> playersColorsSecond = new List<Color32>{
        Color.red, 
        Color.blue
    };
    
    public List<Color32> playersColorsThird = new List<Color32>{
        Color.yellow, 
        new Color32(132, 0, 255, 255)
    };

    public List<Color32> GetColors(int id)
    {
        switch (id)
        {
            case 1:
                return playersColorsFirst;
            case 2:
                return playersColorsSecond;
            case 3:
                return playersColorsThird;
            default:
                return playersColorsFirst;
        }
    }
}
