using UnityEngine;

public class HelicopterController : VehicleController
{
    [Header("Helicopter")]
    public float hoverDrag = 4f;

    protected override void Awake()
    {
        base.Awake();
        currentSpeed = 0f;
    }

    protected override void ApplyMovement()
    {
        // 🚀 Önce base hızlanma (throttle → speed)
        base.ApplyMovement();

        // 🚁 Helikopter geri de gidebilsin
        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed, maxSpeed);

        // 🌬️ Gaz yokken havada asılı kalma
        if (Mathf.Abs(throttleInput) < 0.01f)
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                0f,
                hoverDrag * Time.fixedDeltaTime
            );
        }

        // ➡️ Hareket
        Vector2 forward = transform.up;
        rb.MovePosition(rb.position + forward * currentSpeed * Time.fixedDeltaTime);
    }

    protected override void ApplyRotation()
    {
        // 🌀 Yerinde dönüş
        turnVelocity += -turnInput * turnAcceleration * Time.fixedDeltaTime;
        turnVelocity *= turnDamping;

        rb.MoveRotation(rb.rotation + turnVelocity * Time.fixedDeltaTime * 100);
    }
}
