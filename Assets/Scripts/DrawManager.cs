using UnityEngine;

public class DrawManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Line _linePrefab;

    [Header("Movement (общие)")]
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _startAngleDeg = 90f;
    [SerializeField] private float _turnRateDegPerSec = 90f;
    [SerializeField] private float _stepTurnDeg = 15f;
    public const float Resolution = 0.1f;

    [Header("Collision")]
    [SerializeField] private int _excludeHeadPoints = 3;
    [SerializeField] private float _enableAfterDistance = 0.35f;
    [SerializeField] private int _minPointsBeforeCollision = 10;

    [Header("Spawn")]
    [SerializeField] private Vector2 _spawnP1 = new Vector2(-2f, 0f);
    [SerializeField] private Vector2 _spawnP2 = new Vector2(+2f, 0f);

    // границы камеры
    private float _minX, _maxX, _minY, _maxY;

    private bool _gameRunning = true;

    private Player _p1;
    private Player _p2;

    private class Player
    {
        public string name;
        public Transform head;
        public Rigidbody2D rb;
        public CircleCollider2D headCol;
        public Line line;
        public float headingRad;
        public int turnDir;
        public bool isDrawing = true;

        public Vector2 startPos;
        public bool collisionArmed = false;

        public KeyCode keyLeft;
        public KeyCode keyRight;
        public Color lineColor;
    }

    void Start()
    {
        // Границы видимой камеры
        var bl = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, 0));
        _minX = bl.x; _maxX = tr.x; _minY = bl.y; _maxY = tr.y;

        // Создаём игроков
        _p1 = CreatePlayer("P1", _spawnP1, new Color32( 31 , 255 , 0 , 255 ), KeyCode.A, KeyCode.D);
        _p2 = CreatePlayer("P2", _spawnP2, new Color32( 255 , 0 , 174 , 255 ), KeyCode.LeftArrow, KeyCode.RightArrow);

        // Дуэль: детектируем врезание головы в ЧУЖОЙ EdgeCollider2D
        // P1 головой в линию P2 → P1 проиграл
        var duelDet1 = _p1.head.gameObject.AddComponent<SelfCollisionDetector>();
        duelDet1.Init(_p2.line.Collider, () => OnPlayerHitOther(_p1, _p2));

        // P2 головой в линию P1 → P2 проиграл
        var duelDet2 = _p2.head.gameObject.AddComponent<SelfCollisionDetector>();
        duelDet2.Init(_p1.line.Collider, () => OnPlayerHitOther(_p2, _p1));

        // Новое: голова ↔ голова = ничья
        var headVsHead1 = _p1.head.gameObject.AddComponent<SelfCollisionDetector>();
        headVsHead1.Init(_p2.headCol, OnHeadsClashDraw);

        var headVsHead2 = _p2.head.gameObject.AddComponent<SelfCollisionDetector>();
        headVsHead2.Init(_p1.headCol, OnHeadsClashDraw);
    }
    
    private void OnHeadsClashDraw()
    {
        if (!_gameRunning) return;
        Debug.Log("Ничья: головы столкнулись.");
        StopGame();
    }

    private Player CreatePlayer(string name, Vector2 spawn, Color color, KeyCode left, KeyCode right)
    {
        var p = new Player();
        p.name = name;
        p.keyLeft = left;
        p.keyRight = right;
        p.lineColor = color;

        // Голова/точка
        var headObj = new GameObject($"{name}_Head");
        p.head = headObj.transform;
        p.head.position = spawn;
        p.startPos = spawn;

        p.rb = headObj.AddComponent<Rigidbody2D>();
        p.rb.isKinematic = true;
        p.rb.gravityScale = 0f;
        p.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        p.headCol = headObj.AddComponent<CircleCollider2D>();
        p.headCol.isTrigger = true;
        p.headCol.radius = 0.05f;

        // Линия
        p.line = Instantiate(_linePrefab, spawn, Quaternion.identity);
        p.line.SetExcludeHeadPoints(_excludeHeadPoints);
        p.line.SetColor(color);

        // Самоколлизию «взводим» позже → игнорим до созревания
        Physics2D.IgnoreCollision(p.headCol, p.line.Collider, true);

        // Стартовый курс
        p.headingRad = _startAngleDeg * Mathf.Deg2Rad;

        // Детектор САМОпересечения (проигрыш при ударе в свою линию)
        var selfDet = headObj.AddComponent<SelfCollisionDetector>();
        selfDet.Init(p.line.Collider, () =>
        {
            // сработает только когда самоколлизии разрешены
            if (p.collisionArmed) OnPlayerHitSelf(p);
        });

        return p;
    }

    void Update()
    {
        if (!_gameRunning) return;

        TickPlayer(_p1);
        TickPlayer(_p2);
    }

    private void TickPlayer(Player p)
    {
        if (!p.isDrawing) return;

        HandleInput(p);
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

    private void MoveAndDraw(Player p)
    {
        float dt = Time.deltaTime;
        if (p.turnDir != 0)
            p.headingRad += p.turnDir * _turnRateDegPerSec * Mathf.Deg2Rad * dt;

        Vector2 dir = new Vector2(Mathf.Cos(p.headingRad), Mathf.Sin(p.headingRad));
        Vector2 newPos = (Vector2) p.head.position + _speed * dt * dir;

        p.head.position = newPos;
        p.line.SetPosition(newPos);
    }

    private void ArmCollisionWhenReady(Player p)
    {
        if (p.collisionArmed) return;

        float traveled = Vector2.Distance(p.startPos, p.head.position);
        if (traveled >= _enableAfterDistance && p.line.PointCount >= _minPointsBeforeCollision)
        {
            Physics2D.IgnoreCollision(p.headCol, p.line.Collider, false);
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

    // Проигрыш при самопересечении
    private void OnPlayerHitSelf(Player p)
    {
        if (!_gameRunning) return;
        Debug.Log($"{p.name} соприкоснулся сам с собой! {p.name} проиграл.");
        StopGame(p);
    }

    // Проигрыш при ударе в ЧУЖУЮ линию
    private void OnPlayerHitOther(Player hitter, Player victimOwner)
    {
        if (!_gameRunning) return;
        // hitter — тот, чья голова врезалась; он проиграл
        Debug.Log($"{hitter.name} врезался в линию {victimOwner.name}! {hitter.name} проиграл.");
        StopGame(hitter);
    }

    private void StopGame(Player loser = null)
    {
        if (!_gameRunning) return;
        _gameRunning = false;

        _p1.isDrawing = false;
        _p2.isDrawing = false;

        Debug.Log("Игра остановлена.");
        
        // если кто-то проиграл — погасить его линии
        if (loser != null)
        {
            loser.line.FadeOut(0.5f); // время затухания
        }
    }
}
