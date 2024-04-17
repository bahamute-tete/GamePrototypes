using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatRangeSliderAttribute : PropertyAttribute
{ 
    public float Min { get; private set; }
    public float Max { get; private set; }

    public FloatRangeSliderAttribute(float min, float max)
    {
        if (max < min) 
        {
            max = min;
        }
        Min = min;
        Max = max;
    }
}

public abstract class SpawnZone : PresistableObject
{
    [System.Serializable]
    public struct ColorRangeHSV 
    {
        [FloatRangeSlider(0f,1f)]
        public FloatRange hue, saturation, value;

        public Color RandomValue 
        {
            get 
            {
                return Random.ColorHSV(
                    hue.min, hue.max, saturation.min, saturation.max, value.min, value.max, 1f, 1f
                    );
            }
        }
    }
    

    [System.Serializable]
    public struct SpawnConfiguration
    {
        public enum SpawnMovementDirection
        {
            Forward,
            Upward,
            Outward,
            Random
        }
        [SerializeField]
        public SpawnMovementDirection spawnMovementDirection;


        public FloatRange spawnSpeed;
        public FloatRange angularSpeed;
        public FloatRange sclae;
        public ColorRangeHSV color;
    }
    [SerializeField] SpawnConfiguration spawnConfig;
    
   
    public abstract Vector3 SpawnPoint
    {
        get;
          
    }

    public virtual void ConfigureSpawn(Shape shape)
    {

        Transform t = shape.transform;
        t.localPosition =SpawnPoint;
        t.localRotation = Random.rotation;
        t.localScale = spawnConfig.sclae.RandomValue * Vector3.one;
        shape.SetColor(spawnConfig.color.RandomValue);

        shape.AngularVelocity = Random.onUnitSphere * spawnConfig.angularSpeed.RandomValue;

        Vector3 direction;
        switch (spawnConfig.spawnMovementDirection)
        {
            case SpawnConfiguration.SpawnMovementDirection.Upward:
                direction = Vector3.up; break;
            case SpawnConfiguration.SpawnMovementDirection.Outward:
                direction =(t.localPosition -transform.position).normalized; break;
            case SpawnConfiguration.SpawnMovementDirection.Random:
                direction = Random.onUnitSphere;break;
            default:
                direction = transform.forward; break;
        }

        shape.Velocity = direction * spawnConfig.spawnSpeed.RandomValue;
    }


}


