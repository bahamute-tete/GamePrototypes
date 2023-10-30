using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkylineGenerator : MonoBehaviour
{
    [SerializeField]
    SkylineObject[] prefabs;

    [SerializeField]
    float distance;

    [SerializeField]
    FloatRange altitude;

    const float border = 10f;

    Vector3 endPosition;
    SkylineObject leftmost, rightmost;

    [SerializeField]
    FloatRange gapLength, sequenceLength;

    [SerializeField]
    SkylineObject gapPrefab;

    float sequenceEndX;

    [SerializeField, Min(0f)]
    float maxYDifference;

    [SerializeField]
    bool singleSequenceStart;

    public void FillView(TrackingCamera view,float extraGapLength =0f,float extraSequenceLength =0f)
    {
        FloatRange visibleX = view.VisibleX(distance).GrowExtents(border);

        while (leftmost != rightmost && leftmost.MaxX < visibleX.min)
        {
            leftmost = leftmost.Recycle();
        }

        while (endPosition.x < visibleX.max)
        {
            if (endPosition.x > sequenceEndX)
            {
                StartNewSequence(gapLength.RandomValue+extraGapLength, 
                                 sequenceLength.RandomValue+extraGapLength);
            }
            rightmost = rightmost.Next = GetInstance();
            endPosition = rightmost.PlaceAfter(endPosition);
        }

    }
    SkylineObject GetInstance()
    {
        SkylineObject instance = prefabs[Random.Range(0, prefabs.Length)].GetInstance();
        instance.transform.SetParent(transform, false);
        return instance;
    }

    public SkylineObject StartNewGame(TrackingCamera view)
    {
        while (leftmost != null)
        {
            leftmost = leftmost.Recycle();
        }

        FloatRange visibleX = view.VisibleX(distance).GrowExtents(border);
        endPosition = new Vector3(visibleX.min, altitude.RandomValue, distance);

        sequenceEndX =singleSequenceStart? visibleX.max:endPosition.x + sequenceLength.RandomValue;

        leftmost = rightmost = GetInstance();
        endPosition = rightmost.PlaceAfter(endPosition);
        FillView(view);
        return leftmost;
    }

    void StartNewSequence(float gap, float sequence)
    {
        if (gapPrefab != null)
        {
            rightmost = rightmost.Next = gapPrefab.GetInstance();
            rightmost.transform.SetParent(transform, false);
            rightmost.FillGap(endPosition, gap);
        }

        endPosition.x += gap;
        //endPosition.y = altitude.RandomValue;
        sequenceEndX =endPosition.x +sequence;

        if (maxYDifference > 0)
        {
            endPosition.y = new FloatRange
                (
                    Mathf.Max(endPosition.y - maxYDifference, altitude.min),
                    Mathf.Min(endPosition.y + maxYDifference, altitude.max)
                ).RandomValue;
        }
        else
        {
            endPosition.y = altitude.RandomValue;
        }
    }
}
