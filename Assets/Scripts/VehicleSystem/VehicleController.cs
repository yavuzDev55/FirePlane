using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class VehicleController : MonoBehaviour
{
    [Header("Speed")]
    public float maxSpeed = 12f;
    public float acceleration = 8f;
    public float deceleration = 6f;

    [Header("Turning")]
    public float turnAcceleration = 400f;
    public float turnDamping = 0.9f;

    [Header("Input")]
    public InputActionReference throttleAction;
    public InputActionReference turnAction;
    protected float turnInput;
    protected float throttleInput;

    public float currentSpeed;
    protected float turnVelocity;

    protected Rigidbody2D rb;

    [Header("Extinguishing and Intaking")]
    public VehicleWaterTank waterTank;
    public List<VehicleWaterEffectController> extinguishEffectControllers;
    public List<VehicleWaterEffectController> intakeEffectControllers;
    public InputActionReference intakeAction;
    protected bool intakeHeld;

    [Header("Visual Altitude")]
    public Transform vehicleVisual;
    public float normalScale = 1f;
    public float intakeScale = 0.9f;
    public float scaleLerpSpeed = 12f;



    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    protected virtual void OnEnable()
    {
        throttleAction.action.Enable();
        turnAction.action.Enable();
        intakeAction.action.Enable();
    }

    protected virtual void OnDisable()
    {
        throttleAction.action.Disable();
        turnAction.action.Disable();
        intakeAction.action.Disable();
    }

    protected virtual void Update()
    {
        HandleInput();
        HandleIntakeState();
        UpdateVisualAltitude();
        ExtinguishControl();
    }

    protected virtual void FixedUpdate()
    {
        ApplyMovement();
        ApplyRotation();
    }

    protected virtual void HandleInput()
    {
        throttleInput = throttleAction.action.ReadValue<float>();
        turnInput = turnAction.action.ReadValue<float>();
        intakeHeld = intakeAction.action.IsPressed();
    }

    protected virtual void ApplyMovement()
    {
        if (throttleInput > 0)
            currentSpeed += throttleInput * acceleration * Time.fixedDeltaTime;
        else
            currentSpeed -= deceleration * Time.fixedDeltaTime;

        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);
    }

    protected virtual void ApplyRotation()
    {
        turnVelocity += -turnInput * turnAcceleration * Time.fixedDeltaTime;
        turnVelocity *= turnDamping;
    }

    protected virtual void ExtinguishControl()
    {
        if(waterTank == null || extinguishEffectControllers.Count == 0) return;
        
        bool isExtinguishing = waterTank.isExtinguishing;
        CallEffectControllers(extinguishEffectControllers, isExtinguishing);
    }

    void HandleIntakeState()
    {
        if (intakeHeld)
        {
            if(!waterTank.isIntaking && waterTank.IsFullyOverWater())
            {
                waterTank.isIntaking = true;
                CallEffectControllers(intakeEffectControllers, true);
            }
            else if(waterTank.isIntaking && !waterTank.IsFullyOverWater())
            {
                waterTank.isIntaking = false;
                CallEffectControllers(intakeEffectControllers, false);
            }
            else if(waterTank.isIntaking && waterTank.currentWater >= waterTank.maxWater)
            {
                waterTank.isIntaking = false;
                CallEffectControllers(intakeEffectControllers, false);
            }

            turnInput = 0f;
        }
        else
        {
            if (waterTank.isIntaking)
            {
                waterTank.isIntaking = false;
                CallEffectControllers(intakeEffectControllers, false);
            }
        }
    }

    void UpdateVisualAltitude()
    {
        if (vehicleVisual == null) return;

        float targetScale =
            waterTank != null && waterTank.isIntaking
            ? intakeScale
            : normalScale;

        Vector3 target = Vector3.one * targetScale;

        vehicleVisual.localScale = Vector3.Lerp(
            vehicleVisual.localScale,
            target,
            Time.deltaTime * scaleLerpSpeed
        );
    }

    void CallEffectControllers(List<VehicleWaterEffectController> controllers, bool isActive)
    {
        foreach (var controller in controllers)
        {
            controller.SetWater(isActive, currentSpeed);
        }
    }
}
