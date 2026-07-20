using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using TMPro;
public class Script1 : MonoBehaviour
{
    /// <summary>Raised once whenever one requested character has been completed correctly.</summary>
    public static event Action<bool> OnCharacterDone;

    [Header("Character library")]
    [Tooltip("Add every CharacterData asset that dialogue may request.")]
    [SerializeField] private List<CharacterData> availableCharacters = new List<CharacterData>();

    [Header("Fallback for testing this scene directly")]
    [SerializeField] private CharacterData characterData;

    [Header("Line appearance")]
    [Tooltip("Color used while the player draws a stroke. Correct/incorrect feedback remains green/red.")]
    [SerializeField] private Color userStrokeColor = Color.black;

    private readonly List<CharacterData> activeSequence = new List<CharacterData>();
    private readonly List<GameObject> completedStrokeObjects = new List<GameObject>();
    private int currentCharacterIndex = 0;

    private float hardTolerance = 0.85f;
    private float minLengthRatio = 0.55f;
    private float maxLengthRatio = 1.8f;

    private bool wentTooFarFromStroke = false;

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
    private bool characterComplete = false;

    private CharacterData CurrentCharacterData
    {
        get
        {
            if (activeSequence.Count > 0 &&
                currentCharacterIndex >= 0 &&
                currentCharacterIndex < activeSequence.Count)
            {
                return activeSequence[currentCharacterIndex];
            }

            return characterData;
        }
    }

    private Vector3[] targetStroke
    {
        get
        {
            return CurrentCharacterData.strokes[currentStrokeIndex].points.ToArray();
        }
    }

