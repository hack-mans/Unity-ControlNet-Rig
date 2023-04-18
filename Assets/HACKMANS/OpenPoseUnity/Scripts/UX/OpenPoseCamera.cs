using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenPoseCamera : MonoBehaviour
{
    /// <summary>
    /// Rotation speed when using the mouse.
    /// </summary>
    public float m_LookSpeedMouse = 4.0f;
    /// <summary>
    /// Movement speed.
    /// </summary>
    public float m_MoveSpeed = 10.0f;
    /// <summary>
    /// Value added to the speed when incrementing.
    /// </summary>
    public float m_MoveSpeedIncrement = 2.5f;
    /// <summary>
    /// Scale factor of the turbo mode.
    /// </summary>
    public float m_Turbo = 10.0f;
    
    private static string kMouseX = "Mouse X";
    private static string kMouseY = "Mouse Y";
    private static string kVertical = "Vertical";
    private static string kHorizontal = "Horizontal";
    private static string kYAxis = "YAxis";

    float inputRotateAxisX, inputRotateAxisY;
    float inputChangeSpeed;
    float inputVertical, inputHorizontal, inputYAxis;
    bool leftShiftBoost, leftShift;
    
    private void UpdateInputs()
    {
        inputRotateAxisX = 0.0f;
        inputRotateAxisY = 0.0f;
        leftShiftBoost = false;

        if (Input.GetMouseButton(1))
        {
            leftShiftBoost = true;
            inputRotateAxisX = Input.GetAxis(kMouseX) * m_LookSpeedMouse;
            inputRotateAxisY = Input.GetAxis(kMouseY) * m_LookSpeedMouse;
            
            leftShift = Input.GetKey(KeyCode.LeftShift);

            inputVertical = Input.GetAxis(kVertical);
            inputHorizontal = Input.GetAxis(kHorizontal);
            inputYAxis = Input.GetAxis(kYAxis);
        }
    }

    private void Update()
    {
        UpdateInputs();

        if (inputChangeSpeed != 0.0f)
        {
            m_MoveSpeed += inputChangeSpeed * m_MoveSpeedIncrement;
            if (m_MoveSpeed < m_MoveSpeedIncrement) m_MoveSpeed = m_MoveSpeedIncrement;
        }

        bool moved = inputRotateAxisX != 0.0f || inputRotateAxisY != 0.0f || inputVertical != 0.0f || inputHorizontal != 0.0f || inputYAxis != 0.0f;
        if (moved)
        {
            float rotationX = transform.localEulerAngles.x;
            float newRotationY = transform.localEulerAngles.y + inputRotateAxisX;

            // Weird clamping code due to weird Euler angle mapping...
            float newRotationX = (rotationX - inputRotateAxisY);
            if (rotationX <= 90.0f && newRotationX >= 0.0f)
                newRotationX = Mathf.Clamp(newRotationX, 0.0f, 90.0f);
            if (rotationX >= 270.0f)
                newRotationX = Mathf.Clamp(newRotationX, 270.0f, 360.0f);

            transform.localRotation = Quaternion.Euler(newRotationX, newRotationY, transform.localEulerAngles.z);

            float moveSpeed = Time.deltaTime * m_MoveSpeed;
            if (leftShiftBoost && leftShift)
                moveSpeed *= m_Turbo;
            transform.position += transform.forward * moveSpeed * inputVertical;
            transform.position += transform.right * moveSpeed * inputHorizontal;
            transform.position += Vector3.up * moveSpeed * inputYAxis;
        }
    }
}
