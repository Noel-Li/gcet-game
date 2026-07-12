using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Line-renderer hanzi stroke tracer. Draws a guide stroke, captures the user's mouse-drawn stroke, and scores it on release.
///
/// Scoring (start/end proximity, point-coverage, average distance, and direction) is unchanged. The behaviour additions are:
///   - Publishes a <see cref="OnCharacterDone"/> static event (true on correct completion, false otherwise)
///     so other scripts / scenes (NPC dialogue flow, scene routing) can react to a correct trace without polling.
///   - Loaded as a single-scene modal by <see cref="Dialogue"/> when its writing step is reached.
///</summary>
public class Script1 : MonoBehaviour
{
    public CharacterData characterData;

    public LineRenderer guideLine;
    public LineRenderer userLine;
    public Transform arrowTransform;

    private List<Vector3> userPoints = new List<Vector3>();
    private Camera mainCamera;

    private float tolerance = 0.45f;
    private float requiredScore = 0.8f;
    private float maxAverageDistance = 0.3f;

    private float requiredDirectionScore = 0.85f;
    private float allowedBackwardMovement = 0.03f;

    private float arrowSpeed = 0.7f;
    private float arrowProgress = 0f;
    private float arrowRotationOffset = -90f;

    private int currentStrokeIndex = 0;
    private Vector3[] targetStroke
    {
        get
        {
            return characterData.strokes[currentStrokeIndex].points.ToArray();
        }
    }

    /// <summary>Fired on every scoring attempt. true = the whole character was completed correctly.</summary>
    public static event Action<bool> OnCharacterDone;

    /// <summary>True once the character has been fully and correctly traced.</summary>
    public bool IsComplete { get; private set; }

    void Start()
    {

        if (characterData == null || characterData.strokes.Count == 0)
        {
            Debug.LogError("No character data assigned!");
            enabled = false;
            return;
        }

        for (int i = 0; i < characterData.strokes.Count; i++)
        {
            if (characterData.strokes[i].points == null || characterData.strokes[i].points.Count < 2)
            {
                Debug.LogError("Stroke " + (i + 1) + " does not have enough points.");
                enabled = false;
                return;
            }
        }


        mainCamera = Camera.main;

        SetupLine(guideLine, 0.12f, 0, Color.gray);
        SetupLine(userLine, 0.10f, 10, Color.red);

        DrawGuideStroke();

        userLine.positionCount = 0;

        Debug.Log("Step 8 started: trace " + characterData.characterName + ". Stroke 1.");
    }

    void Update()
    {
        AnimateArrow();

        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            userPoints.Clear();
            userLine.positionCount = 0;
            SetLineColor(userLine, Color.red);

            Vector3 startPoint = GetMouseWorldPosition();
            AddUserPoint(startPoint);
        }

        if (Mouse.current.leftButton.isPressed)
        {
            Vector3 currentPoint = GetMouseWorldPosition();

            if (userPoints.Count == 0 || Vector3.Distance(userPoints[userPoints.Count - 1], currentPoint) > 0.05f)
            {
                AddUserPoint(currentPoint);
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            CheckTrace();
        }
    }

    void SetupLine(LineRenderer line, float width, int sortingOrder, Color color)
    {
        line.positionCount = 0;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;
        line.sortingOrder = sortingOrder;

        line.material = new Material(Shader.Find("Sprites/Default"));

        line.startColor = color;
        line.endColor = color;
    }

    void SetLineColor(LineRenderer line, Color color)
    {
        line.startColor = color;
        line.endColor = color;
    }

    void DrawGuideStroke()
    {
        guideLine.positionCount = targetStroke.Length;

        for (int i = 0; i < targetStroke.Length; i++)
        {
            guideLine.SetPosition(i, targetStroke[i]);
        }

        arrowProgress = 0f;
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(
            new Vector3(
                mouseScreenPosition.x,
                mouseScreenPosition.y,
                -mainCamera.transform.position.z
            )
        );

        mouseWorldPosition.z = 0;
        return mouseWorldPosition;
    }

    void AddUserPoint(Vector3 point)
    {
        userPoints.Add(point);

        userLine.positionCount = userPoints.Count;
        userLine.SetPosition(userPoints.Count - 1, point);
    }

