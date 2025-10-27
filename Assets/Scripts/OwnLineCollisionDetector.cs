using System.Collections.Generic;
using UnityEngine;

public class OwnLineCollisionDetector : MonoBehaviour
{
    private HashSet<Collider2D> _targets = new HashSet<Collider2D>();
    private System.Func<bool> _isGameRunning;
    private System.Func<bool> _isCurrentSegmentArmed;
    private System.Func<Collider2D> _getCurrentSegmentCollider;
    private System.Action _onSelfHit;

    public void Init(
        System.Func<bool> isGameRunning,
        System.Func<bool> isCurrentSegmentArmed,
        System.Func<Collider2D> getCurrentSegmentCollider,
        System.Action onSelfHit)
    {
        _isGameRunning = isGameRunning;
        _isCurrentSegmentArmed = isCurrentSegmentArmed;
        _getCurrentSegmentCollider = getCurrentSegmentCollider;
        _onSelfHit = onSelfHit;
    }

    public void AddTarget(Collider2D col)
    {
        if (col != null) _targets.Add(col);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isGameRunning != null && !_isGameRunning()) return;
        if (other == null || !_targets.Contains(other)) return;

        // Если это текущий сегмент — триггерим только после «взведения»
        var currentCol = _getCurrentSegmentCollider?.Invoke();
        if (other == currentCol && (_isCurrentSegmentArmed != null && !_isCurrentSegmentArmed()))
            return;

        _onSelfHit?.Invoke();
    }
}
