using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простая зацикленная демонстрация для сцены Menu:
/// одна линия стартует снизу, летит под небольшим углом,
/// касается границы, выбрасывает партиклы, плавно гаснет и всё повторяется.
/// </summary>
public class MenuLineLoop : MonoBehaviour
{
    [Header("Prefabs & Visuals")]
    [SerializeField] private Line linePrefab;
    [SerializeField] private ParticleSystem impactParticlesPrefab;
    [SerializeField] private Sprite headSprite;

    [Header("Motion")]
    [SerializeField, Tooltip("Скорость движения головы (ед/сек).")]
    private float speed = 2.2f;
    [SerializeField, Tooltip("Диапазон угла к горизонту (в градусах) для старта вверх. Небольшой наклон.")]
    private Vector2 startAngleDegRange = new Vector2(70f, 110f); // около вертикали, но с малым наклоном
    [SerializeField]
    private Vector2 startRightAngleDegRange = new Vector2(70f, 110f);
    [SerializeField, Tooltip("Отступ от краёв при спавне (в мировых единицах).")]
    private float spawnMargin = 0.1f;

    [Header("Loop & Fade")]
    [SerializeField, Tooltip("Длительность затухания линии и головы.")]
    private float fadeDuration = 1.1f;
    [SerializeField, Tooltip("Пауза перед перезапуском цикла.")]
    private float restartDelay = 0.4f;
    [SerializeField, Tooltip("Целевая прозрачность в конце затухания (0..255).")]
    private byte targetAlpha = 0;

    // --- runtime ---
    private float minX, maxX, minY, maxY;
    private Transform head;
    private SpriteRenderer headRenderer;
    private CircleCollider2D headCol;
    private Line segment;
    private float headingRad;

    private bool _isTurn = true;
    private Color32 _lineColor = new Color32(255, 255, 255, 255);
    private List<Color32> _lineColors = new List<Color32>
    {
        new Color32(31, 255, 0, 255),
        new Color32(255, 0, 174, 255),
        Color.red,
        Color.blue,
        Color.yellow,
        new Color32(132, 0, 255, 255)
    };

