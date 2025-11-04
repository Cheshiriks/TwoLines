using UnityEngine;
using System;
using System.Collections.Generic;

public class BonusSystem : MonoBehaviour
{
    [Header("Prefab & Spawn Timing")]
    [SerializeField] private GameObject _bonusPrefab;
    [Tooltip("Интервал между попытками заспавнить НОВЫЙ бонус (если не достигнут лимит).")]
    [SerializeField] private Vector2 _bonusIntervalRange = new Vector2(3f, 6f);
    [Tooltip("Максимум одновременных бонусов на карте.")]
    [SerializeField] private int _maxConcurrent = 3;
    [Tooltip("Сколько бонусов попытаться заспавнить сразу при инициализации (не больше MaxConcurrent).")]
    [SerializeField] private int _initialSpawnCount = 1;

    [Header("Spawn Area")]
    [Tooltip("Отступ от границ камеры при размещении бонуса.")]
    [SerializeField] private float _edgePadding = 0.3f;

    [Header("Placement Safety (без слоёв)")]
    [Tooltip("Радиус проверки пустоты вокруг точки спавна относительно ЛИНИЙ (EdgeCollider2D).")]
    [SerializeField] private float _spawnClearanceLine = 0.35f;
    [Tooltip("Радиус проверки пустоты вокруг точки спавна относительно ГОЛОВ (CircleCollider2D).")]
    [SerializeField] private float _spawnClearanceHead = 0.5f;
    [Tooltip("Сколько раз пробуем найти безопасную позицию за одну попытку спавна.")]
    [SerializeField] private int _maxPlacementAttempts = 24;

    [Header("Collider Fallback")]
    [Tooltip("Если у префаба нет своего Collider2D, добавим CircleCollider2D такого радиуса.")]
    [SerializeField] private float _fallbackColliderRadius = 0.5f;
    
    [Header("Pickup VFX")]
    [SerializeField] private ParticleSystem _pickupVfx;

    [Header("Kinds & Colors (Color32, без ScriptableObject)")]
    [Tooltip("Пул типов и их цветов. Система выбирает случайный тип из этого списка.")]
    [SerializeField] private List<KindColor> _palette = new List<KindColor>
    {
        new KindColor{ kind = BonusKind.SpeedUp,         color = new Color32( 80, 255,  80, 255) },
        new KindColor{ kind = BonusKind.SpeedDown,       color = new Color32(255,  80,  80, 255) },
        new KindColor{ kind = BonusKind.Invulnerability, color = new Color32(255, 215,   0, 255) },
        new KindColor{ kind = BonusKind.PenOff,          color = new Color32( 80, 180, 255, 255) },
    };

    [Serializable]
    private struct KindColor
    {
        public BonusKind kind;
        public Color32 color;
    }

    // --- Runtime state ---
    private float _minX, _maxX, _minY, _maxY;
    private float _nextSpawnAt;

    private readonly List<HeadEntry> _heads = new();
    private readonly List<BonusEntry> _bonuses = new();

    private class HeadEntry
    {
        public Collider2D headCollider;
        public GameObject headGO;
        public Action<BonusKind> onCollected;
    }

    private class BonusEntry
    {
        public GameObject go;
        public Collider2D col;
        public BonusKind kind;
        public Color32 color;
        public readonly List<SelfCollisionDetector> detectors = new();
    }

    // Буфер под OverlapCircleNonAlloc
    private static readonly Collider2D[] _overlapBuf = new Collider2D[64];

    // ==== Публичный API ====

    public void InitializeFromCamera(Camera cam, float paddingOverride = -1f)
    {
        if (cam == null)
        {
            Debug.LogError("BonusSystem.InitializeFromCamera: Camera == null");
            return;
        }

        var bl = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        _minX = bl.x; _maxX = tr.x; _minY = bl.y; _maxY = tr.y;

        if (paddingOverride >= 0f) _edgePadding = paddingOverride;

        ScheduleNextSpawn();

        // начальный спавн
        int count = Mathf.Clamp(_initialSpawnCount, 0, Mathf.Max(0, _maxConcurrent));
        for (int i = 0; i < count; i++)
        {
            if (_bonuses.Count >= _maxConcurrent) break;
            SpawnOneBonus();
        }
    }

    public void RegisterHead(Collider2D headCollider, Action<BonusKind> onCollected)
    {
        if (headCollider == null || onCollected == null) return;

        var h = new HeadEntry
        {
            headCollider = headCollider,
            headGO = headCollider.gameObject,
            onCollected = onCollected
        };
        _heads.Add(h);

        // подключить к уже активным бонусам
        foreach (var b in _bonuses)
        {
            var det = h.headGO.AddComponent<SelfCollisionDetector>();
            det.Init(b.col, () => Collect(b, h));
            b.detectors.Add(det);
        }
    }

    public void Tick()
    {
        if (_bonusPrefab == null) return;

        if (Time.time >= _nextSpawnAt && _bonuses.Count < _maxConcurrent)
        {
            SpawnOneBonus();
            ScheduleNextSpawn();
        }
    }

    public void ForceDespawn()
    {
        foreach (var b in _bonuses) CleanupBonusEntry(b);
        _bonuses.Clear();
    }

    // ==== Внутренняя логика ====

