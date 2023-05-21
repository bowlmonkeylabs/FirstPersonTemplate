using BML.ScriptableObjectCore.Scripts.Variables;
using BML.Scripts.Utils;
using KinematicCharacterController;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class FirstPersonController : MonoBehaviour, ICharacterController
    {
	    [Tooltip("Move speed of the character in m/s")]
        [SerializeField, FoldoutGroup("Player")] protected float _moveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		[SerializeField, FoldoutGroup("Player")] protected float _sprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		[SerializeField, FoldoutGroup("Player")] protected float _rotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		[SerializeField, FoldoutGroup("Player")] protected float _speedChangeRate = 10.0f;
		[Tooltip("Factor for look acceleration setting")]
		[SerializeField, FoldoutGroup("Player")] protected float _lookAccelerationFactor = .0001f;
		[Tooltip("Rate of look acceleration")]
		[SerializeField, FoldoutGroup("Player")] protected FloatReference _lookAcceleration;
		[Tooltip("Curve for analog look input smoothing")]
		[SerializeField, FoldoutGroup("Player")] protected AnimationCurve _analogMovementCurve;
		[SerializeField, FoldoutGroup("Player")] protected BoolReference _isPlayerInputDisabled;

		[Tooltip("The height the player can jump")]
		[SerializeField, FoldoutGroup("Jump")] protected float _jumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		[SerializeField, FoldoutGroup("Jump")] protected float _gravity = -15.0f;
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		[SerializeField, FoldoutGroup("Jump")] protected float _jumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		[SerializeField, FoldoutGroup("Jump")] protected float _fallTimeout = 0.15f;
		
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		[SerializeField, FoldoutGroup("Grounded")] protected bool _grounded = true;
		[Tooltip("Useful for rough ground")]
		[SerializeField, FoldoutGroup("Grounded")] protected float _groundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		[SerializeField, FoldoutGroup("Grounded")] protected float _groundedRadius = 0.5f;

		[SerializeField, FoldoutGroup("Caffeine")] protected float _caffeineMoveSpeedMultiplier;
		[SerializeField, FoldoutGroup("Caffeine")] protected BoolReference _isCaffeinated;

		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		[SerializeField, FoldoutGroup("Cinemachine")] protected GameObject _cinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		[SerializeField, FoldoutGroup("Cinemachine")] protected float _topClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		[SerializeField, FoldoutGroup("Cinemachine")] protected float _bottomClamp = -90.0f;
		
		[SerializeField, FoldoutGroup("Output")] protected Vector3Variable _outputCurrentVelocity;
		[SerializeField, FoldoutGroup("Output")] protected BoolVariable _outputIsGrounded;
		
		[SerializeField, FoldoutGroup("Feedbacks")] protected MMF_Player JumpFeedback;

		// cinemachine
		protected float _cinemachineTargetPitch;
		protected float _cinemachineTargetYaw;

		// player
		protected float _speed;
		protected float _verticalVelocity;
		protected float _terminalVelocity = 53.0f;

		// timeout deltatime
		protected float _jumpTimeoutDelta;
		protected float _fallTimeoutDelta;

	
		protected PlayerInput _playerInput;
		protected KinematicCharacterMotor _motor;
		protected PlayerInputProcessor _input;
		protected GameObject _mainCamera;
		protected float previouRotSpeed = 0f;
		protected bool sprinting = false;
		
		protected virtual bool IsCurrentDeviceMouse
		{
			get => _playerInput.currentControlScheme == "Keyboard&Mouse";
			
		}

		#region Unity Lifecycle

		protected virtual void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
			
			_motor = GetComponent<KinematicCharacterMotor>();
			_motor.CharacterController = this;
			_input = GetComponent<PlayerInputProcessor>();
			_playerInput = GetComponent<PlayerInput>();
		}

		protected virtual void Start()
		{
			// reset our timeouts on start
			_jumpTimeoutDelta = _jumpTimeout;
			_fallTimeoutDelta = _fallTimeout;
		}

		protected virtual void Update()
		{
			if (_isPlayerInputDisabled.Value)
				return;
			
			JumpAndGravity();
			GroundedCheck();
		}

		protected virtual void LateUpdate()
		{
			if (_isPlayerInputDisabled.Value)
				return;
			
			CameraRotation();
		}

		#endregion
		
			#region Kinematic Character Controller

		public void BeforeCharacterUpdate(float deltaTime)
	    {
	        // This is called before the motor does anything
	        JumpAndGravity();
	        GroundedCheck();
	    }

	    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
	    {
		    
	    }

	    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
	    {
		    _outputCurrentVelocity.Value = currentVelocity;
		    
		    // Kill Y velocity if just get grounded so not to slide
		    // Without this, y velocity is converted to horizontal velocity when land
		    if (_motor.GroundingStatus.IsStableOnGround && !_motor.LastGroundingStatus.IsStableOnGround)
		    {
			    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, _motor.CharacterUp);
			    currentVelocity = _motor.GetDirectionTangentToSurface(currentVelocity,
				    _motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;
		    }
		    
		    sprinting = _input.sprint;

		    // set target speed based on move speed, sprint speed and if sprint is pressed
		    float targetSpeed = sprinting ? _sprintSpeed : _moveSpeed;
		    targetSpeed *= _isCaffeinated.Value ? _caffeineMoveSpeedMultiplier : 1f;

		    // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = currentVelocity.xoz().magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * _speedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 inputDirection = (_input.move.y * _mainCamera.transform.forward.xoz().normalized + _input.move.x *
	            _mainCamera.transform.right.xoz().normalized).normalized;
            

		    Vector3 horizontalVelocity = inputDirection * _speed;

		    // move the player
		    currentVelocity = horizontalVelocity +
		                      new Vector3(0.0f, _verticalVelocity, 0.0f);
	    }

	    public void AfterCharacterUpdate(float deltaTime)
	    {
		    // This is called after the motor has finished everything in its update
	    }

	    public bool IsColliderValidForCollisions(Collider coll)
	    {
	        // This is called after when the motor wants to know if the collider can be collided with (or if we just go through it)
	        return true;
	    }

	    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
	        ref HitStabilityReport hitStabilityReport)
	    {
	        // This is called when the motor's ground probing detects a ground hit
	    }

	    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
	        ref HitStabilityReport hitStabilityReport)
	    {
            // This is called when the motor's movement logic detects a hit
	    }

	    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
	        Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
	    {
	        // This is called after every hit detected in the motor, to give you a chance to modify the HitStabilityReport any way you want
	    }

	    public void PostGroundingUpdate(float deltaTime)
	    {
	        // This is called after the motor has finished its ground probing, but before PhysicsMover/Velocity/etc.... handling
	    }

	    public void OnDiscreteCollisionDetected(Collider hitCollider)
	    {
	        // This is called by the motor when it is detecting a collision that did not result from a "movement hit".
	    }

		#endregion

		protected void GroundedCheck()
		{
			_grounded = _motor.GroundingStatus.FoundAnyGround;

			//Only update when changed
			if (_outputIsGrounded.Value != _grounded)
				_outputIsGrounded.Value = _grounded;
		}

		protected virtual void CameraRotation()
		{
			//Don't multiply mouse input by Time.deltaTime
			float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
			
			float rotSpeed = _rotationSpeed;
			float lookAcceleration = _lookAcceleration.Value * _lookAccelerationFactor;
			
			if (!IsCurrentDeviceMouse)
			{
				//For analog movement, dont interpolate linearly
				rotSpeed *= _analogMovementCurve.Evaluate(_input.lookUnscaled.magnitude);
				
				float dummy = 0f;
				float rotSpeedAccelerated;

				//Accelerate to higher values but stop immediately
				if (rotSpeed > previouRotSpeed)
					rotSpeedAccelerated = Mathf.SmoothDamp(previouRotSpeed, rotSpeed, ref dummy, lookAcceleration);
				else
					rotSpeedAccelerated = rotSpeed;
				
				//Debug.Log($"prev: {previouRotSpeed} | target: {String.Format("{0:0.00}", rotSpeed)}");

				rotSpeed = rotSpeedAccelerated;
				previouRotSpeed = rotSpeedAccelerated;
			}
			else
			{
				float dummy = 0f;
				float rotSpeedAccelerated;

				//Accelerate to higher values but stop immediately
				if (rotSpeed > previouRotSpeed)
					rotSpeedAccelerated = Mathf.SmoothDamp(previouRotSpeed, rotSpeed, ref dummy, lookAcceleration);
				else
					rotSpeedAccelerated = rotSpeed;

				rotSpeed = rotSpeedAccelerated;
				previouRotSpeed = rotSpeedAccelerated;
			}

			if (Mathf.Approximately(0f, _input.look.magnitude))
				previouRotSpeed = 0f;
				
			
			_cinemachineTargetYaw += _input.look.x * rotSpeed * deltaTimeMultiplier;
			_cinemachineTargetPitch += _input.look.y * rotSpeed * deltaTimeMultiplier;
			

			// clamp our pitch rotation
			_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, _bottomClamp, _topClamp);

			// Update Cinemachine camera target pitch and yaw
			_cinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
		}

		protected virtual void JumpAndGravity()
		{
			if (_grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = _fallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_motor.ForceUnground();
					JumpFeedback.PlayFeedbacks();
					_verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = _jumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += _gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		protected virtual void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (_grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - _groundedOffset, transform.position.z), _groundedRadius);
		}
    }
}