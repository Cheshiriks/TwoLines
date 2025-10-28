using UnityEngine;
using System;
using System.Collections.Generic;

/// Система спавна бонусов с поддержкой нескольких активных одновременно.
public class BonusSystem : MonoBehaviour
{
    [Header("Bonus Prefab & Spawn")]
    [SerializeField] private GameObject _bonusPrefab;
    [Tooltip("Диапазон задержек между спавнами (сек). Каждый раз планируется новая задержка в этом диапазоне.")]
    [SerializeField] private Vector2 _bonusIntervalRange = new Vector2(3f, 6f);

    [Tooltip("Максимум одновременных бонусов на карте.")]
    [SerializeField] private int _maxConcurrent = 3;

    [Tooltip("Отступ от границ видимой области при спавне (в мировых единицах).")]
    [SerializeField] private float _edgePadding = 0.3f;

    [Header("Collider (если на префабе нет своего)")]
    [Tooltip("Радиус CircleCollider2D, если у префаба нет собственного коллайдера.")]
    [SerializeField] private float _fallbackColliderRadius = 0.5f;

    [Header("Initial Spawn")]
    [Tooltip("Сколько бонусов попытаться заспавнить сразу при старте (не превышая MaxConcurrent).")]
    [SerializeField] private int _initialSpawnCount = 1;

    // --- Runtime state ---
    private float _minX, _maxX, _minY, _maxY;
    private float _nextSpawnAt = 0f;

    private readonly List<HeadEntry> _heads = new();         // зарегистрированные головы (кто может собирать бонусы)
    private readonly List<BonusEntry> _bonuses = new();      // активные бонусы

    private class HeadEntry
    {
        public Collider2D headCollider;
        public GameObject headGO;
        public Action onCollected;
    }

    private class BonusEntry
    {
        public GameObject go;
        public Collider2D col;
        public readonly List<SelfCollisionDetector> detectors = new(); // детекторы на головах, привязанные к этому бонусу
    }

    // --- Public API ---

    /// Задать границы спавна по текущей камере (+ опционально переопределить padding).
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

        // Начальный спавн (по желанию)
        int count = Mathf.Clamp(_initialSpawnCount, 0, _maxConcurrent);
        for (int i = 0; i < count; i++)
        {
            if (_bonuses.Count >= _maxConcurrent) break;
            SpawnOneBonus();
        }
    }

    /// Регистрируем голову: при появлении бонусов ей навешиваем детекторы, а при подборе вызываем колбэк.
    public void RegisterHead(Collider2D headCollider, Action onCollected)
    {
        if (headCollider == null || onCollected == null)
        {
            Debug.LogWarning("BonusSystem.RegisterHead: пустой headCollider или onCollected");
            return;
        }

        var entry = new HeadEntry
        {
            headCollider = headCollider,
            headGO = headCollider.gameObject,
            onCollected = onCollected
        };
        _heads.Add(entry);

        // Для уже активных бонусов сразу повесим детекторы
        foreach (var b in _bonuses)
        {
            var det = entry.headGO.AddComponent<SelfCollisionDetector>();
            det.Init(b.col, () => Collect(b, entry));
            b.detectors.Add(det);
        }
    }

    /// Вызывать из Update() гейм-менеджера.
    public void Tick()
    {
        if (_bonusPrefab == null) return;

        // Спавним по расписанию, пока не достигли лимита
        if (Time.time >= _nextSpawnAt && _bonuses.Count < _maxConcurrent)
        {
            SpawnOneBonus();
            ScheduleNextSpawn();
        }
    }

    /// Принудительно удалить все активные бонусы (например, при окончании игры).
    public void ForceDespawn()
    {
        foreach (var b in _bonuses)
        {
            CleanupBonusEntry(b);
        }
        _bonuses.Clear();
    }

    // --- Internal ---

    private void SpawnOneBonus()
    {
        if (_bonusPrefab == null) return;

        // случайная позиция внутри камеры с отступом
        float x = UnityEngine.Random.Range(_minX + _edgePadding, _maxX - _edgePadding);
        float y = UnityEngine.Random.Range(_minY + _edgePadding, _maxY - _edgePadding);
        Vector2 pos = new Vector2(x, y);

        var go = Instantiate(_bonusPrefab, pos, Quaternion.identity);

        // гарантируем коллайдер-триггер
        var col = go.GetComponent<Collider2D>();
        if (col == null)
        {
            var cc = go.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            cc.radius = _fallbackColliderRadius;
            col = cc;
        }
        else
        {
            col.isTrigger = true;
        }

        var bonus = new BonusEntry { go = go, col = col };

        // навесим детекторы на все зарегистрированные головы
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
        // колбэк игрока
        try { who.onCollected?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }

        // удалить только этот бонус
        if (bonus != null)
        {
            CleanupBonusEntry(bonus);
            _bonuses.Remove(bonus);
        }
    }

    private void CleanupBonusEntry(BonusEntry bonus)
    {
        if (bonus == null) return;

        // снять детекторы
        foreach (var d in bonus.detectors)
            if (d != null) Destroy(d);
        bonus.detectors.Clear();

        // удалить объект
        if (bonus.go != null) Destroy(bonus.go);
        bonus.go = null;
        bonus.col = null;
    }

    private void ScheduleNextSpawn()
    {
        float min = Mathf.Min(_bonusIntervalRange.x, _bonusIntervalRange.y);
        float max = Mathf.Max(_bonusIntervalRange.x, _bonusIntervalRange.y);
        _nextSpawnAt = Time.time + UnityEngine.Random.Range(min, max);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _bonusIntervalRange.x = Mathf.Max(0.1f, _bonusIntervalRange.x);
        _bonusIntervalRange.y = Mathf.Max(0.1f, _bonusIntervalRange.y);
        _maxConcurrent = Mathf.Max(0, _maxConcurrent);
        _initialSpawnCount = Mathf.Max(0, _initialSpawnCount);
        _fallbackColliderRadius = Mathf.Max(0.01f, _fallbackColliderRadius);
        _edgePadding = Mathf.Max(0f, _edgePadding);
    }
#endif
}
