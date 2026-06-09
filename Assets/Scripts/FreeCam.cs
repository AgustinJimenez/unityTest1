using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Free-fly camera. Hold right-click to look, WASD to move, Q/E up/down.
/// Scroll wheel changes move speed.
/// </summary>
[DisallowMultipleComponent]
public class FreeCam : MonoBehaviour
{
    [SerializeField] private float moveSpeed       = 3f;
    [SerializeField] private float lookSensitivity = 0.2f;
    [SerializeField] private float minSpeed        = 0.5f;
    [SerializeField] private float maxSpeed        = 30f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        yaw   = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    private void Update()
    {
        var mouse    = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        // Speed — scroll
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
            moveSpeed = Mathf.Clamp(scroll > 0 ? moveSpeed * 1.3f : moveSpeed / 1.3f, minSpeed, maxSpeed);

        // Look — right mouse held
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw   += delta.x * lookSensitivity;
            pitch -= delta.y * lookSensitivity;
            pitch  = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Move — WASD + Q/E
        Vector3 dir = Vector3.zero;
        if (keyboard.wKey.isPressed) dir += transform.forward;
        if (keyboard.sKey.isPressed) dir -= transform.forward;
        if (keyboard.dKey.isPressed) dir += transform.right;
        if (keyboard.aKey.isPressed) dir -= transform.right;
        if (keyboard.eKey.isPressed) dir += Vector3.up;
        if (keyboard.qKey.isPressed) dir -= Vector3.up;

        if (dir.sqrMagnitude > 0f)
            transform.position += dir.normalized * (moveSpeed * Time.deltaTime);
    }
}
