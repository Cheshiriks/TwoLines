using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class DrawManager : MonoBehaviour
{
    // ==== Prefabs / Systems ====
    [Header("Prefabs & Systems")]
    [SerializeField] private Line _linePrefab;                    // Line (LineRenderer+EdgeCollider2D+Line.cs)
    [SerializeField] private Sprite _headSprite;                  // визуал головы
    [SerializeField] private int _headSortingOrder = 10;          // рендер головы поверх линии
    [SerializeField] private ParticleSystem _deathParticlesPrefab;// партиклы при поражении
    [SerializeField] private BonusSystem _bonusSystem;            // внешняя система бонусов

    // ==== Movement ====
    [Header("Movement")]
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _startAngleDeg = 90f;
    [SerializeField] private float _turnRateDegPerSec = 90f;
    [SerializeField] private float _stepTurnDeg = 15f;

    // ==== Self-Collision ====
    [Header("Self-Collision")]
    [SerializeField] private int _excludeHeadPoints = 3;          // не отдавать в EdgeCollider2D последние N точек
    [SerializeField] private int _minPointsBeforeCollision = 4;   // «взвод» самоколлизии

    // ==== Spawns ====
    [Header("Spawn")]
    [SerializeField] private Vector2 _spawnP1 = new Vector2(-2f, 0f);
    [SerializeField] private Vector2 _spawnP2 = new Vector2(+2f, 0f);

    // ==== Gaps (разрывы) ====
    [Header("Gaps")]
    [SerializeField] private Vector2 _gapIntervalRange = new Vector2(2f, 3f);
    [SerializeField] private Vector2 _gapDurationRange = new Vector2(0.15f, 0.35f);

    // ==== Bonus effects (без ScriptableObject) ====
    [Header("Bonus Effects")]
    [Tooltip("Ускорение (множитель) для SpeedUp")]
    [SerializeField] private float _speedUpMul = 1.3f;   // +30%
    [Tooltip("Замедление (множитель) для SpeedDown")]
    [SerializeField] private float _speedDownMul = 0.7f; // -30%
    [Tooltip("Длительность временных эффектов (сек)")]
    [SerializeField] private float _effectDuration = 3f; // для Speed/Invuln/PenOff
    [Tooltip("Прозрачность линии при неуязвимости (0–1). Например, 0.5 = полупрозрачная.")]
    [SerializeField, Range(0f, 1f)] private float _invulnAlpha = 0.5f;

    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private TextMeshProUGUI scoreFirstPlayerText;
    [SerializeField] private TextMeshProUGUI scoreSecondPlayerText;
    
    // Для Line.CanAppend
    public const float Resolution = 0.1f;

    // ==== Camera bounds ====
    private float _minX, _maxX, _minY, _maxY;

    // ==== Game state ====
    private bool _gameRunning = false;
    private Player _p1, _p2;

    // ==== Player model ====
    private class Player
    {
        public int id;
        public string name;

        // Физика головы
        public Transform head;              // объект с Rigidbody2D + CircleCollider2D (scale = 1,1,1)
        public Rigidbody2D rb;
        public CircleCollider2D headCol;

        // Визуал головы (на дочернем объекте)
        public SpriteRenderer headRenderer;

        // Управление и движение
        public KeyCode keyLeft, keyRight;
        public float headingRad;
        public int turnDir;

        // Линия
        public Color32 color;
        public List<Line> segments = new List<Line>();
        public Line currentSegment;
        public bool collisionArmed = false;

        // Разрывы
        public bool penDown = true, inGap = false;
        public float nextGapAt, gapEndAt;
        public bool wasDrawSuppressed = false;  // для PenOff

        // Эффекты бонусов
        public float speedMul = 1f;
        public float speedMulUntil = 0f;

        public bool invulnerable = false;
        public float invulnUntil = 0f;

        public float noDrawUntil = 0f;          // PenOff до времени

        // Детектор своих сегментов (самопересечение)
        public OwnLineCollisionDetector ownDetector;
    }

    // ==== Unity lifecycle ====
    void Start()
    {
        if (_linePrefab == null)
        {
            Debug.LogError("DrawManagerV3: Line Prefab не задан.");
            enabled = false;
            return;
        }
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("DrawManagerV3: Нет MainCamera (поставь тег MainCamera).");
            enabled = false;
            return;
        }

        // Границы экрана
        var bl = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        _minX = bl.x; _maxX = tr.x; _minY = bl.y; _maxY = tr.y;

        // Игроки
        _p1 = CreatePlayer(1, "P1", _spawnP1, new Color32(31, 255, 0, 255), KeyCode.A, KeyCode.D);
        _p2 = CreatePlayer(2, "P2", _spawnP2, new Color32(255, 0, 174, 255), KeyCode.LeftArrow, KeyCode.RightArrow);

        // Дуэль: голова ↔ все сегменты соперника (на старте по одному)
        AddOpponentDetectorsForAllSegments(_p1, _p2);
        AddOpponentDetectorsForAllSegments(_p2, _p1);

        // Head-to-Head (ничья)
        var h12 = _p1.head.gameObject.AddComponent<SelfCollisionDetector>();
        h12.Init(_p2.headCol, OnHeadsClashDraw);
        var h21 = _p2.head.gameObject.AddComponent<SelfCollisionDetector>();
        h21.Init(_p1.headCol, OnHeadsClashDraw);

        // Бонусы — только если включены в SaveGame и компонент активен
        if (SaveGame.IsBonusSystem && _bonusSystem != null && _bonusSystem.isActiveAndEnabled)
        {
            _bonusSystem.InitializeFromCamera(cam);
            _bonusSystem.RegisterHead(_p1.headCol, (kind) => OnBonusCollected(_p1, kind));
            _bonusSystem.RegisterHead(_p2.headCol, (kind) => OnBonusCollected(_p2, kind));
        }
        else
        {
            if (_bonusSystem != null)
            {
                _bonusSystem.ForceDespawn();
                _bonusSystem.enabled = false;
            }
            Debug.Log("Система бонусов отключена (SaveGame.IsBonusSystem = false или компонент не активен).");
        }

        _gameRunning = true;
    }

    void Update()
    {
        if (!_gameRunning) return;

        TickPlayer(_p1);
        TickPlayer(_p2);

        // Тик бонусной системы — только когда разрешено и реально включена
        if (SaveGame.IsBonusSystem && _bonusSystem != null && _bonusSystem.isActiveAndEnabled)
        {
            _bonusSystem.Tick();
        }
    }

    // ==== Player / segments ====
    private Player CreatePlayer(int id, string name, Vector2 spawn, Color32 color, KeyCode left, KeyCode right)
    {
        var p = new Player();
        p.id = id;
        p.name = name; 
        p.keyLeft = left; 
        p.keyRight = right; 
        p.color = color;

        // Родитель: голова с физикой и триггером (scale = 1,1,1)
        var headObj = new GameObject($"{name}_Head");
        p.head = headObj.transform;
        p.head.position = spawn;
        p.head.localScale = Vector3.one;

        p.rb = headObj.AddComponent<Rigidbody2D>();
        p.rb.isKinematic = true;
        p.rb.gravityScale = 0f;
        p.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        p.headCol = headObj.AddComponent<CircleCollider2D>();
        p.headCol.isTrigger = true;
        p.headCol.radius = 0.05f;

        // Дочерний объект визуала — масштабируем только его, чтобы не ломать радиус коллайдера
        var vis = new GameObject($"{name}_HeadVisual");
        vis.transform.SetParent(p.head, worldPositionStays: false);
        vis.transform.localPosition = new Vector3(0f, 0f, -0.1f); // чуть вперёд по Z
        vis.transform.localRotation = Quaternion.identity;
        vis.transform.localScale = Vector3.one;

        var headSR = vis.AddComponent<SpriteRenderer>();
        headSR.sprite = _headSprite;
        headSR.color = p.color;
        headSR.sortingOrder = _headSortingOrder;
        p.headRenderer = headSR;

        FitHeadSpriteToCollider(p, vis.transform);

        // Первый сегмент
        StartNewSegment(p, spawn);

        // Детектор своих сегментов (реагирует на ЛЮБУЮ старую часть)
        p.ownDetector = headObj.AddComponent<OwnLineCollisionDetector>();
        p.ownDetector.Init(
            () => _gameRunning,
            () => p.collisionArmed,
            () => p.currentSegment != null ? p.currentSegment.Collider : null,
            () => OnPlayerHitSelf(p)
        );
        p.ownDetector.AddTarget(p.currentSegment.Collider);

        // Движение
        p.headingRad = _startAngleDeg * Mathf.Deg2Rad;
        p.penDown = true; p.inGap = false;
        p.nextGapAt = Time.time + Random.Range(_gapIntervalRange.x, _gapIntervalRange.y);

        return p;
    }

    // Масштабируем ТОЛЬКО дочерний визуал (не объект с коллайдером)
    private void FitHeadSpriteToCollider(Player p, Transform visual)
    {
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null || p.headCol == null) return;

        // ширина спрайта в мире при localScale = 1
        float spriteWidth = sr.bounds.size.x;
        if (spriteWidth <= 0f) return;

        // хотим видимый диаметр ≈ 2 * radius
        float targetDiameter = p.headCol.radius * 4f;
        float k = targetDiameter / spriteWidth;

        visual.localScale = new Vector3(k, k, 1f);
    }

    private void StartNewSegment(Player p, Vector2 at)
    {
        var seg = Instantiate(_linePrefab, at, Quaternion.identity);
        seg.SetExcludeHeadPoints(_excludeHeadPoints);
        seg.SetColor(p.color);
        // при желании настрой порядки рендера линии прямо тут (или в префабе)
        // seg.SetSorting("Default", 0);

        // До «взведения» самоколлизии игнорим касания головы с текущим сегментом
        Physics2D.IgnoreCollision(p.headCol, seg.Collider, true);

        if (p.invulnerable)
            seg.SetAlpha(_invulnAlpha);
        p.currentSegment = seg;
        p.segments.Add(seg);
        p.collisionArmed = false;

        // Подписываем новый сегмент в собственный детектор
        p.ownDetector?.AddTarget(seg.Collider);
    }

    private void AddOpponentDetectorsForAllSegments(Player hitter, Player victimOwner)
    {
        if (hitter?.head == null || victimOwner?.segments == null) return;

        foreach (var seg in victimOwner.segments)
        {
            if (seg == null || seg.Collider == null) continue;
            var det = hitter.head.gameObject.AddComponent<SelfCollisionDetector>();
            det.Init(seg.Collider, () => OnPlayerHitOther(hitter, victimOwner));
        }
    }

    private void AddOpponentDetectorForSegment(Player hitter, Player victimOwner, Line segment)
    {
        if (segment == null || segment.Collider == null || hitter?.head == null) return;
        var det = hitter.head.gameObject.AddComponent<SelfCollisionDetector>();
        det.Init(segment.Collider, () => OnPlayerHitOther(hitter, victimOwner));
    }

    // ==== Per-frame player tick ====
    private void TickPlayer(Player p)
    {
        if (p == null) return;

        HandleInput(p);
        HandleGaps(p);
        MoveAndDraw(p);
        ArmCollisionWhenReady(p);
        CheckBounds(p);

        // страховка: голова всегда видна
        if (p.headRenderer != null && !p.headRenderer.enabled) p.headRenderer.enabled = true;

        // истёкшие эффекты
        if (p.speedMul != 1f && Time.time >= p.speedMulUntil) p.speedMul = 1f;
        // Проверка окончания неуязвимости
        if (p.invulnerable && Time.time >= p.invulnUntil)
        {
            p.invulnerable = false;
            // вернуть непрозрачность
            foreach (var seg in p.segments)
                if (seg != null) seg.SetAlpha(1f);
        }
    }

    private void HandleInput(Player p)
    {
        bool holdLeft  = Input.GetKey(p.keyLeft);
        bool holdRight = Input.GetKey(p.keyRight);
        p.turnDir = (holdLeft && holdRight) ? 0 : (holdLeft ? +1 : (holdRight ? -1 : 0));

        if (Input.GetKeyDown(p.keyLeft))  p.headingRad += _stepTurnDeg * Mathf.Deg2Rad;
        if (Input.GetKeyDown(p.keyRight)) p.headingRad -= _stepTurnDeg * Mathf.Deg2Rad;
    }

    private void HandleGaps(Player p)
    {
        float t = Time.time;

        if (!p.inGap && t >= p.nextGapAt)
        {
            p.inGap = true; p.penDown = false;
            p.gapEndAt = t + Random.Range(_gapDurationRange.x, _gapDurationRange.y);
        }

        if (p.inGap && t >= p.gapEndAt)
        {
            p.inGap = false; p.penDown = true;

            var pos = (Vector2)p.head.position;
            StartNewSegment(p, pos);

            // Подключаем дуэльный детектор для нового сегмента у соперника
            var other = (p == _p1) ? _p2 : _p1;
            AddOpponentDetectorForSegment(other, p, p.currentSegment);

            p.nextGapAt = t + Random.Range(_gapIntervalRange.x, _gapIntervalRange.y);
        }
    }

    private void MoveAndDraw(Player p)
    {
        float dt = Time.deltaTime;

        if (p.turnDir != 0)
            p.headingRad += p.turnDir * _turnRateDegPerSec * Mathf.Deg2Rad * dt;

        Vector2 dir = new Vector2(Mathf.Cos(p.headingRad), Mathf.Sin(p.headingRad));
        Vector2 newPos = (Vector2)p.head.position + dir * (_speed * p.speedMul) * dt;

        p.head.position = newPos;

        bool suppressed = Time.time < p.noDrawUntil;      // эффект PenOff активен?
        bool wasSuppressed = p.wasDrawSuppressed;
        bool exitSuppressionNow = (!suppressed && wasSuppressed);

        // === 1) Если только что ВЫШЛИ из подавления — начнём новый сегмент до любого SetPosition ===
        if (exitSuppressionNow)
        {
            StartNewSegment(p, newPos);
            var other = (p == _p1) ? _p2 : _p1;
            AddOpponentDetectorForSegment(other, p, p.currentSegment);

            // Хотим посеять первую точку в новый сегмент и на этом кадре больше не рисовать
            if (p.penDown && p.currentSegment != null)
                p.currentSegment.SetPosition(newPos);

            p.wasDrawSuppressed = false;
            return; // важно: не добавлять точку в старый сегмент
        }

        // === 2) Обычное рисование, если сейчас не подавлено ===
        if (p.penDown && !suppressed && p.currentSegment != null)
        {
            p.currentSegment.SetPosition(newPos);
        }

        // === 3) Запомнить текущее состояние подавления для следующего кадра ===
        p.wasDrawSuppressed = suppressed;
    }

    private void ArmCollisionWhenReady(Player p)
    {
        if (p.collisionArmed || p.currentSegment == null) return;

        if (p.currentSegment.PointCount >= _minPointsBeforeCollision)
        {
            Physics2D.IgnoreCollision(p.headCol, p.currentSegment.Collider, false);
            p.collisionArmed = true;
        }
    }

    private void CheckBounds(Player p)
    {
        Vector2 pos = p.head.position;
        float r = p.headCol.radius;
        if (pos.x - r <= _minX || pos.x + r >= _maxX || pos.y - r <= _minY || pos.y + r >= _maxY)
        {
            Debug.Log($"{p.name} достиг края экрана! {p.name} проиграл.");
            StopGame(p);
        }
    }

    // ==== Bonus callback ====
    private void OnBonusCollected(Player p, BonusKind kind)
    {
        switch (kind)
        {
            case BonusKind.SpeedUp:
                p.speedMul = _speedUpMul;
                p.speedMulUntil = Time.time + _effectDuration;
                break;

            case BonusKind.SpeedDown:
                p.speedMul = _speedDownMul;
                p.speedMulUntil = Time.time + _effectDuration;
                break;

            case BonusKind.Invulnerability:
                p.invulnerable = true;
                p.invulnUntil = Time.time + _effectDuration;

                // сделать все текущие сегменты полупрозрачными
                foreach (var seg in p.segments)
                    if (seg != null) seg.SetAlpha(_invulnAlpha);
                break;

            case BonusKind.PenOff:
                p.noDrawUntil = Mathf.Max(p.noDrawUntil, Time.time + _effectDuration);
                p.wasDrawSuppressed = true; // подстраховка: считаем, что уже в подавлении
                break;
        }

        Debug.Log($"Бонус {kind} для {p.name}");
    }

    // ==== Effects ====
    private void SpawnDeathParticles(Player p)
    {
        if (p == null || _deathParticlesPrefab == null) return;

        var ps = Instantiate(_deathParticlesPrefab, p.head.position, Quaternion.identity);

        // Цвет партиклов = цвет линии
        var main = ps.main;
        Color baseColor = new Color32(p.color.r, p.color.g, p.color.b, 255);

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

    private IEnumerator FadeHead(Player p, float duration = 0.5f, byte targetAlpha = 20)
    {
        if (p?.headRenderer == null) yield break;

        Color start = p.headRenderer.color;
        float startA = start.a;
        float targetA = targetAlpha / 255f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(startA, targetA, k);
            p.headRenderer.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }
        p.headRenderer.color = new Color(start.r, start.g, start.b, targetA);
    }

    // ==== Events / Game over ====
    private void OnPlayerHitSelf(Player p)
    {
        if (!_gameRunning || p.invulnerable) return;
        Debug.Log($"{p.name} соприкоснулся сам с собой! {p.name} проиграл.");
        StopGame(p);
    }

    private void OnPlayerHitOther(Player hitter, Player victimOwner)
    {
        if (!_gameRunning || hitter.invulnerable) return;
        Debug.Log($"{hitter.name} врезался в линию {victimOwner.name}! {hitter.name} проиграл.");
        StopGame(hitter);
    }

    private void OnHeadsClashDraw()
    {
        if (!_gameRunning) return;
        Debug.Log("Ничья: головы столкнулись.");
        // эффект у обеих
        SpawnDeathParticles(_p1);
        SpawnDeathParticles(_p2);
        StopGame(null);
    }

    private void StopGame(Player loser)
    {
        if (!_gameRunning) return;
        _gameRunning = false;

        // Погасим проигравшего (если есть) + партиклы
        if (loser != null)
        {
            foreach (var s in loser.segments) s?.FadeOut(0.5f, 20);
            StartCoroutine(FadeHead(loser, 0.5f, 20));
            SpawnDeathParticles(loser);
        }

        // Спрячем активные бонусы (если есть/включены)
        // if (_bonusSystem != null) _bonusSystem.ForceDespawn();
        ActiveMenu(loser?.id);
        Debug.Log("Игра остановлена.");
    }

    private void ActiveMenu(int? id)
    {
        
        SaveGame.Instance.AddScore(id);
        scoreFirstPlayerText.text = SaveGame.Instance.scoreFirst.ToString();
        scoreSecondPlayerText.text = SaveGame.Instance.scoreSecond.ToString();
        
        Invoke(nameof(SetActiveMenu), 0.6f);
    }

    private void SetActiveMenu()
    {
        menuCanvas.SetActive(true);
    }
}