    private void SpawnOneBonus()
    {
        if (_bonusPrefab == null) return;
        if (_palette == null || _palette.Count == 0) return;

        // выберем тип/цвет
        var kc = _palette[UnityEngine.Random.Range(0, _palette.Count)];

        // найдём безопасную позицию
        if (!TryFindFreePosition(out Vector2 pos))
        {
            // не нашли — пропускаем спавн
            return;
        }

        var go = Instantiate(_bonusPrefab, pos, Quaternion.identity);

        // визуал: покрасить спрайт
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.color = kc.color;

        // тэг типа
        var tag = go.GetComponent<BonusTag>();
        if (tag == null) tag = go.AddComponent<BonusTag>();
        tag.kind = kc.kind;

        // коллайдер (триггер)
        var col = go.GetComponent<Collider2D>();
        if (col == null)
        {
            var cc = go.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            cc.radius = _fallbackColliderRadius;
            col = cc;
        }
        else col.isTrigger = true;

        var bonus = new BonusEntry { go = go, col = col, kind = kc.kind, color = kc.color  };

        // детекторы подбора на головы
        foreach (var h in _heads)
        {
            var det = h.headGO.AddComponent<SelfCollisionDetector>();
            det.Init(col, () => Collect(bonus, h));
            bonus.detectors.Add(det);
        }

        _bonuses.Add(bonus);
    }

    private void Collect(BonusEntry bonus, HeadEntry who)
    {
        try { who.onCollected?.Invoke(bonus.kind); }
        catch (Exception e) { Debug.LogException(e); }

        // ---- VFX: кольца в цвет бонуса ----
        if (_pickupVfx != null && bonus?.go != null)
        {
            var ps = Instantiate(_pickupVfx, bonus.go.transform.position, Quaternion.identity);

            // красим стартовый цвет
            var main = ps.main;
            var c = (Color)bonus.color; // Color32 -> Color
            main.startColor = new ParticleSystem.MinMaxGradient(c);

            ps.Play();

            // авто-удаление после проигрыша
            float life = 0f;
            var lt = main.startLifetime;
            switch (lt.mode)
            {
                case ParticleSystemCurveMode.Constant:      life = lt.constant; break;
                case ParticleSystemCurveMode.TwoConstants:  life = lt.constantMax; break;
                default:                                    life = 1.0f; break;
            }
            Destroy(ps.gameObject, main.duration + life + 0.5f);
        }
        // -----------------------------------

        CleanupBonusEntry(bonus);
        _bonuses.Remove(bonus);
    }

    private void CleanupBonusEntry(BonusEntry bonus)
    {
        if (bonus == null) return;

        foreach (var d in bonus.detectors)
            if (d != null) Destroy(d);
        bonus.detectors.Clear();

        if (bonus.go != null) Destroy(bonus.go);
        bonus.go = null; bonus.col = null;
    }

    private void ScheduleNextSpawn()
    {
        float a = Mathf.Min(_bonusIntervalRange.x, _bonusIntervalRange.y);
        float b = Mathf.Max(_bonusIntervalRange.x, _bonusIntervalRange.y);
        _nextSpawnAt = Time.time + UnityEngine.Random.Range(a, b);
    }

    // ==== Поиск безопасной позиции (без слоёв) ====

    private bool TryFindFreePosition(out Vector2 pos)
    {
        for (int i = 0; i < _maxPlacementAttempts; i++)
        {
            float x = UnityEngine.Random.Range(_minX + _edgePadding, _maxX - _edgePadding);
            float y = UnityEngine.Random.Range(_minY + _edgePadding, _maxY - _edgePadding);
            Vector2 candidate = new Vector2(x, y);

            if (IsPositionFree(candidate))
            {
                pos = candidate;
                return true;
            }
        }

        pos = default;
        return false;
    }

    /// Проверяем близость к любым коллайдерам (линии/головы и пр.) без использования слоёв.
    /// Игнорируем только сами бонусы (по наличию BonusTag).
    private bool IsPositionFree(Vector2 candidate)
    {
        // Маска слоёв = все (~0), глубины по умолчанию
        int n = Physics2D.OverlapCircleNonAlloc(candidate, Mathf.Max(_spawnClearanceLine, _spawnClearanceHead),
                                                _overlapBuf, ~0, float.NegativeInfinity, float.PositiveInfinity);

        for (int i = 0; i < n; i++)
        {
            var c = _overlapBuf[i];
            if (c == null) continue;

            // игнор своих бонусов
            if (c.GetComponent<BonusTag>() != null)
                continue;

            // определяем «тип» коллайдера по компонентам
            bool isLine = c is EdgeCollider2D || c.GetComponent<EdgeCollider2D>() != null;
            bool isHead = c is CircleCollider2D || c.GetComponent<CircleCollider2D>() != null;

            float clearance = _spawnClearanceLine;
            if (isHead) clearance = _spawnClearanceHead;
            else if (!isLine) clearance = Mathf.Max(_spawnClearanceLine, _spawnClearanceHead); // на всякий случай для прочих

            // расстояние до ближайшей точки
            Vector2 closest = c.ClosestPoint(candidate);
            float dist = Vector2.Distance(closest, candidate);

            if (dist < clearance)
                return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _maxConcurrent = Mathf.Max(0, _maxConcurrent);
        _initialSpawnCount = Mathf.Clamp(_initialSpawnCount, 0, _maxConcurrent);
        _edgePadding = Mathf.Max(0f, _edgePadding);
        _fallbackColliderRadius = Mathf.Max(0.01f, _fallbackColliderRadius);
        _spawnClearanceLine = Mathf.Max(0f, _spawnClearanceLine);
        _spawnClearanceHead = Mathf.Max(0f, _spawnClearanceHead);
        _maxPlacementAttempts = Mathf.Max(1, _maxPlacementAttempts);
        _bonusIntervalRange.x = Mathf.Max(0.1f, _bonusIntervalRange.x);
        _bonusIntervalRange.y = Mathf.Max(0.1f, _bonusIntervalRange.y);
    }
#endif
}
