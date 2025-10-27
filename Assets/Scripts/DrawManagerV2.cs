using System.Collections.Generic;
using UnityEngine;

public class DrawManagerV2 : MonoBehaviour
{
    // ==== Общие настройки ====
    [Header("Prefab")]
    [SerializeField] private Line _linePrefab;              // Префаб линии (с твоим Line.cs, LineRenderer, EdgeCollider2D)

    [Header("Movement")]
    [SerializeField] private float _speed = 2f;             // линейная скорость
    [SerializeField] private float _startAngleDeg = 90f;    // 90° = вверх
    [SerializeField] private float _turnRateDegPerSec = 90f;// угловая скорость при удержании
    [SerializeField] private float _stepTurnDeg = 15f;      // поворот при одиночном нажатии

    [Header("Self-Collision")]
    [SerializeField] private int _excludeHeadPoints = 3;    // сколько последних точек не отдавать в EdgeCollider2D
    [SerializeField] private int _minPointsBeforeCollision = 4; // когда «взводить» самоколлизию

    [Header("Spawn")]
    [SerializeField] private Vector2 _spawnP1 = new Vector2(-2f, 0f);
    [SerializeField] private Vector2 _spawnP2 = new Vector2(+2f, 0f);

    [Header("Gaps (разрывы)")]
    [SerializeField] private Vector2 _gapIntervalRange = new Vector2(2f, 3f);   // через сколько секунд стартует разрыв
    [SerializeField] private Vector2 _gapDurationRange = new Vector2(0.15f, 0.35f); // длительность разрыва

    // Для Line.CanAppend
    public const float Resolution = 0.1f;

    // Границы камеры
    private float _minX, _maxX, _minY, _maxY;

    // Состояние игры
    private bool _gameRunning = false;

    // Игроки
    private Player _p1;
    private Player _p2;

    // ==== Внутренние типы/структуры ====
    private class Player
    {
        public string name;
        public Transform head;
        public Rigidbody2D rb;
        public CircleCollider2D headCol;

        public KeyCode keyLeft, keyRight;
        public float headingRad;
        public int turnDir;

        public Color color;
        public List<Line> segments = new List<Line>();
        public Line currentSegment;
        public bool collisionArmed = false;

        public bool penDown = true, inGap = false;
        public float nextGapAt, gapEndAt;

        // добавим ссылку на детектор своих сегментов
        public OwnLineCollisionDetector ownDetector;
    }

    // ==== Жизненный цикл ====

    void Start()
    {
        // Проверка префаба
        if (_linePrefab == null)
        {
            Debug.LogError("DrawManager: Line Prefab не задан в инспекторе.");
            enabled = false;
            return;
        }

        // Камера
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("DrawManager: Camera.main == null. Пометь главную камеру тегом MainCamera.");
            enabled = false;
            return;
        }

        var bl = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        _minX = bl.x; _maxX = tr.x; _minY = bl.y; _maxY = tr.y;

        // Создаём игроков (без перекрёстных ссылок)
        _p1 = CreatePlayer("P1", _spawnP1, new Color32( 31 , 255 , 0 , 255 ), KeyCode.A, KeyCode.D);
        _p2 = CreatePlayer("P2", _spawnP2, new Color32( 255 , 0 , 174 , 255 ), KeyCode.LeftArrow, KeyCode.RightArrow);

        // Дуэль: голова ↔ все сегменты соперника (на старте у обоих по одному сегменту)
        AddOpponentDetectorsForAllSegments(_p1, _p2);
        AddOpponentDetectorsForAllSegments(_p2, _p1);

        // Head-to-Head (ничья)
        var h12 = _p1.head.gameObject.AddComponent<SelfCollisionDetector>();
        h12.Init(_p2.headCol, OnHeadsClashDraw);
        var h21 = _p2.head.gameObject.AddComponent<SelfCollisionDetector>();
        h21.Init(_p1.headCol, OnHeadsClashDraw);