    void CheckTrace()
    {
        if (userPoints.Count < 2)
        {
            Debug.Log("Not enough points.");
            return;
        }

        float startDistance = Vector3.Distance(userPoints[0], targetStroke[0]);
        float endDistance = Vector3.Distance(userPoints[userPoints.Count - 1], targetStroke[targetStroke.Length - 1]);

        bool startedCorrectly = startDistance <= tolerance;
        bool endedCorrectly = endDistance <= tolerance;

        int goodPointCount = 0;
        float totalDistance = 0f;

        for (int i = 0; i < userPoints.Count; i++)
        {
            float nearestDistance = GetNearestDistanceToTargetStroke(userPoints[i]);
            totalDistance += nearestDistance;

            if (nearestDistance <= tolerance)
            {
                goodPointCount++;
            }
        }

        float score = (float)goodPointCount / userPoints.Count;
        float averageDistance = totalDistance / userPoints.Count;

        float directionScore = GetDirectionScore();
        bool directionCorrect = directionScore >= requiredDirectionScore;

        Debug.Log("Stroke " + (currentStrokeIndex + 1));
        Debug.Log("Trace score: " + (score * 100f).ToString("F1") + "%");
        Debug.Log("Average distance: " + averageDistance.ToString("F2"));
        Debug.Log("Start distance: " + startDistance.ToString("F2"));
        Debug.Log("End distance: " + endDistance.ToString("F2"));
        Debug.Log("Direction score: " + (directionScore * 100f).ToString("F1") + "%");

        if (
            startedCorrectly &&
            endedCorrectly &&
            score >= requiredScore &&
            averageDistance <= maxAverageDistance &&
            directionCorrect
        )
        {
            Debug.Log("Good trace!");
            SetLineColor(userLine, Color.green);
            MoveToNextStroke();
        }
        else
        {
            Debug.Log("Bad trace. Try again.");
            SetLineColor(userLine, Color.red);
            OnCharacterDone?.Invoke(false);
        }
    }

    void MoveToNextStroke()
    {
        SaveCompletedStroke();

        currentStrokeIndex++;

        if (currentStrokeIndex >= characterData.strokes.Count)
        {
            Debug.Log("Character " + characterData.characterName + " complete!");

            IsComplete = true;
            userLine.positionCount = 0;

            if (arrowTransform != null)
            {
                arrowTransform.gameObject.SetActive(false);
            }

            OnCharacterDone?.Invoke(true);
            return;
        }

        Debug.Log("Now trace stroke " + (currentStrokeIndex + 1));

        userLine.positionCount = 0;
        DrawGuideStroke();
    }

    float GetNearestDistanceToTargetStroke(Vector3 userPoint)
    {
        float nearestDistance = Mathf.Infinity;

        for (int i = 0; i < targetStroke.Length - 1; i++)
        {
            Vector3 start = targetStroke[i];
            Vector3 end = targetStroke[i + 1];

            float distance = DistancePointToLineSegment(userPoint, start, end);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        return nearestDistance;
    }

    float DistancePointToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 line = lineEnd - lineStart;
        Vector3 pointToStart = point - lineStart;

        float lineLengthSquared = line.sqrMagnitude;

        if (lineLengthSquared == 0)
        {
            return Vector3.Distance(point, lineStart);
        }

        float t = Vector3.Dot(pointToStart, line) / lineLengthSquared;
        t = Mathf.Clamp01(t);

        Vector3 closestPoint = lineStart + t * line;

        return Vector3.Distance(point, closestPoint);
    }

    float GetDirectionScore()
    {
        if (userPoints.Count < 2)
        {
            return 0f;
        }

        int correctDirectionCount = 0;
        int totalChecks = 0;

        float maxProgressSoFar = GetProgressAlongTargetStroke(userPoints[0]);

        for (int i = 1; i < userPoints.Count; i++)
        {
            float currentProgress = GetProgressAlongTargetStroke(userPoints[i]);

            if (currentProgress + allowedBackwardMovement >= maxProgressSoFar)
            {
                correctDirectionCount++;
            }

            if (currentProgress > maxProgressSoFar)
            {
                maxProgressSoFar = currentProgress;
            }

            totalChecks++;
        }

        return (float)correctDirectionCount / totalChecks;
    }

