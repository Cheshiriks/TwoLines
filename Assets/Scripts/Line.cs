using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Line : MonoBehaviour
{
    [SerializeField] private LineRenderer _renderer;
    [SerializeField] private EdgeCollider2D _collider;
    [SerializeField] private float scaleCollider = 0.1f;
    [SerializeField] private int _excludeHeadPoints = 3;

    private readonly List<Vector2> _points = new List<Vector2>();
    
    private Coroutine _fadeRoutine;
    private Color32 _baseColor;

    public EdgeCollider2D Collider => _collider;
    public int PointCount => _renderer.positionCount;
    public Vector2 GetPoint(int i) => _renderer.GetPosition(i);
    public Vector2 GetLastPoint() => _renderer.positionCount > 0 ? _renderer.GetPosition(_renderer.positionCount - 1) : Vector2.zero;

    void Awake()
    {
        if (!TryGetComponent<Rigidbody2D>(out var rb))
            rb = gameObject.AddComponent<Rigidbody2D>();
        rb.isKinematic = true;
        rb.gravityScale = 0f;
    }

    void Start()
    {
        _collider.transform.position -= transform.position;
    }

    public void SetPosition(Vector2 pos)
    {
        if (!CanAppend(pos)) return;

        _points.Add(pos);

        _renderer.positionCount++;
        _renderer.SetPosition(_renderer.positionCount - 1, pos);

        UpdateEdgeColliderPoints();
    }

    private void UpdateEdgeColliderPoints()
    {
        if (_points.Count < 2) return;

        int cut = Mathf.Clamp(_excludeHeadPoints, 0, Mathf.Max(0, _points.Count - 2));
        int usableCount = _points.Count - cut;

        if (usableCount >= 2)
        {
            var arr = new Vector2[usableCount];
            for (int i = 0; i < usableCount; i++)
                arr[i] = _points[i];

            _collider.points = arr;
            _collider.edgeRadius = scaleCollider;
        }
    }

    private bool CanAppend(Vector2 pos)
    {
        if (_renderer.positionCount == 0) return true;
        return Vector2.Distance(_renderer.GetPosition(_renderer.positionCount - 1), pos) > DrawManager.Resolution;
    }

    public void SetExcludeHeadPoints(int count)
    {
        _excludeHeadPoints = Mathf.Max(0, count);
        UpdateEdgeColliderPoints();
    }

    public void SetColor(Color32 color)
    {
        _baseColor = color;
        _renderer.material.color = color;
    }
    
    // === Затухание (остается слегка видимой) ===
    public void FadeOut(float duration = 0.1f, byte targetAlpha = 20)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(duration, targetAlpha));
    }

    private IEnumerator FadeRoutine(float duration, byte targetAlpha)
    {
        float t = 0f;
        byte startA = _baseColor.a;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            byte newA = (byte)Mathf.Lerp(startA, targetAlpha, k);
            var newColor = new Color32(_baseColor.r, _baseColor.g, _baseColor.b, newA);
            _renderer.material.color = newColor;
            yield return null;
        }
        
    }
}
