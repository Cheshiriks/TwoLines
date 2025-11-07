using System.Collections;
using TMPro;
using UnityEngine;

public class BestTime : MonoBehaviour
{
    [Header("Pulse Settings")]
    [SerializeField] private float pulseDuration = 1.0f; // Длительность одного цикла пульсации (сжатие + растяжение)
    [SerializeField] private float scaleFactor = 0.7f; // Насколько уменьшать объект (70% от исходного)

    private Vector3 originalScale; // Оригинальный размер текста
    private TextMeshProUGUI textMeshPro; // Ссылка на TextMeshPro компонент
    private string _currentLanguage = "ru";
    private bool _isChangeLang = true;
    private float _maxGameTime = 0f;
    
    private void Start()
    {
        // Получаем компонент TextMeshProUGUI (если это UI)
        textMeshPro = GetComponent<TextMeshProUGUI>();
        originalScale = transform.localScale;
        _currentLanguage = Language.Instance.currentLanguage;
        _maxGameTime = SaveGame.Instance.maxGameTime;

        // Запускаем пульсацию
        StartCoroutine(Pulse());
    }

    private void Update()
    {
        if (_currentLanguage != Language.Instance.currentLanguage)
        {
            _currentLanguage = Language.Instance.currentLanguage;
            _isChangeLang = true;
        }

        if (_maxGameTime > 0 && _isChangeLang)
        {
            int minutes = (int)(_maxGameTime / 60f);
            int seconds = (int)(_maxGameTime % 60f);
            string time = $"{minutes:00}:{seconds:00}";
        
            if (Language.Instance.currentLanguage == "ru")
            {
                textMeshPro.text = "Максимальное время матча: " + time;
            }
            else if (Language.Instance.currentLanguage == "tr")
            {
                textMeshPro.text = "Maksimum maç süresi: " + time;
            }
            else
            {
                textMeshPro.text = "Max match time: " + time;
            }
            _isChangeLang =  false;
        }
    }
    
    private IEnumerator Pulse()
    {
        // Бесконечная анимация пульсации
        while (true)
        {
            // Уменьшаем масштаб до scaleFactor
            float elapsedTime = 0f;
            while (elapsedTime < pulseDuration)
            {
                transform.localScale = Vector3.Lerp(originalScale, originalScale * scaleFactor, elapsedTime / pulseDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Увеличиваем масштаб обратно до оригинала
            elapsedTime = 0f;
            while (elapsedTime < pulseDuration)
            {
                transform.localScale = Vector3.Lerp(originalScale * scaleFactor, originalScale, elapsedTime / pulseDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
    }
}
