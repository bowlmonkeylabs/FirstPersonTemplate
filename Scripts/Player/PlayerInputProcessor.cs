using System;
using BML.ScriptableObjectCore.Scripts.Events;
using BML.ScriptableObjectCore.Scripts.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerInputProcessor : MonoBehaviour
    {
	    [FoldoutGroup("Realtime Input Values")] public Vector2 move;
	    [FoldoutGroup("Realtime Input Values")] public Vector2 look;
	    [FoldoutGroup("Realtime Input Values")] public Vector2 lookUnscaled;
	    [FoldoutGroup("Realtime Input Values")] public Vector2 lookScaleMouse = new Vector2(0.005f, 0.005f);
	    [FoldoutGroup("Realtime Input Values")] public Vector2 lookScaleGamepad = new Vector2(12.5f, 12.5f);
	    [FoldoutGroup("Realtime Input Values")] public bool jump;
        [FoldoutGroup("Realtime Input Values")] public bool sprint;
        
        [SerializeField, FoldoutGroup("Settings")] public bool analogMovement;
        [SerializeField, FoldoutGroup("Settings")] protected bool cursorLocked = true;
        [SerializeField, FoldoutGroup("Settings")] protected FloatReference _mouseSensitivity;

        [SerializeField, FoldoutGroup("References")] protected PlayerInput playerInput;
        [SerializeField, FoldoutGroup("References")] protected BoolVariable isPaused;
        [SerializeField, FoldoutGroup("References")] protected DynamicGameEvent _switchInputStateEvent;

        [SerializeField, FoldoutGroup("Outputs")] protected Vector2Reference _mouseInput;
        [SerializeField, FoldoutGroup("Outputs")] protected Vector2Reference _moveInput;

        protected virtual bool IsCurrentDeviceMouse
        {
	        get => playerInput.currentControlScheme == "Keyboard&Mouse";
        }

        #region Unity lifecycle

        protected virtual void OnEnable()
        {
	        SwitchInputState("Player");
	        isPaused.Subscribe(UpdateInputState);
	        _switchInputStateEvent.Subscribe(SwitchInputState);
        }
        
        protected virtual void OnDisable()
        {
	        isPaused.Unsubscribe(UpdateInputState);
	        _switchInputStateEvent.Unsubscribe(SwitchInputState);
        }

        #endregion

        #region Input Callbacks

        public virtual void OnMove(InputValue value)
        {
	        Vector2 moveInput = value.Get<Vector2>();
	        MoveInput(moveInput);
        }

        public virtual void OnLook(InputValue value)
        {
	        var inputValue = value.Get<Vector2>();
	        LookInput(inputValue);
        }

        public virtual void OnJump(InputValue value)
        {
	        JumpInput(value.isPressed);
        }

        public virtual void OnSprint(InputValue value)
        {
	        SprintInput(value.isPressed);
        }

        public virtual void OnPause(InputValue value)
        {
	        if (isPaused != null)
	        {
		        isPaused.Value = !isPaused.Value;
		        UpdateInputState();
	        }
        }

        #endregion
        
        protected virtual void MoveInput(Vector2 newMoveDirection)
        {
	        move = newMoveDirection;
	        _moveInput.Value = move;
        }
        
        protected virtual void LookInput(Vector2 newLookDirection)
        {
	        lookUnscaled = newLookDirection;

	        if (IsCurrentDeviceMouse)
		        look = lookUnscaled * lookScaleMouse * _mouseSensitivity.Value;
	        else
		        look = lookUnscaled * lookScaleGamepad * _mouseSensitivity.Value;

	        _mouseInput.Value = look;
        }
        
        protected virtual void JumpInput(bool newJumpState)
        {
	        jump = newJumpState;
        }

        protected virtual void SprintInput(bool newSprintState)
        {
	        sprint = newSprintState;
        }
        
        protected virtual void OnApplicationFocus(bool hasFocus)
        {
	        SetCursorState(cursorLocked);
        }

        protected virtual void UpdateInputState()
        {
	        SetCursorState(!isPaused.Value);
	        playerInput.SwitchCurrentActionMap(isPaused.Value ? "UI" : "Player");
	        Time.timeScale = (isPaused.Value ? 0f : 1f);
        }

        protected virtual void SwitchInputState(object prev, object curr)
        {
	        SwitchInputState((string) curr);
        }

        protected virtual void SwitchInputState(string inputState)
        {
	        if (inputState != "UI" && inputState != "Player")
		        Debug.LogError($"Invalid input state: {inputState}");
	        
	        SetCursorState(inputState == "Player");
	        playerInput.SwitchCurrentActionMap(inputState);
        }

        protected virtual void SetCursorState(bool newState)
        {
	        Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }

    }
}