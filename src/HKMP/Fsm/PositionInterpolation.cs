using System.Collections;
using UnityEngine;

namespace Hkmp.Fsm;

/// <summary>
/// MonoBehaviour for interpolating position between position updates.
/// </summary>
internal class PositionInterpolation : MonoBehaviour {
    /// <summary>
    /// The duration between interpolation of positions.
    /// </summary>
    private const float Duration = 1f / 60f;

    /// <summary>
    /// Whether player avatars should be smoothed between network updates. Entity positions are never
    /// interpolated, since the scene host is authoritative for them.
    /// </summary>
    public static bool SmoothPlayers { get; set; } = true;

    /// <summary>
    /// Whether this instance should smooth the positions it receives. Only set for player avatars.
    /// </summary>
    public bool Smooth { get; set; }

    /// <summary>
    /// The last coroutine for interpolation.
    /// </summary>
    private Coroutine _lastCoroutine;

    /// <summary>
    /// Whether this is the first update.
    /// </summary>
    private bool _firstUpdate = true;

    /// <summary>
    /// Set the new position to interpolate to.
    /// </summary>
    /// <param name="newPosition">The new position as Vector3.</param>
    public void SetNewPosition(Vector3 newPosition) {
        if (!Smooth || !SmoothPlayers) {
            if (_lastCoroutine != null) {
                StopCoroutine(_lastCoroutine);
                _lastCoroutine = null;
            }

            transform.localPosition = newPosition;
            _firstUpdate = true;
            return;
        }

        if (_firstUpdate) {
            transform.localPosition = newPosition;

            _firstUpdate = false;
            return;
        }

        if (_lastCoroutine != null) {
            StopCoroutine(_lastCoroutine);
        }

        _lastCoroutine = StartCoroutine(LerpPosition(newPosition, Duration));
    }

    /// <summary>
    /// Lerp the position of this instance to the target position over the given duration.
    /// </summary>
    /// <param name="targetPosition">The target position as Vector3.</param>
    /// <param name="duration">The duration as float.</param>
    /// <returns>An enumerator for this coroutine.</returns>
    private IEnumerator LerpPosition(Vector3 targetPosition, float duration) {
        var time = 0f;
        var startPosition = transform.localPosition;

        while (time < duration) {
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = targetPosition;
        _lastCoroutine = null;
    }
}
