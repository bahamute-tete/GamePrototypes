
using UnityEngine;

public class WaterParticle2D
{
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 acceleration;
    public float density;
    public float pressure;
    public float mass = 1.0f;
    
    public Vector2 surfaceGradient;

   
    public WaterParticle2D(Vector2 pos)
    {
        position = pos;
        velocity = Vector2.zero;
        acceleration = Vector2.zero;
        density = 0f;
        pressure = 0f;

        surfaceGradient = Vector2.zero;
    }

}
