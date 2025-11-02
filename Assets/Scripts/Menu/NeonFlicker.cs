using System.Collections;
using System.Net.Mime;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MediaTypeNames.Image))]
public class NeonFlicker : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Image target;           // Если не задано — возьмём с этого объекта

    [Header("Параметры яркости")]
    [Range(0,255)] public byte fullAlpha = 255;     // Полная яркость
    [Range(0,255)] public byte blinkAlpha = 60;     // Просадка при «морге»
    [Range(0,255)] public byte offAlpha = 0;        // «Выключено»

    [Header("Длительности (сек)")]
    public float steadyOnDuration = 3f;             // Сколько горит постоянно
    public float blinkLowHold = 0.3f;               // «Низ» каждого морга
    public int blinkCount = 2;                      // Сколько моргов подряд
    public float offDuration = 1f;                  // Пауза «выключено»

    [Header("Плавность переходов")]
    public float fadeInTime = 0.15f;                // Из off → full
    public float fadeOutTime = 0.1f;                // В off перед паузой
    public float blinkDownTime = 0.06f;             // full → blinkAlpha
    public float blinkUpTime = 0.06f;               // blinkAlpha → full

    [Header("Прочее")]
    public bool useUnscaledTime = false;            // Игнорировать timeScale (например, в меню)

    Coroutine routine;

    void Awake()
    {
        if (target == null) target = GetComponent<Image>();
    }

    void OnEnable()
    {
        routine = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
    }

    IEnumerator Loop()
    {
        // Начнём со включения (можно сразу full, можно плавно — оставлю плавно)
        SetAlpha(offAlpha);
        yield return FadeTo(fullAlpha, fadeInTime);

        while (true)
        {
            // 1) Горим стабильно
            yield return Wait(steadyOnDuration);

            // 2) Моргаем N раз
            for (int i = 0; i < blinkCount; i++)
            {
                yield return FadeTo(blinkAlpha, blinkDownTime);
                yield return Wait(blinkLowHold);
                yield return FadeTo(fullAlpha, blinkUpTime);
                yield return Wait(blinkLowHold);
            }

            // 3) Выключаемся на секунду
            yield return FadeTo(offAlpha, fadeOutTime);
            yield return Wait(offDuration);

            // 4) Обратно включаемся
            yield return FadeTo(fullAlpha, fadeInTime);
        }
    }

    void SetAlpha(byte a)
    {
        var c = target.color;
        c.a = a / 255f;
        target.color = c;
    }

    IEnumerator FadeTo(byte toAlpha, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(toAlpha);
            yield break;
        }

        float t = 0f;
        float start = target.color.a;
        float end = toAlpha / 255f;

        while (t < 1f)
        {
            t += DeltaTime() / duration;
            float a = Mathf.Lerp(start, end, t);
            var c = target.color;
            c.a = a;
            target.color = c;
            yield return null;
        }
    }

    float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    IEnumerator Wait(float seconds)
    {
        if (seconds <= 0f) yield break;
        if (useUnscaledTime)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
