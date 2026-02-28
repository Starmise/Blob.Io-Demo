using UnityEngine;
using Colyseus.Schema;

/// <summary>
/// Instantiates a crown prefab as a child of the player
/// with the highest score. When the leader changes, the
/// current crown is destroyed and re-instantiated on the new leader.
/// </summary>
public class LeaderCrownManager : MonoBehaviour
{
    [Header("References")]
    public GameObject crownPrefab;

    private GameObject _currentCrown;
    private string _currentLeaderId;

    void Update()
    {
        if (NetworkManager.Instance == null ||
            NetworkManager.Instance.Room == null ||
            GameManager.Instance == null ||
            crownPrefab == null)
        {
            return;
        }

        var room = NetworkManager.Instance.Room;

        string bestId = null;
        PlayerState bestState = null;

        room.State.players.ForEach((sessionId, state) =>
        {
            if (!state.isAlive) return;

            if (bestState == null || state.score > bestState.score)
            {
                bestState = state;
                bestId = sessionId;
            }
        });

        // No alive players
        if (bestId == null)
        {
            if (_currentCrown != null)
            {
                Destroy(_currentCrown);
                _currentCrown = null;
                _currentLeaderId = null;
            }
            return;
        }

        // Same leader as before, nothing to do.
        if (bestId == _currentLeaderId && _currentCrown != null)
            return;

        // log when we are about to change leader/crown
        if (bestId != _currentLeaderId)
        {
            Debug.Log($"[LeaderCrownManager] leader changed from '{_currentLeaderId}' to '{bestId}'");
        }

        // Leader changed: destroy old crown
        if (_currentCrown != null)
        {
            Destroy(_currentCrown);
            _currentCrown = null;
        }

        // Get PlayerController for new leader
        if (!GameManager.Instance.TryGetPlayer(bestId, out var controller) ||
            controller == null)
        {
            Debug.LogWarning($"[LeaderCrownManager] Could not find PlayerController for leader {bestId}");
            _currentLeaderId = null;
            return;
        }

        // Instantiate new crown as child of the leader
        _currentCrown = Instantiate(crownPrefab, controller.transform);
        _currentCrown.transform.localPosition = Vector3.zero;
        _currentLeaderId = bestId;
        Debug.Log($"[LeaderCrownManager] instantiated crown on player {bestId}");
    }
}

