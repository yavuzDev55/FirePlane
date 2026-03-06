using System.Diagnostics;
using UnityEngine;

public class PlaneController : VehicleController
{
    [Header("Plane")]
    public float minSpeed = 4f;
    public AnimationCurve turnBySpeed;
    // x: speed factor (0–1), y: turn effectiveness

    private float speedFactor;
    private float turnEffectiveness;

    protected override void Awake()
    {
        base.Awake();
        currentSpeed = minSpeed;
    }

    protected override void ApplyMovement()
    {
        // 🚀 Önce base hızlanma çalışsın
        base.ApplyMovement();

        // ✈️ Uçak asla minSpeed altına düşmesin
        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);

        // 🌀 Hıza bağlı manevra
        speedFactor = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);
        turnEffectiveness = turnBySpeed.Evaluate(speedFactor);

        // ➡️ Sürekli ileri hareket
        Vector2 forward = transform.up;
        rb.MovePosition(rb.position + forward * currentSpeed * Time.fixedDeltaTime);
    }

    protected override void ApplyRotation()
    {
        turnVelocity += -turnInput * turnAcceleration * turnEffectiveness * Time.fixedDeltaTime;
        turnVelocity *= turnDamping;

        rb.MoveRotation(rb.rotation + turnVelocity * Time.fixedDeltaTime * 100);
    }
}
