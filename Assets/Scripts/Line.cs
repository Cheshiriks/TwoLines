using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Line : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineRenderer _renderer;
    [SerializeField] private EdgeCollider2D _collider;

    [Header("Settings")]
    [SerializeField] private float _edgeRadius = 0.1f;

    private readonly List<Vector2> _pointsWorld = new(); // точки в мировых координатах
    private int _excludeHeadPoints = 0;

    public EdgeCollider2D Collider => _collider;
    public int PointCount => _pointsWorld.Count;

    private void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<LineRenderer>();
        if (_collider == null)
            _collider = GetComponent<EdgeCollider2D>();

        if (_renderer != null)
            _renderer.positionCount = 0;

        if (_collider != null)
        {
            _collider.enabled = false;
            _collider.edgeRadius = _edgeRadius;
            _collider.isTrigger = true;
        }
    }

    /// <summary>Сеем первую точку линии в месте старта (во избежание артефакта в (0,0))</summary>
    public void Seed(Vector2 posWorld)
    {
        AppendPoint(posWorld, updateCollider: false);
        UpdateRenderer();
    }

    public void SetExcludeHeadPoints(int exclude)
    {
        _excludeHeadPoints = Mathf.Max(0, exclude);
        UpdateCollider();
    }

    public void SetColor(Color32 color)
    {
        if (_renderer == null) return;
        _renderer.material.color = color;
    }

    public void SetAlpha(float a01)
    {
        if (_renderer == null) return;
        var s = _renderer.startColor;
        var e = _renderer.endColor;
        s.a = a01;
        e.a = a01;
        _renderer.startColor = s;
        _renderer.endColor = e;
    }

    /// <summary>Добавляет новую точку линии (при движении)</summary>
    public void SetPosition(Vector2 posWorld)
    {
        if (!CanAppend(posWorld))
            return;

        AppendPoint(posWorld, updateCollider: true);
        UpdateRenderer();
        UpdateCollider();
    }

    private void AppendPoint(Vector2 posWorld, bool updateCollider)
    {
        _pointsWorld.Add(posWorld);

        if (_renderer != null)
        {
            _renderer.positionCount = _pointsWorld.Count;
            _renderer.SetPosition(_renderer.positionCount - 1, posWorld);
        }

        if (updateCollider)
            UpdateCollider();
    }

    private void UpdateRenderer()
    {
        if (_renderer == null)
            return;

        _renderer.positionCount = _pointsWorld.Count;
        for (int i = 0; i < _pointsWorld.Count; i++)
            _renderer.SetPosition(i, _pointsWorld[i]);
    }

    private void UpdateCollider()
    {
        if (_collider == null)
            return;

        int usableCount = Mathf.Max(0, _pointsWorld.Count - _excludeHeadPoints);

        if (usableCount >= 2)
        {
            var local = new Vector2[usableCount];
            for (int i = 0; i < usableCount; i++)
                local[i] = transform.InverseTransformPoint(_pointsWorld[i]);

            _collider.points = local;
            _collider.edgeRadius = _edgeRadius;
            _collider.enabled = true;
        }
        else
        {
            _collider.enabled = false;
        }
    }

    private bool CanAppend(Vector2 posWorld)
    {
        if (_renderer == null) return true;
        if (_renderer.positionCount == 0) return true;

        Vector3 last = _renderer.GetPosition(_renderer.positionCount - 1);
        return Vector2.Distance(last, posWorld) > DrawManager.Resolution;
    }

    /// <summary>Плавное затухание линии</summary>
    public void FadeOut(float duration = 0.5f, byte targetAlpha = 20)
    {
        if (!gameObject.activeInHierarchy || _renderer == null)
            return;
        StartCoroutine(FadeRoutine(duration, targetAlpha));
    }

    private System.Collections.IEnumerator FadeRoutine(float duration, byte targetAlpha)
    {
        float t = 0f;
        var startColor = _renderer.startColor;
        var endColor = _renderer.endColor;

        float startA = startColor.a;
        float targetA = targetAlpha / 255f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(startA, targetA, k);

            var sc = startColor; var ec = endColor;
            sc.a = ec.a = a;
            _renderer.startColor = sc;
            _renderer.endColor = ec;

            yield return null;
        }
    }
}