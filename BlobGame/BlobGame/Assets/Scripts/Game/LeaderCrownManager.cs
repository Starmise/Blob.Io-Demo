using UnityEngine;

/// <summary>
/// Instantiates crown prefab(s) on the highest total-score player. When split, places a crown on both masses.
/// Runs in LateUpdate so split clone roots exist after PlayerController builds them.
/// </summary>
public class LeaderCrownManager : MonoBehaviour
{
    [Header("References")]
    public GameObject crownPrefab;

    private GameObject _crownPrimary;
    private GameObject _crownClone;
    private string _leaderId;

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

        bool wantCloneCrown = bestState.hasSplit && controller.SplitCloneRoot != null;
        bool haveCloneCrown = _crownClone != null;

        if (bestId == _leaderId && _crownPrimary != null && wantCloneCrown == haveCloneCrown)
            return;

        RebuildCrowns(controller, wantCloneCrown);
        _leaderId = bestId;
    }

    void RebuildCrowns(PlayerController controller, bool addCloneCrown)
    {
        ClearCrowns();

        _crownPrimary = Instantiate(crownPrefab, controller.transform);
        _crownPrimary.transform.localPosition = Vector3.zero;

        if (addCloneCrown && controller.SplitCloneRoot != null)
        {
            _crownClone = Instantiate(crownPrefab, controller.SplitCloneRoot);
            _crownClone.transform.localPosition = Vector3.zero;
        }
    }

    void ClearCrowns()
    {
        if (_crownPrimary != null)
        {
            Destroy(_crownPrimary);
            _crownPrimary = null;
        }
        if (_crownClone != null)
        {
            Destroy(_crownClone);
            _crownClone = null;
        }
        _leaderId = null;
    }
}