    float GetProgressAlongTargetStroke(Vector3 userPoint)
    {
        float totalLength = GetTargetStrokeLength();

        if (totalLength <= 0f)
        {
            return 0f;
        }

        float nearestDistance = Mathf.Infinity;
        float bestProgress = 0f;
        float distanceBeforeSegment = 0f;

        for (int i = 0; i < targetStroke.Length - 1; i++)
        {
            Vector3 segmentStart = targetStroke[i];
            Vector3 segmentEnd = targetStroke[i + 1];

            Vector3 segment = segmentEnd - segmentStart;
            float segmentLength = segment.magnitude;

            if (segmentLength == 0f)
            {
                continue;
            }

            Vector3 pointToStart = userPoint - segmentStart;

            float t = Vector3.Dot(pointToStart, segment) / segment.sqrMagnitude;
            t = Mathf.Clamp01(t);

            Vector3 closestPoint = segmentStart + t * segment;

            float distance = Vector3.Distance(userPoint, closestPoint);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;

                float progressDistance = distanceBeforeSegment + t * segmentLength;
                bestProgress = progressDistance / totalLength;
            }

            distanceBeforeSegment += segmentLength;
        }

        return bestProgress;
    }

    float GetTargetStrokeLength()
    {
        float length = 0f;

        for (int i = 0; i < targetStroke.Length - 1; i++)
        {
            length += Vector3.Distance(targetStroke[i], targetStroke[i + 1]);
        }

        return length;
    }

    void AnimateArrow()
    {
        if (arrowTransform == null)
        {
            return;
        }

        arrowProgress += arrowSpeed * Time.deltaTime;

        if (arrowProgress > 1f)
        {
            arrowProgress = 0f;
        }

        Vector3 arrowPosition = GetPointAtProgress(arrowProgress);
        Vector3 arrowDirection = GetDirectionAtProgress(arrowProgress);

        arrowTransform.position = arrowPosition;

        float angle = Mathf.Atan2(arrowDirection.y, arrowDirection.x) * Mathf.Rad2Deg;
        arrowTransform.rotation = Quaternion.Euler(0, 0, angle + arrowRotationOffset);
    }

    Vector3 GetPointAtProgress(float progress)
    {
        float totalLength = GetTargetStrokeLength();
        float targetDistance = progress * totalLength;

        float distanceSoFar = 0f;

        for (int i = 0; i < targetStroke.Length - 1; i++)
        {
            Vector3 start = targetStroke[i];
            Vector3 end = targetStroke[i + 1];

            float segmentLength = Vector3.Distance(start, end);

            if (distanceSoFar + segmentLength >= targetDistance)
            {
                float remainingDistance = targetDistance - distanceSoFar;
                float t = remainingDistance / segmentLength;

                return Vector3.Lerp(start, end, t);
            }

            distanceSoFar += segmentLength;
        }

        return targetStroke[targetStroke.Length - 1];
    }

    Vector3 GetDirectionAtProgress(float progress)
    {
        float smallStep = 0.01f;

        float progressA = Mathf.Clamp01(progress);
        float progressB = Mathf.Clamp01(progress + smallStep);

        Vector3 pointA = GetPointAtProgress(progressA);
        Vector3 pointB = GetPointAtProgress(progressB);

        Vector3 direction = pointB - pointA;

        if (direction.magnitude == 0f)
        {
            return Vector3.right;
        }

        return direction.normalized;
    }

    void SaveCompletedStroke()
    {
        GameObject completedObject = new GameObject("CompletedStroke " + (currentStrokeIndex + 1));

        LineRenderer completedLine = completedObject.AddComponent<LineRenderer>();

        SetupLine(completedLine, 0.12f, 5, Color.green);

        completedLine.positionCount = userPoints.Count;

        for (int i = 0; i < userPoints.Count; i++)
        {
            completedLine.SetPosition(i, userPoints[i]);
        }
    }
}