        _gameRunning = true;
    }

    void Update()
    {
        if (!_gameRunning) return;

        TickPlayer(_p1);
        TickPlayer(_p2);
    }

    // ==== Создание игроков/сегментов ====

    private Player CreatePlayer(string name, Vector2 spawn, Color color, KeyCode left, KeyCode right)
    {
        var p = new Player();
        p.name = name;
        p.keyLeft = left; p.keyRight = right;
        p.color = color;

        var headObj = new GameObject($"{name}_Head");
        p.head = headObj.transform;
        p.head.position = spawn;

        p.rb = headObj.AddComponent<Rigidbody2D>();
        p.rb.isKinematic = true; p.rb.gravityScale = 0f;
        p.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        p.headCol = headObj.AddComponent<CircleCollider2D>();
        p.headCol.isTrigger = true; p.headCol.radius = 0.05f;

        // Первый сегмент
        StartNewSegment(p, spawn);

        // Один детектор на голову, следит за ВСЕМИ своими сегментами
        p.ownDetector = headObj.AddComponent<OwnLineCollisionDetector>();
        p.ownDetector.Init(
            () => _gameRunning,
            () => p.collisionArmed,
            () => p.currentSegment != null ? p.currentSegment.Collider : null,
            () => OnPlayerHitSelf(p)
        );
        // подписываем первый сегмент
        p.ownDetector.AddTarget(p.currentSegment.Collider);

        p.headingRad = _startAngleDeg * Mathf.Deg2Rad;
        p.penDown = true; p.inGap = false;
        p.nextGapAt = Time.time + Random.Range(_gapIntervalRange.x, _gapIntervalRange.y);
        return p;
    }

    private void StartNewSegment(Player p, Vector2 at)
    {
        if (_linePrefab == null || p == null || p.headCol == null) return;

        var seg = Instantiate(_linePrefab, at, Quaternion.identity);
        if (seg == null || seg.Collider == null)
        {
            Debug.LogError("StartNewSegment: проблема с Line префабом.");
            return;
        }

        seg.SetExcludeHeadPoints(_excludeHeadPoints);
        seg.SetColor(p.color);

        // Игнор самоколлизии с ТЕКУЩИМ сегментом до взведения
        Physics2D.IgnoreCollision(p.headCol, seg.Collider, true);

        p.currentSegment = seg;
        p.segments.Add(seg);
        p.collisionArmed = false;

        // Подписываем новый сегмент в детектор своих линий
        p.ownDetector?.AddTarget(seg.Collider);
    }

    // Дуэльные детекторы: голова hitter ↔ ВСЕ сегменты victimOwner
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

    // Дуэльный детектор: голова hitter ↔ конкретный сегмент victimOwner
    private void AddOpponentDetectorForSegment(Player hitter, Player victimOwner, Line segment)
    {
        if (hitter?.head == null || segment?.Collider == null) return;
        var det = hitter.head.gameObject.AddComponent<SelfCollisionDetector>();
        det.Init(segment.Collider, () => OnPlayerHitOther(hitter, victimOwner));
    }

    // ==== Игровой цикл игрока ====

    private void TickPlayer(Player p)
    {
        if (p == null || p.head == null) return;

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

        if (Input.GetKeyDown(p.keyLeft))
            p.headingRad += _stepTurnDeg * Mathf.Deg2Rad;
        if (Input.GetKeyDown(p.keyRight))
            p.headingRad -= _stepTurnDeg * Mathf.Deg2Rad;
    }

    // Управление «дырками»: отключаем рисование на время, затем начинаем НОВЫЙ сегмент
    private void HandleGaps(Player p)
    {
        float t = Time.time;

        // старт разрыва
        if (!p.inGap && t >= p.nextGapAt)
        {
            p.inGap = true;
            p.penDown = false;
            p.gapEndAt = t + Random.Range(_gapDurationRange.x, _gapDurationRange.y);
        }

        // конец разрыва → новый сегмент, плюс дуэльный детектор для соперника
        if (p.inGap && t >= p.gapEndAt)
        {
            p.inGap = false;
            p.penDown = true;

            // создаём новый сегмент на текущей позиции головы
            var pos = (Vector2)p.head.position;
            StartNewSegment(p, pos);

            // соперник уже создан к этому моменту — добавим детектор только на НОВЫЙ сегмент
            var other = (p == _p1) ? _p2 : _p1;
            AddOpponentDetectorForSegment(other, p, p.currentSegment);

            // запланируем следующий разрыв
            p.nextGapAt = t + Random.Range(_gapIntervalRange.x, _gapIntervalRange.y);
        }
    }

    private void MoveAndDraw(Player p)
    {
        float dt = Time.deltaTime;

        if (p.turnDir != 0)
            p.headingRad += p.turnDir * _turnRateDegPerSec * Mathf.Deg2Rad * dt;

        Vector2 dir = new Vector2(Mathf.Cos(p.headingRad), Mathf.Sin(p.headingRad));
        Vector2 newPos = (Vector2)p.head.position + _speed * dt * dir;

        p.head.position = newPos;

        if (p.penDown && p.currentSegment != null)
            p.currentSegment.SetPosition(newPos);
    }

    // Взводим самоколлизию для текущего сегмента, когда он «подрос»
    private void ArmCollisionWhenReady(Player p)
    {
        if (p.collisionArmed || p.currentSegment == null) return;
        
        if (p.currentSegment.PointCount >= _minPointsBeforeCollision)
        {
            Physics2D.IgnoreCollision(p.headCol, p.currentSegment.Collider, false);
            p.collisionArmed = true;
        }
    }

    // Стоп по границам камеры
    private void CheckBounds(Player p)
    {
        Vector2 pos = p.head.position;
        float r = p.headCol != null ? p.headCol.radius : 0f;

        if (pos.x - r <= _minX || pos.x + r >= _maxX || pos.y - r <= _minY || pos.y + r >= _maxY)
        {
            Debug.Log($"{p.name} достиг края экрана! {p.name} проиграл.");
            StopGame(p);
        }
    }

    // ==== Обработчики событий ====

    // Самопересечение
    private void OnPlayerHitSelf(Player p)
    {
        if (!_gameRunning) return;
        Debug.Log($"{p.name} соприкоснулся сам с собой! {p.name} проиграл.");
        StopGame(p);
    }

    // Врезание в чужую линию
    private void OnPlayerHitOther(Player hitter, Player victimOwner)
    {
        if (!_gameRunning) return;
        Debug.Log($"{hitter.name} врезался в линию {victimOwner.name}! {hitter.name} проиграл.");
        StopGame(hitter);
    }

    // Столкновение голов — ничья
    private void OnHeadsClashDraw()
    {
        if (!_gameRunning) return;
        Debug.Log("Ничья: головы столкнулись.");
        StopGame();
    }

    private void StopGame(Player loser = null)
    {
        if (!_gameRunning) return;
        _gameRunning = false;
        Debug.Log("Игра остановлена.");
        
        // если кто-то проиграл — погасить его линии
        if (loser != null)
        {
            foreach (var seg in loser.segments)
            {
                if (seg != null)
                    seg.FadeOut(0.5f); // время затухания
            }
        }
    }
}