    private void Start()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("MenuLineLoop: нет MainCamera на сцене.");
            enabled = false;
            return;
        }

        var bl = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        minX = bl.x; maxX = tr.x; minY = bl.y; maxY = tr.y;

        StartCoroutine(LoopRoutine());
    }

    private IEnumerator LoopRoutine()
    {
        while (true)
        {
            SpawnNew();

            // Движение до первого касания границы
            bool hit = false;
            while (!hit)
            {
                float dt = Time.deltaTime;
                Vector2 dir = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
                Vector2 newPos = (Vector2)head.position + dir * speed * dt;

                head.position = newPos;
                segment.SetPosition(newPos);

                hit = CheckBorderHit(newPos, out Vector2 clampedPos);
                if (hit)
                {
                    head.position = clampedPos; // точно на границе
                    SpawnImpactParticles(clampedPos);
                    // плавно гасим линию и голову
                    segment.FadeOut(fadeDuration, targetAlpha);
                    yield return FadeHead(headRenderer, fadeDuration, targetAlpha);
                    // подчистим объекты текущего пробега
                    if (segment != null) Destroy(segment.gameObject);
                    if (head != null) Destroy(head.gameObject);
                }

                yield return null;
            }

            yield return new WaitForSeconds(restartDelay);
        }
    }

    private void SpawnNew()
    {
        Vector2 spawn = new Vector2(0, 0);
        if (_isTurn)
        {
            // Позиция старта: где-то внизу, с небольшими отступами от краёв
            float x = minX + spawnMargin;
            float y = Random.Range(minY + spawnMargin, 0);
            spawn = new Vector2(x, y);
            
            // Угол старта: в сторону "вверх" с небольшим разбросом вокруг вертикали
            float angleDeg = Random.Range(startAngleDegRange.x, startAngleDegRange.y);
            headingRad = angleDeg * Mathf.Deg2Rad;
            
            _isTurn = false;
        }
        else
        {
            // Позиция старта: где-то внизу, с небольшими отступами от краёв
            float x = maxX - spawnMargin;
            float y = Random.Range(maxY - spawnMargin,0);
            spawn = new Vector2(x, y);
            
            // Угол старта: в сторону "вверх" с небольшим разбросом вокруг вертикали
            float angleDeg = Random.Range(startRightAngleDegRange.x, startRightAngleDegRange.y);
            headingRad = angleDeg * Mathf.Deg2Rad;
            
            _isTurn = true;
        }
        
        //Vector2 spawn = new Vector2(x, y);

        // Угол старта: в сторону "вверх" с небольшим разбросом вокруг вертикали
        // float angleDeg = Random.Range(startAngleDegRange.x, startAngleDegRange.y);
        // headingRad = angleDeg * Mathf.Deg2Rad;
        
        int colorNumber = Random.Range(0, _lineColors.Count);
        _lineColor = _lineColors[colorNumber];
        _lineColor.a = 190;

        // Голова (только визуал + триггерный коллайдер, физика не нужна)
        var headGO = new GameObject("MenuHead");
        head = headGO.transform;
        head.position = spawn;

        headCol = headGO.AddComponent<CircleCollider2D>();
        headCol.isTrigger = true;
        headCol.radius = 0.05f;

        var vis = new GameObject("HeadVisual");
        vis.transform.SetParent(head, false);
        vis.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        headRenderer = vis.AddComponent<SpriteRenderer>();
        headRenderer.sprite = headSprite;
        headRenderer.color = (_lineColor.a == 0) ? Color.white : (Color)_lineColor;
        headRenderer.sortingOrder = 10;

        // Подогнать размер визуала к радиусу коллайдера (мягкая копия вашей логики)
        FitHeadSpriteToCollider(headCol, vis.transform);

        // Новый сегмент линии
        segment = Instantiate(linePrefab, spawn, Quaternion.identity);
        segment.SetColor((_lineColor.a == 0) ? new Color32(255, 255, 255, 255) : _lineColor);
        segment.SetExcludeHeadPoints(3); // чтоб EdgeCollider не мешал в меню
        segment.Seed(spawn);
        segment.SetPosition(spawn); // первая точка
    }

    private bool CheckBorderHit(Vector2 pos, out Vector2 clamped)
    {
        clamped = pos;
        float r = headCol != null ? headCol.radius : 0f;

        bool hit =
            (pos.x - r <= minX) ||
            (pos.x + r >= maxX) ||
            (pos.y - r <= minY) ||
            (pos.y + r >= maxY);

        if (hit)
        {
            clamped.x = Mathf.Clamp(pos.x, minX + r, maxX - r);
            clamped.y = Mathf.Clamp(pos.y, minY + r, maxY - r);
        }
        return hit;
    }

    private void SpawnImpactParticles(Vector2 at)
    {
        if (impactParticlesPrefab == null) return;

        var ps = Instantiate(impactParticlesPrefab, at, Quaternion.identity);
        // Подкрасим как в игре
        var main = ps.main;
        Color baseColor = (Color)((_lineColor.a == 0) ? new Color32(255, 255, 255, 255) : _lineColor);

        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(baseColor, 0f),
                new GradientColorKey(baseColor, 0.6f),
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(g);
        ps.Play();

        Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax + 0.5f);
    }

    private IEnumerator FadeHead(SpriteRenderer sr, float duration, byte targetAByte)
    {
        if (sr == null) yield break;

        Color start = sr.color;
        float startA = start.a;
        float targetA = targetAByte / 255f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(startA, targetA, k);
            sr.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }
        sr.color = new Color(start.r, start.g, start.b, targetA);
    }

    private void FitHeadSpriteToCollider(CircleCollider2D col, Transform visual)
    {
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null || col == null) return;

        // ширина спрайта при localScale=1 (в мире) может быть 0, если спрайт ещё не отрисован — подстрахуемся
        float spriteWidth = sr.bounds.size.x;
        if (spriteWidth <= 0f)
        {
            // оценка через pixelsPerUnit
            if (sr.sprite != null)
            {
                float texW = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
                if (texW > 0f) spriteWidth = texW * visual.lossyScale.x;
            }
        }
        if (spriteWidth <= 0f) return;

        float targetDiameter = col.radius * 4f; // как в вашем коде
        float k = targetDiameter / spriteWidth;
        visual.localScale = new Vector3(k, k, 1f);
    }
}
