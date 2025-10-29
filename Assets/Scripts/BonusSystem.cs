using UnityEngine;
using System;
using System.Collections.Generic;

public class BonusSystem : MonoBehaviour
{
    [Header("Prefab & Spawn")]
    [SerializeField] private GameObject _bonusPrefab;
    [SerializeField] private Vector2 _bonusIntervalRange = new Vector2(3f, 6f);
    [SerializeField] private int _maxConcurrent = 3;
    [SerializeField] private float _edgePadding = 0.3f;
    [SerializeField] private float _fallbackColliderRadius = 0.5f;
    [SerializeField] private int _initialSpawnCount = 1;

    [Header("Kinds & Colors (Color32)")]
    [Tooltip("Пул типов и их цветов. Система выбирает случайный тип из этого списка.")]
    [SerializeField] private List<KindColor> _palette = new List<KindColor>
    {
        new KindColor{ kind = BonusKind.SpeedUp,        color = new Color32( 80, 255,  80, 255) },
        new KindColor{ kind = BonusKind.SpeedDown,      color = new Color32(255,  80,  80, 255) },
        new KindColor{ kind = BonusKind.Invulnerability,color = new Color32(255, 215,   0, 255) },
        new KindColor{ kind = BonusKind.PenOff,         color = new Color32( 80, 180, 255, 255) },
    };

    [Serializable]
    private struct KindColor
    {
        public BonusKind kind;
        public Color32 color;
    }

    // --- runtime ---
    private float _minX, _maxX, _minY, _maxY;
    private float _nextSpawnAt;

    private readonly List<HeadEntry> _heads = new();
    private readonly List<BonusEntry> _bonuses = new();

    private class HeadEntry
    {
        public Collider2D headCollider;
        public GameObject headGO;
        public Action<BonusKind> onCollected; // <--- тип
    }

    private class BonusEntry
    {
        public GameObject go;
        public Collider2D col;
        public BonusKind kind;
        public readonly List<SelfCollisionDetector> detectors = new();
    }

    // === API ===
    public void InitializeFromCamera(Camera cam, float paddingOverride = -1f)
    {
        var bl = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var tr = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        _minX = bl.x; _maxX = tr.x; _minY = bl.y; _maxY = tr.y;
        if (paddingOverride >= 0f) _edgePadding = paddingOverride;

        ScheduleNextSpawn();

        int count = Mathf.Clamp(_initialSpawnCount, 0, _maxConcurrent);
        for (int i = 0; i < count; i++)
        {
            if (_bonuses.Count >= _maxConcurrent) break;
            SpawnOneBonus();
        }
    }

    public void RegisterHead(Collider2D headCollider, Action<BonusKind> onCollected)
    {
        if (headCollider == null || onCollected == null) return;

        var entry = new HeadEntry
        {
            headCollider = headCollider,
            headGO = headCollider.gameObject,
            onCollected = onCollected
        };
        _heads.Add(entry);

        // подключить ко всем уже активным бонусам
        foreach (var b in _bonuses)
        {
            var det = entry.headGO.AddComponent<SelfCollisionDetector>();
            det.Init(b.col, () => Collect(b, entry));
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

    // === spawn / collect ===
    private void SpawnOneBonus()
    {
        if (_palette == null || _palette.Count == 0) return;

        // выбор типа и цвета
        var kc = _palette[UnityEngine.Random.Range(0, _palette.Count)];

        float x = UnityEngine.Random.Range(_minX + _edgePadding, _maxX - _edgePadding);
        float y = UnityEngine.Random.Range(_minY + _edgePadding, _maxY - _edgePadding);
        Vector2 pos = new Vector2(x, y);

        var go = Instantiate(_bonusPrefab, pos, Quaternion.identity);

        // визуал: покрасить спрайт
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.color = kc.color;

        // коллайдер-триггер
        var col = go.GetComponent<Collider2D>();
        if (col == null)
        {
            var cc = go.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            cc.radius = _fallbackColliderRadius;
            col = cc;
        }
        else col.isTrigger = true;

        // тэг с типом
        var tag = go.GetComponent<BonusTag>();
        if (tag == null) tag = go.AddComponent<BonusTag>();
        tag.kind = kc.kind;

        var bonus = new BonusEntry { go = go, col = col, kind = kc.kind };

        // детекторы для всех голов
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
        finally
        {
            CleanupBonusEntry(bonus);
            _bonuses.Remove(bonus);
        }
    }

    private void CleanupBonusEntry(BonusEntry bonus)
    {
        foreach (var d in bonus.detectors) if (d != null) Destroy(d);
        bonus.detectors.Clear();
        if (bonus.go != null) Destroy(bonus.go);
        bonus.go = null; bonus.col = null;
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
        _maxConcurrent = Mathf.Max(0, _maxConcurrent);
        _initialSpawnCount = Mathf.Clamp(_initialSpawnCount, 0, _maxConcurrent);
        _edgePadding = Mathf.Max(0f, _edgePadding);
        _fallbackColliderRadius = Mathf.Max(0.01f, _fallbackColliderRadius);
        _bonusIntervalRange.x = Mathf.Max(0.1f, _bonusIntervalRange.x);
        _bonusIntervalRange.y = Mathf.Max(0.1f, _bonusIntervalRange.y);
    }
#endif
}
