using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FloatingScore : MonoBehaviour
{
    public Text label;
    public float duration = 1.2f;
    public float riseSpeed = 80f;
    public Canvas worldCanvas;

    const float RectWidth = 100f;
    const float RectHeight = 34f;

    static Font s_cachedFont;
    static Transform s_poolRoot;
    static readonly Stack<FloatingScore> s_pool = new Stack<FloatingScore>();

    Camera _billboardCamera;
    Outline _outline;
    Color _textBaseColor = Color.white;

    static Font CachedFont
    {
        get
        {
            if (s_cachedFont == null)
                s_cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return s_cachedFont;
        }
    }

    static void EnsurePoolRoot()
    {
        if (s_poolRoot != null)
            return;
        var root = new GameObject("FloatingScorePool");
        Object.DontDestroyOnLoad(root);
        s_poolRoot = root.transform;
    }

    public static void Spawn(Canvas canvas, Vector3 worldPos, int points, Camera cam)
    {
        if (canvas == null || cam == null)
            return;

        EnsurePoolRoot();

        FloatingScore fs;
        GameObject go;

        if (s_pool.Count > 0)
        {
            fs = s_pool.Pop();
            go = fs.gameObject;
            go.SetActive(true);
            go.transform.SetParent(canvas.transform, false);
        }
        else
        {
            go = new GameObject("FloatingScore");
            go.transform.SetParent(canvas.transform, false);

            var rect = go.AddComponent<RectTransform>();
            var txt = go.AddComponent<Text>();
            fs = go.AddComponent<FloatingScore>();

            txt.font = CachedFont;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(0.6f, -0.6f);

            fs.label = txt;
            fs._outline = outline;
            rect.sizeDelta = new Vector2(RectWidth, RectHeight);
        }

        var text = fs.label;
        text.raycastTarget = false;
        int fontSize = points >= 1000 ? 20 : 16;
        text.fontSize = fontSize;
        text.text = points >= 1000
            ? $"+{PlayerController.FormatNumber(points)}"
            : $"+{points}";

        fs._textBaseColor = Color.white;
        text.color = fs._textBaseColor;
        if (fs._outline != null)
            fs._outline.effectColor = Color.black;

        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
        var canvasRect = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            cam,
            out Vector2 localPos);

        var rt = fs.transform as RectTransform;
        rt.localPosition = localPos;
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(RectWidth, RectHeight);

        fs._billboardCamera = cam;
        fs.StartCoroutine(fs.Animate());
    }

    IEnumerator Animate()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.localPosition;
        var baseColor = _textBaseColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localPosition = startPos + Vector3.up * (riseSpeed * t);

            float alpha = t < 0.5f ? 1f : 1f - ((t - 0.5f) / 0.5f);
            baseColor.a = alpha;
            label.color = baseColor;

            float scale = t < 0.1f ? Mathf.Lerp(0f, 1.2f, t / 0.1f)
                : t < 0.2f ? Mathf.Lerp(1.2f, 1f, (t - 0.1f) / 0.1f)
                : 1f;
            transform.localScale = Vector3.one * scale;

            if (_billboardCamera != null)
                transform.rotation = _billboardCamera.transform.rotation;

            yield return null;
        }

        ReturnToPool();
    }

    void ReturnToPool()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
        transform.SetParent(s_poolRoot, false);
        s_pool.Push(this);
    }
}
