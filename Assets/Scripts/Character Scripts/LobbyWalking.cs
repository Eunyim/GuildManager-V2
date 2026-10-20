using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LobbyWalking : MonoBehaviour
{
    public float moveSpeed = 50f;      // 이동 속도
    public float waitTimeMin = 2f;     // 최소 대기 시간
    public float waitTimeMax = 5f;     // 최대 대기 시간

    private RectTransform myRect;      // 내 몸통
    private RectTransform parentRect;  // 활동 구역(부모)
    private Vector2 targetPosition;    // 목적지
    private bool isWalking = false;    // 걷는 중인가?

    void Start()
    {
        myRect = GetComponent<RectTransform>();
        parentRect = transform.parent.GetComponent<RectTransform>();

        // 치비가 시설 오브젝트 위를 지나가도 클릭을 가로막지 않도록 함
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = false;

        // 시작하면 바로 다음 행동 개시
        StartCoroutine(ThinkRoutine());
    }

    void Update()
    {
        if (isWalking)
        {
            // 1. 목적지 쪽으로 조금씩 이동
            myRect.anchoredPosition = Vector2.MoveTowards(
                myRect.anchoredPosition, 
                targetPosition, 
                moveSpeed * Time.deltaTime
            );

            // 2. 목적지에 도착했으면 멈춤
            if (Vector2.Distance(myRect.anchoredPosition, targetPosition) < 1f)
            {
                isWalking = false;
            }
        }
    }

    // AI의 생각 루틴 (생각 -> 이동 -> 대기 -> 반복)
    IEnumerator ThinkRoutine()
    {
        while (true)
        {
            // 1. 랜덤한 목적지 정하기
            PickRandomDestination();

            // 2. 걷기 시작 (방향 전환 포함)
            isWalking = true;
            FlipSprite(); 

            // 3. 도착할 때까지 기다림 (Update에서 도착하면 isWalking을 끔)
            yield return new WaitUntil(() => !isWalking);

            // 4. 도착 후 잠시 휴식 (랜덤 시간)
            float waitTime = Random.Range(waitTimeMin, waitTimeMax);
            yield return new WaitForSeconds(waitTime);
        }
    }

    // 타일맵 바닥 위에서 랜덤 좌표 뽑기 (타일맵이 없으면 부모 영역 안에서 뽑음)
    void PickRandomDestination()
    {
        if (LobbyMapGenerator.Instance != null
            && LobbyMapGenerator.Instance.TryGetRandomFloorAnchoredPosition(myRect, out Vector2 floorPos))
        {
            targetPosition = floorPos;
            return;
        }

        float rangeX = parentRect.rect.width / 2f - 50f; // 가장자리 여유 50
        float rangeY = parentRect.rect.height / 2f - 50f;

        float randX = Random.Range(-rangeX, rangeX);
        float randY = Random.Range(-rangeY, rangeY);

        targetPosition = new Vector2(randX, randY);
    }

    // 왼쪽/오른쪽 보는 방향 뒤집기
    void FlipSprite()
    {
        // 목적지가 내 현재 위치보다 오른쪽이면 -> 그대로 (Scale X = 1)
        // 목적지가 내 현재 위치보다 왼쪽이면   -> 뒤집기 (Scale X = -1)
        if (targetPosition.x > myRect.anchoredPosition.x)
            transform.localScale = new Vector3(1, 1, 1);
        else
            transform.localScale = new Vector3(-1, 1, 1);
    }
}