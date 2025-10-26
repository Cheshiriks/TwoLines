using UnityEngine;

public class SelfCollisionDetector : MonoBehaviour
{
    private System.Action _onHit;
    private EdgeCollider2D _target;

    public void Init(EdgeCollider2D target, System.Action onHit)
    {
        _target = target;
        _onHit = onHit;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_target != null && other == _target)
            _onHit?.Invoke();
    }
}
