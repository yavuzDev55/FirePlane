using UnityEngine;

public class VehicleWaterEffectController : MonoBehaviour
{
    public ParticleSystem waterPS;

    [Header("Profile")]
    [Tooltip("Suya çarpma spreyi mi? (yukarı doğru, kısa, dar)")]
    public bool surfaceSpray = false;

    [Header("Tuning")]
    public float maxConeLength = 1.5f;
    public float maxSpread = 5f;
    public float maxSpeed = 2.5f;

    ParticleSystem.ShapeModule shape;
    ParticleSystem.VelocityOverLifetimeModule velocity;
    ParticleSystem.MainModule main;

    void Awake()
    {
        if (waterPS == null)
            waterPS = GetComponent<ParticleSystem>();

        shape = waterPS.shape;
        velocity = waterPS.velocityOverLifetime;
        main = waterPS.main;

        velocity.enabled = true;
    }

    /// <summary>
    /// active: efekt çalışsın mı
    /// planeSpeed: uçağın hızı
    /// </summary>
    public void SetWater(bool active, float planeSpeed)
    {
        if (!active)
        {
            if (waterPS.isPlaying)
                waterPS.Stop();
            return;
        }

        if (!waterPS.isPlaying)
            waterPS.Play();

        // normalize speed
        float t = Mathf.Clamp01(planeSpeed / 10f);

        // yayılma
        float spread = Mathf.Lerp(1f, maxSpread, t);
        float speed = Mathf.Lerp(1f, maxSpeed, t);

        // yön: ana su aşağı, surfaceSpray yukarı
        float yDir = surfaceSpray ? 1f : -1f;

        // velocity (WORLD space olmalı)
        velocity.x = new ParticleSystem.MinMaxCurve(-spread, spread);
        velocity.y = new ParticleSystem.MinMaxCurve(
            yDir * speed * 1.1f,
            yDir * speed * 0.9f
        );
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // koni uzunluğu
        shape.length = Mathf.Lerp(0.3f, maxConeLength, t);

        // ömür
        main.startLifetime = surfaceSpray
            ? Mathf.Lerp(0.2f, 0.4f, t)
            : Mathf.Lerp(0.6f, 1.2f, t);
    }
}