    void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("[Script1] No Main Camera found.");
            enabled = false;
            return;
        }

        if (guideLine == null || userLine == null)
        {
            Debug.LogError("[Script1] Guide Line and User Line must be assigned.");
            enabled = false;
            return;
        }

        SetupLine(guideLine, 0.12f, 0, Color.gray);
        SetupLine(userLine, 0.10f, 10, userStrokeColor);

        if (!BuildActiveSequence())
        {
            enabled = false;
            return;
        }

        currentCharacterIndex = 0;
        LoadCurrentCharacter();
    }

    private bool BuildActiveSequence()
    {
        activeSequence.Clear();

        int requestedCount = 1;
        IReadOnlyList<string> requestedCharacters = null;
        if (GameProgress.Instance != null)
        {
            requestedCount = Mathf.Max(1, GameProgress.Instance.RequiredTraceCount);
            requestedCharacters = GameProgress.Instance.RequiredTraceCharacters;
        }

        if (availableCharacters != null && availableCharacters.Count > 0)
        {
            if (requestedCharacters != null && requestedCharacters.Count > 0)
            {
                for (int requestedIndex = 0; requestedIndex < requestedCharacters.Count; requestedIndex++)
                {
                    string requestedName = requestedCharacters[requestedIndex];
                    CharacterData match = availableCharacters.Find(
                        candidate => candidate != null && candidate.characterName == requestedName);
                    if (match == null)
                    {
                        Debug.LogError(
                            "[Script1] Dialogue requested CharacterData '" + requestedName +
                            "', but it is not assigned to Available Characters."
                        );
                        return false;
                    }
                    activeSequence.Add(match);
                }
                return true;
            }

            if (availableCharacters.Count < requestedCount)
            {
                Debug.LogError(
                    "[Script1] This task requires " + requestedCount +
                    " characters, but Available Characters contains only " +
                    availableCharacters.Count + "."
                );
                return false;
            }

            for (int i = 0; i < requestedCount; i++)
            {
                if (availableCharacters[i] == null)
                {
                    Debug.LogError(
                        "[Script1] Available Characters element " + i + " is empty."
                    );
                    return false;
                }

                activeSequence.Add(availableCharacters[i]);
            }
        }
        else if (requestedCount == 1 && characterData != null)
        {
            activeSequence.Add(characterData);
        }

        if (activeSequence.Count == 0)
        {
            Debug.LogError(
                "[Script1] Assign the required CharacterData assets to Available Characters."
            );
            return false;
        }

        return true;
    }

    private void LoadCurrentCharacter()
    {
        CharacterData data = CurrentCharacterData;

        if (data == null || data.strokes == null || data.strokes.Count == 0)
        {
            Debug.LogError("[Script1] Current character has no stroke data.");
            enabled = false;
            return;
        }

        for (int i = 0; i < data.strokes.Count; i++)
        {
            if (data.strokes[i].points == null || data.strokes[i].points.Count < 2)
            {
                Debug.LogError(
                    "[Script1] Stroke " + (i + 1) + " of " + data.characterName +
                    " does not have enough points."
                );
                enabled = false;
                return;
            }
        }

        currentStrokeIndex = 0;
        characterComplete = false;
        wentTooFarFromStroke = false;
        arrowProgress = 0f;
        userPoints.Clear();
        guideLine.positionCount = 0;
        userLine.positionCount = 0;
        SetLineColor(userLine, userStrokeColor);

        if (arrowTransform != null)
        {
            arrowTransform.gameObject.SetActive(true);
        }

        DrawGuideStroke();

        Debug.Log(
            "[Script1] Now tracing " + data.characterName +
            " (" + (currentCharacterIndex + 1) + "/" + activeSequence.Count + "), stroke 1."
        );
    }

    void Update()
    {
        if (characterComplete)
        {
            return;
        }

        // Testing aid: hold Delete and press Backspace to skip the trace entirely, returning to the
        // dialogue as if every character was traced correctly. Remove before shipping.
        if (Keyboard.current != null &&
            Keyboard.current.deleteKey.isPressed &&
            Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            if (GameProgress.Instance != null)
            {
                Debug.Log("[Script1] Skip hotkey (Delete+Backspace) pressed — treating trace as passed.");
                GameProgress.Instance.ForcePassTrace();
            }
            return;
        }

        AnimateArrow();

        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            userPoints.Clear();
            userLine.positionCount = 0;
            SetLineColor(userLine, userStrokeColor);

            wentTooFarFromStroke = false;

            Vector3 startPoint = GetMouseWorldPosition();
            AddUserPoint(startPoint);
        }

        if (Mouse.current.leftButton.isPressed)
        {
            Vector3 currentPoint = GetMouseWorldPosition();

            float distanceToStroke = GetNearestDistanceToTargetStroke(currentPoint);

            if (distanceToStroke > hardTolerance)
            {
                wentTooFarFromStroke = true;
            }

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
        float targetLength = GetTargetStrokeLength();
        float userLength = GetUserPathLength();

        float lengthRatio = userLength / targetLength;

        bool lengthCorrect = lengthRatio >= minLengthRatio && lengthRatio <= maxLengthRatio;
        bool didNotScribble = !wentTooFarFromStroke;

        Debug.Log("User length: " + userLength.ToString("F2"));
        Debug.Log("Target length: " + targetLength.ToString("F2"));
        Debug.Log("Length ratio: " + lengthRatio.ToString("F2"));
        Debug.Log("Went too far from stroke: " + wentTooFarFromStroke);
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
        directionCorrect &&
        lengthCorrect &&
        didNotScribble
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
        }
    }

    void MoveToNextStroke()
    {
        SaveCompletedStroke();
        currentStrokeIndex++;

        if (currentStrokeIndex >= CurrentCharacterData.strokes.Count)
        {
            Debug.Log(
                "[Script1] Character " + CurrentCharacterData.characterName + " complete!"
            );
            MoveToNextRequestedCharacter();
            return;
        }

        userPoints.Clear();
        userLine.positionCount = 0;
        DrawGuideStroke();

        Debug.Log(
            "[Script1] Now trace stroke " + (currentStrokeIndex + 1) +
            " of " + CurrentCharacterData.characterName + "."
        );
    }

    private void MoveToNextRequestedCharacter()
    {
        currentCharacterIndex++;

        if (currentCharacterIndex < activeSequence.Count)
        {
            ClearCompletedStrokes();
            LoadCurrentCharacter();
            OnCharacterDone?.Invoke(true);
            return;
        }

        FinishTracingTask();
    }

    private void FinishTracingTask()
    {
        characterComplete = true;
        guideLine.positionCount = 0;
        userLine.positionCount = 0;

        if (arrowTransform != null)
        {
            arrowTransform.gameObject.SetActive(false);
        }

        Debug.Log("[Script1] All requested characters completed.");
        OnCharacterDone?.Invoke(true);
    }

    private void ClearCompletedStrokes()
    {
        for (int i = 0; i < completedStrokeObjects.Count; i++)
        {
            if (completedStrokeObjects[i] != null)
            {
                Destroy(completedStrokeObjects[i]);
            }
        }

        completedStrokeObjects.Clear();
        userPoints.Clear();
        guideLine.positionCount = 0;
        userLine.positionCount = 0;
    }

    float GetUserPathLength()
    {
        float length = 0f;

        for (int i = 0; i < userPoints.Count - 1; i++)
        {
            length += Vector3.Distance(userPoints[i], userPoints[i + 1]);
        }

        return length;
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
        GameObject completedObject = new GameObject(
            CurrentCharacterData.characterName +
            "_CompletedStroke_" +
            (currentStrokeIndex + 1)
        );

        completedObject.transform.SetParent(transform, true);
        completedStrokeObjects.Add(completedObject);

        LineRenderer completedLine = completedObject.AddComponent<LineRenderer>();
        SetupLine(completedLine, 0.12f, 5, Color.green);
        completedLine.positionCount = userPoints.Count;

        for (int i = 0; i < userPoints.Count; i++)
        {
            completedLine.SetPosition(i, userPoints[i]);
        }
    }



}
