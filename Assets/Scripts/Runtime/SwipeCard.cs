using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 좌우로 드래그해서 결정하는 카드 (왼쪽 = 거절, 오른쪽 = 수락).
/// 판정만 알려주고, 실제 처리는 구독자(ReceptionView)가 담당합니다.
/// </summary>
public class SwipeCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("드래그 설정")]
    [Tooltip("이 거리(캔버스 단위) 이상 끌고 놓으면 결정으로 판정")]
    public float decideDistance = 260f;
    [Tooltip("끝까지 끌었을 때 기울어지는 각도")]
    public float maxTiltAngle = 10f;

    [Header("연출")]
    public float snapBackDuration = 0.15f;
    public float flyOutDuration = 0.25f;
    public float flyOutDistance = 1400f;
    public float enterDuration = 0.3f;
    [Tooltip("등장 시작 크기 (멀리서 걸어오는 느낌)")]
    public float enterStartScale = 0.6f;

    public Canvas canvas;

    // 드래그 진행도: -1(왼쪽 끝) ~ 1(오른쪽 끝)
    public event Action<float> Dragged;
    // 결정 거리 이상에서 손을 뗌: true = 오른쪽, false = 왼쪽
    public event Action<bool> Released;

    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Vector2 homePosition;
    private bool interactable = true;
    private bool dragging;
    private Coroutine motion;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        homePosition = rect.anchoredPosition;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
    }

    float Offset => rect.anchoredPosition.x - homePosition.x;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!interactable) return;
        dragging = true;
        StopMotion();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        SetOffset(Offset + eventData.delta.x / scale);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;

        if (Mathf.Abs(Offset) >= decideDistance)
        {
            // 구독자가 FlyOut 또는 SnapBack을 호출할 때까지 추가 입력을 막음
            interactable = false;
            if (Released != null) Released(Offset > 0f);
            else SnapBack();
        }
        else
        {
            SnapBack();
        }
    }

    void SetOffset(float offset)
    {
        rect.anchoredPosition = new Vector2(homePosition.x + offset, homePosition.y);

        float progress = decideDistance > 0f ? Mathf.Clamp(offset / decideDistance, -1f, 1f) : 0f;
        rect.localRotation = Quaternion.Euler(0f, 0f, -progress * maxTiltAngle);
        Dragged?.Invoke(progress);
    }

    // 제자리로 되돌림 (결정이 거부되었을 때)
    public void SnapBack()
    {
        StopMotion();
        motion = StartCoroutine(SnapBackRoutine());
    }

    IEnumerator SnapBackRoutine()
    {
        float start = Offset;
        for (float t = 0f; t < snapBackDuration; t += Time.unscaledDeltaTime)
        {
            SetOffset(Mathf.Lerp(start, 0f, t / snapBackDuration));
            yield return null;
        }
        SetOffset(0f);
        interactable = true;
        motion = null;
    }

    // 결정된 방향으로 화면 밖으로 날려 보낸 뒤 onDone 호출
    public void FlyOut(bool toRight, Action onDone)
    {
        StopMotion();
        motion = StartCoroutine(FlyOutRoutine(toRight, onDone));
    }

    IEnumerator FlyOutRoutine(bool toRight, Action onDone)
    {
        interactable = false;
        float start = Offset;
        float end = toRight ? flyOutDistance : -flyOutDistance;
        for (float t = 0f; t < flyOutDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / flyOutDuration;
            rect.anchoredPosition = new Vector2(homePosition.x + Mathf.Lerp(start, end, k), homePosition.y);
            if (canvasGroup != null) canvasGroup.alpha = 1f - k;
            yield return null;
        }
        motion = null;
        onDone?.Invoke();
    }

    // 다음 모험가가 접수대로 걸어오는 등장 연출
    public void PlayEnter()
    {
        StopMotion();
        ResetCard();
        motion = StartCoroutine(EnterRoutine());
    }

    IEnumerator EnterRoutine()
    {
        interactable = false;
        for (float t = 0f; t < enterDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / enterDuration;
            rect.localScale = Vector3.one * Mathf.Lerp(enterStartScale, 1f, k);
            if (canvasGroup != null) canvasGroup.alpha = k;
            yield return null;
        }
        ResetCard();
        motion = null;
    }

    // 위치/회전/투명도를 즉시 초기 상태로
    public void ResetCard()
    {
        if (rect == null) return; // Awake 전
        dragging = false;
        interactable = true;
        rect.anchoredPosition = homePosition;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        Dragged?.Invoke(0f);
    }

    void StopMotion()
    {
        if (motion != null) StopCoroutine(motion);
        motion = null;
    }

    void OnDisable()
    {
        // 비활성화되면 코루틴이 끊기므로, 다시 켜졌을 때 어중간한 상태로 남지 않게 함
        motion = null;
        ResetCard();
    }
}
