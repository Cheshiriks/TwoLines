using UnityEngine;

public enum BonusKind
{
    SpeedUp,        // ускорить собравшего (по умолчанию +30%)
    SpeedDown,      // замедлить собравшего (по умолчанию -30%)
    Invulnerability,// неуязвимость к линиям
    PenOff          // выключить рисование (как при разрыве)
}

/// Простой тэг на инстансе бонуса: хранит его тип
public class BonusTag : MonoBehaviour
{
    public BonusKind kind;
}
