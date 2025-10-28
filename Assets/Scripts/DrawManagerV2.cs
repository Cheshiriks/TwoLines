using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawManagerV2 : MonoBehaviour
{
    // ==== Общие настройки ====
    [Header("Prefabs")]
    [SerializeField] private Line _linePrefab;               // твой Line (LineRenderer+EdgeCollider2D+Line.cs)
    [SerializeField] private GameObject _bonusPrefab;        // твой Bonus (SpriteRenderer)
    [SerializeField] private Sprite _headSprite;             // круг/точка для головы
    [SerializeField] private int _headSortingOrder = 10;     // поверх линий

    [Header("Movement")]
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _startAngleDeg = 90f;
    [SerializeField] private float _turnRateDegPerSec = 90f;
    [SerializeField] private float _stepTurnDeg = 15f;

    [Header("Self-Collision")]
    [SerializeField] private int _excludeHeadPoints = 3;
    [SerializeField] private int _minPointsBeforeCollision = 4;

    [Header("Spawn")]
    [SerializeField] private Vector2 _spawnP1 = new Vector2(-2f, 0f);
    [SerializeField] private Vector2 _spawnP2 = new Vector2(+2f, 0f);

    [Header("Gaps (разрывы)")]
    [SerializeField] private Vector2 _gapIntervalRange = new Vector2(2f, 3f);
    [SerializeField] private Vector2 _gapDurationRange = new Vector2(0.15f, 0.35f);

    [Header("Bonus")]
    [SerializeField] private Vector2 _bonusIntervalRange = new Vector2(3f, 6f); // каждые 3–6 сек
    [SerializeField] private float _bonusEdgePadding = 0.3f; // отступ от границ камеры

    public const float Resolution = 0.1f;

    // Границы камеры
    private float _minX, _maxX, _minY, _maxY;

    // Игра/игроки
    private bool _gameRunning = false;
    private Player _p1, _p2;

    // Бонус (один за раз)
    private GameObject _bonusGO;
    private Collider2D _bonusCol;
    private float _nextBonusAt = 0f;
    private SelfCollisionDetector _p1BonusDet;
    private SelfCollisionDetector _p2BonusDet;

    // ==== Внутренние типы ====
    private class Player
    {
        public string name;
        public Transform head;                // объект с Rigidbody2D + CircleCollider2D (scale = 1,1,1)
        public Rigidbody2D rb;
        public CircleCollider2D headCol;

        public KeyCode keyLeft, keyRight;
        public float headingRad;
        public int turnDir;

        public Color32 color;
        public List<Line> segments = new List<Line>();
        public Line currentSegment;
        public bool collisionArmed = false;

        public SpriteRenderer headRenderer;   // спрайт головы (на дочернем объекте)

        // Разрывы
        public bool penDown = true, inGap = false;
        public float nextGapAt, gapEndAt;

        // Детектор своих сегментов
        public OwnLineCollisionDetector ownDetector;
    }

    void Start()
    {
        if (_linePrefab == null) { Debug.LogError("Line Prefab не задан."); enabled = false; return; }
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("Нет MainCamera."); enabled = false; return; }

        var bl = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        _minX = bl.x; _maxX = tr.x; _minY = bl.y; _maxY = tr.y;

        _p1 = CreatePlayer("P1", _spawnP1, new Color32(31, 255, 0, 255),   KeyCode.A, KeyCode.D);
        _p2 = CreatePlayer("P2", _spawnP2, new Color32(255, 0, 174, 255),  KeyCode.LeftArrow, KeyCode.RightArrow);

        // Дуэль: голова ↔ все сегменты соперника (на старте по одному)
        AddOpponentDetectorsForAllSegments(_p1, _p2);
        AddOpponentDetectorsForAllSegments(_p2, _p1);

        // Head-to-Head (ничья)
        var h12 = _p1.head.gameObject.AddComponent<SelfCollisionDetector>();
        h12.Init(_p2.headCol, OnHeadsClashDraw);
        var h21 = _p2.head.gameObject.AddComponent<SelfCollisionDetector>();
        h21.Init(_p1.headCol, OnHeadsClashDraw);

        // Планируем первый бонус
        ScheduleNextBonus();

        _gameRunning = true;
    }

    void Update()
    {
        if (!_gameRunning) return;

        TickPlayer(_p1);
        TickPlayer(_p2);

        // Спавн бонуса по таймеру
        HandleBonusSpawn();
    }

    // ==== Игроки/сегменты ====
    private Player CreatePlayer(string name, Vector2 spawn, Color32 color, KeyCode left, KeyCode right)
    {
        var p = new Player();
        p.name = name; p.keyLeft = left; p.keyRight = right; p.color = color;

        // Родитель: голова с физикой и триггером (scale = 1,1,1)
        var headObj = new GameObject($"{name}_Head");
        p.head = headObj.transform;
        p.head.position = spawn;
        p.head.localScale = Vector3.one;

        p.rb = headObj.AddComponent<Rigidbody2D>();
        p.rb.isKinematic = true; p.rb.gravityScale = 0f;
        p.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        p.headCol = headObj.AddComponent<CircleCollider2D>();
        p.headCol.isTrigger = true; p.headCol.radius = 0.05f;

        // Дочерний объект визуала — масштабируем только его
        var vis = new GameObject($"{name}_HeadVisual");
        vis.transform.SetParent(p.head, worldPositionStays: false);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;
        vis.transform.localScale = Vector3.one;

        var headSR = vis.AddComponent<SpriteRenderer>();
        headSR.sprite = _headSprite;
        headSR.color = p.color;
        headSR.sortingOrder = _headSortingOrder;
        p.headRenderer = headSR;

        // Подгоняем размер спрайта под радиус коллайдера головы
        FitHeadSpriteToCollider(p, vis.transform);

        // Первый сегмент
        StartNewSegment(p, spawn);

        // Детектор своих сегментов: реагирует на ЛЮБУЮ старую часть
        p.ownDetector = headObj.AddComponent<OwnLineCollisionDetector>();
        p.ownDetector.Init(
            () => _gameRunning,
            () => p.collisionArmed,
            () => p.currentSegment != null ? p.currentSegment.Collider : null,
            () => OnPlayerHitSelf(p)
        );
        p.ownDetector.AddTarget(p.currentSegment.Collider);

        p.headingRad = _startAngleDeg * Mathf.Deg2Rad;
        p.penDown = true; p.inGap = false;
        p.nextGapAt = Time.time + Random.Range(_gapIntervalRange.x, _gapIntervalRange.y);
        return p;
    }

    // масштабируем ТОЛЬКО дочерний визуал, не трогая Transform головы с коллайдером
    private void FitHeadSpriteToCollider(Player p, Transform visual)
    {
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null || p.headCol == null) return;

        // ширина спрайта в мире (при current localScale = 1)
        // Примечание: bounds считается в мировых координатах, поэтому при localScale=1 — это исходный размер.
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

        // До «взведения» самоколлизии игнорим столкновения головы с текущим сегментом
        Physics2D.IgnoreCollision(p.headCol, seg.Collider, true);

        p.currentSegment = seg;
        p.segments.Add(seg);
        p.collisionArmed = false;

        // подписываем новый сегмент в свой детектор
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
        if (segment == null || segment.Collider == null) return;
        var det = hitter.head.gameObject.AddComponent<SelfCollisionDetector>();
        det.Init(segment.Collider, () => OnPlayerHitOther(hitter, victimOwner));
    }

    private void TickPlayer(Player p)
    {
        if (p == null) return;

        HandleInput(p);
        HandleGaps(p);
        MoveAndDraw(p);
        ArmCollisionWhenReady(p);
        CheckBounds(p);
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

            var other = (p == _p1) ? _p2 : _p1;
            AddOpponentDetectorForSegment(other, p, p.currentSegment);

            p.nextGapAt = t + Random.Range(_gapIntervalRange.x, _gapIntervalRange.y);
        }
    }

    private void MoveAndDraw(Player p)
    {
        float dt = Time.deltaTime;
        if (p.turnDir != 0) p.headingRad += p.turnDir * _turnRateDegPerSec * Mathf.Deg2Rad * dt;

        Vector2 dir = new Vector2(Mathf.Cos(p.headingRad), Mathf.Sin(p.headingRad));
        Vector2 newPos = (Vector2)p.head.position + dir * _speed * dt;

        p.head.position = newPos;

        if (p.penDown && p.currentSegment != null)
            p.currentSegment.SetPosition(newPos);
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

    // ==== Бонусы ====
    private void HandleBonusSpawn()
    {
        if (_bonusPrefab == null) return;
        if (_bonusGO == null && Time.time >= _nextBonusAt) SpawnBonus();
    }

    private void ScheduleNextBonus()
    {
        _nextBonusAt = Time.time + Random.Range(_bonusIntervalRange.x, _bonusIntervalRange.y);
    }

    private void SpawnBonus()
    {
        float x = Random.Range(_minX + _bonusEdgePadding, _maxX - _bonusEdgePadding);
        float y = Random.Range(_minY + _bonusEdgePadding, _maxY - _bonusEdgePadding);
        Vector2 pos = new Vector2(x, y);

        _bonusGO = Instantiate(_bonusPrefab, pos, Quaternion.identity);

        // гарантируем, что есть триггер-коллайдер
        _bonusCol = _bonusGO.GetComponent<Collider2D>();
        if (_bonusCol == null)
        {
            var cc = _bonusGO.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            cc.radius = 0.5f; // подстрой под размер спрайта
            _bonusCol = cc;
        }
        else
        {
            _bonusCol.isTrigger = true;
        }

        // подключаем детекторы подбора для обеих голов
        _p1BonusDet = _p1.head.gameObject.AddComponent<SelfCollisionDetector>();
        _p1BonusDet.Init(_bonusCol, () => OnBonusCollected(_p1));

        _p2BonusDet = _p2.head.gameObject.AddComponent<SelfCollisionDetector>();
        _p2BonusDet.Init(_bonusCol, () => OnBonusCollected(_p2));
    }

    private void OnBonusCollected(Player collector)
    {
        if (_bonusGO == null) return;

        Debug.Log($"Бонус подобран: {collector.name}");

        if (_p1BonusDet != null) Destroy(_p1BonusDet);
        if (_p2BonusDet != null) Destroy(_p2BonusDet);
        _p1BonusDet = null; _p2BonusDet = null;

        Destroy(_bonusGO);
        _bonusGO = null; _bonusCol = null;

        ScheduleNextBonus();
    }

    // ==== Завершение игры / эффекты ====
    private void OnPlayerHitSelf(Player p)
    {
        if (!_gameRunning) return;
        Debug.Log($"{p.name} соприкоснулся сам с собой! {p.name} проиграл.");
        StopGame(p);
    }

    private void OnPlayerHitOther(Player hitter, Player victimOwner)
    {
        if (!_gameRunning) return;
        Debug.Log($"{hitter.name} врезался в линию {victimOwner.name}! {hitter.name} проиграл.");
        StopGame(hitter);
    }

    private void OnHeadsClashDraw()
    {
        if (!_gameRunning) return;
        Debug.Log("Ничья: головы столкнулись.");
        StopGame(null);
    }

    private void StopGame(Player loser)
    {
        if (!_gameRunning) return;
        _gameRunning = false;

        // Погасим проигравшего (если есть)
        if (loser != null)
        {
            foreach (var s in loser.segments) s?.FadeOut(0.5f, 20);
            StartCoroutine(FadeHead(loser, 0.5f, 20));
        }

        Debug.Log("Игра остановлена.");
    }

    private IEnumerator FadeHead(Player p, float duration = 0.5f, byte targetAlpha = 20)
    {
        if (p?.headRenderer == null) yield break;

        // headRenderer.color — это Color (float 0..1), но мы хотим целевую альфу в 0..255
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
}
