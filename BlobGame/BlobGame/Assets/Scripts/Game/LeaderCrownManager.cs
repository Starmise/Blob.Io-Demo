using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instantiates crown prefab(s) on the highest total-score player. When split, places a crown on each mass cell.
/// Runs in LateUpdate so split clone roots exist after PlayerController builds them.
/// </summary>
public class LeaderCrownManager : MonoBehaviour
{
    [Header("References")]
    public GameObject crownPrefab;

    private GameObject _crownPrimary;
    private readonly List<GameObject> _crownSplits = new List<GameObject>();
    private string _leaderId;
    private int _lastSplitCount = -1;

    void LateUpdate()
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
        int bestTotal = -1;

        room.State.players.ForEach((sessionId, state) =>
        {
            if (!state.isAlive) return;

            int total = PlayerController.GetTotalDisplayedScore(state);
            if (total > bestTotal)
            {
                bestTotal = total;
                bestState = state;
                bestId = sessionId;
            }
        });

        if (bestId == null || bestState == null)
        {
            ClearCrowns();
            return;
        }

        if (!GameManager.Instance.TryGetPlayer(bestId, out var controller) || controller == null)
        {
            ClearCrowns();
            return;
        }

        int splitCount = bestState.splitCells != null ? bestState.splitCells.Count : 0;

        if (bestId == _leaderId &&
            _crownPrimary != null &&
            splitCount == _lastSplitCount &&
            _crownSplits.Count == splitCount)
            return;

        RebuildCrowns(controller, splitCount);
        _leaderId = bestId;
        _lastSplitCount = splitCount;
    }

    void RebuildCrowns(PlayerController controller, int splitCount)
    {
        ClearCrowns();

        _crownPrimary = Instantiate(crownPrefab, controller.transform);
        _crownPrimary.transform.localPosition = Vector3.zero;

        for (int i = 0; i < splitCount; i++)
        {
            var root = controller.GetSplitCloneRoot(i);
            if (root == null) continue;
            var c = Instantiate(crownPrefab, root);
            c.transform.localPosition = Vector3.zero;
            _crownSplits.Add(c);
        }
    }

    void ClearCrowns()
    {
        if (_crownPrimary != null)
        {
            Destroy(_crownPrimary);
            _crownPrimary = null;
        }
        for (int i = 0; i < _crownSplits.Count; i++)
        {
            if (_crownSplits[i] != null)
                Destroy(_crownSplits[i]);
        }
        _crownSplits.Clear();
        _leaderId = null;
        _lastSplitCount = -1;
    }
}
