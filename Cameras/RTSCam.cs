using UnityEngine;
using UnityEngine.InputSystem;

namespace GirlsDevGames.GHOST_Character
{
	public class RTSCam : MonoBehaviour
	{
		private Transform cameraTransform;

		[Header("Horizontal Translation")]
		[SerializeField] private float maxSpeed = 40f;		
		[SerializeField] private float acceleration = 150f;
		[SerializeField] private float damping = 100f;
		[SerializeField] private float minPanFactor = 0.975f;
		[SerializeField] private float maxPanFactor = 1f;
		private float speed;

		[Header("Vertical Translation (Zoom)")]
		[SerializeField] private float zoomStep = 2.5f;
		[SerializeField] private float zoomDamping = 7.5f;
		[SerializeField] private float minZoomDistance = 10f;
		[SerializeField] private float maxZoomDistance = 50f;
		private float targetZoomDistance = 30f;

		[Header("MouseDrag")]
		[SerializeField] private float dragSensitivity = 5f;
		[SerializeField] private float dragFriction = 150f;

		[Header("Rotation")]
		[SerializeField] private float rotationSpeed = 0.5f;
		[SerializeField] private float rotationSmooth = 10f;

		[Header("Edge Movement")]
		[SerializeField] private bool enableEdgeScrolling = true;
		[SerializeField] [Range(0f, 0.1f)] private float edgeTolerance = 0.05f;

		[Header("Movement Bounds")]
		[SerializeField] private Vector2 movementBoundsX = new Vector2(-50, 50);
		[SerializeField] private Vector2 movementBoundsZ = new Vector2(-50, 50);

		// Cache
		private Vector3 targetPosition;
		private Vector3 lastPosition;
		private Vector3 velocity;
		private Vector3 horizontalVelocity;
		private Vector3 dragStartPos;
		private float zoomPanFactor;
		
		// Mouse input
		private bool mouse_right;
		private bool mouse_left;
		private bool mouse_middle;
		private bool mouse_right_pressed_this_frame;
		private Vector2 m_pos;
		
		// Input
		private PlayerInput input;
		private InputAction movementInputActn;
		private InputAction rotateInputActn;
		private Vector2 moveVector;
		private Vector2 rotateInput;
		
		// Other
		private System.Action<InputAction.CallbackContext> rotateHandler_a; // Mouse or axis based rotate handler
	
		private void OnEnable()
		{			
			// ---------------------------------------- //
			// Init Input
			input = new PlayerInput();
			movementInputActn = input.CameraRTS.Movement;
			rotateInputActn = input.CameraRTS.BtnRotateActn;

			rotateHandler_a = ctx => 
			{ 
				Vector2 vec = ctx.ReadValue<Vector2>();
				if (mouse_left) { vec.y = 0; Rotate(-vec); }
				else if (mouse_middle) { vec.x = 0; Rotate(vec); }
			};
			
			input.CameraRTS.AxisRotateActn.performed += rotateHandler_a;
			
			input.CameraRTS.Zoom.performed += Zoom;
			input.CameraRTS.Enable();
			// ---------------------------------------- //
		}
		
		private void OnDisable()
		{
			// Disable Input
			input.CameraRTS.AxisRotateActn.performed -= rotateHandler_a;
			input.CameraRTS.Zoom.performed           -= Zoom;
			input.CameraRTS.Disable();
		}
		
		private void Start()
		{
			cameraTransform = GetComponentInChildren<Camera>().transform;
			
			targetZoomDistance = cameraTransform.localPosition.magnitude;
			lastPosition = transform.position;
			cameraTransform.LookAt(transform);
			
			// ---------------------------------------- //
			// Mirror any changes from update method here
			Move();
			MouseDrag();
			if (enableEdgeScrolling) EdgeScroll();
			if (rotateInput.sqrMagnitude > 0) Rotate(rotateInput);
			UpdatePosition();
			LookAtTarget();
			UpdateVelocity();
			// ---------------------------------------- //
			
			mouse_right = false;
			mouse_left = false;
			mouse_middle = false;
			mouse_right_pressed_this_frame = false;
			m_pos = Vector2.zero;
			
			moveVector = Vector2.zero;
			rotateInput = Vector2.zero;
		}

		private void Update()
		{
			// Lock mouse
			if (mouse_right_pressed_this_frame) {
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			
			// Update			
			mouse_right = Mouse.current.rightButton.isPressed;
			mouse_left = Mouse.current.leftButton.isPressed;
			mouse_middle = Mouse.current.middleButton.isPressed;
			mouse_right_pressed_this_frame = Mouse.current.rightButton.wasPressedThisFrame;
			
			m_pos = Mouse.current.position.ReadValue();
			moveVector = -(movementInputActn.ReadValue<Vector2>());
			rotateInput = rotateInputActn.ReadValue<Vector2>();
			
			// ---------------------------------------- //
			Move();
			
			if (rotateInput.sqrMagnitude > 0)
				Rotate(rotateInput);
				
			if (enableEdgeScrolling)
				EdgeScroll();
			
			MouseDrag();
			UpdatePosition();
			LookAtTarget();
			UpdateVelocity();
			// ---------------------------------------- //
			
			// Unlock mouse
			if (!mouse_right) {
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
		}
		
		private void LateUpdate()
		{
			Quaternion targetRotation = Quaternion.Euler(rotationX, rotationY, 0f);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmooth);
		}

		private void Move()
		{
			if (moveVector.sqrMagnitude > 0.0f) {
				Vector3 direction = moveVector.x * GetCameraRight() + moveVector.y * GetCameraForward();
				targetPosition = direction.normalized;
			}	
		}

		private Vector3 dragVelocity = Vector3.zero;
		
		private void MouseDrag()
		{			
			if (mouse_right)
			{
				Vector2 mouseDelta = Mouse.current.delta.ReadValue();;

				if (mouse_right_pressed_this_frame)
					return;

				if (Mathf.Abs(mouseDelta.x) > Mathf.Abs(mouseDelta.y))
					mouseDelta.y = 0f;
				else
					mouseDelta.x = 0f;

				Vector3 right = GetCameraRight();
				Vector3 forward = GetCameraForward();
				
				Vector3 dragDelta = (right * mouseDelta.x + forward * mouseDelta.y) * dragSensitivity;

				dragVelocity += dragDelta;
			} 
			else 
			{
				dragVelocity = Vector3.zero;
			}

			// Apply inertia to target position
			targetPosition += dragVelocity * Time.deltaTime;

			// Decelerate drag velocity
			dragVelocity = Vector3.Lerp(dragVelocity, Vector3.zero, dragFriction * Time.deltaTime);
		}

		private void EdgeScroll()
		{
			// Don't move if any other camera movement related button
			// is pressed
			if (mouse_left || mouse_right || mouse_middle)
				return;
				
			if (moveVector.sqrMagnitude > 0f)
				return;
			
			Vector3 moveDirection = Vector3.zero;

			if (m_pos.x < edgeTolerance * Screen.width)
				moveDirection += -GetCameraRight();
			else if (m_pos.x > (1f - edgeTolerance) * Screen.width)
				moveDirection += GetCameraRight();

			if (m_pos.y < edgeTolerance * Screen.height)
				moveDirection += -GetCameraForward();
			else if (m_pos.y > (1f - edgeTolerance) * Screen.height)
				moveDirection += GetCameraForward();

			moveDirection *= -1f;
			if (moveDirection.sqrMagnitude > 1f)
				moveDirection.Normalize();
			
			targetPosition = moveDirection;
		}

		private void Zoom(InputAction.CallbackContext context)
		{
			float scrollInput = context.ReadValue<Vector2>().y;
			float scrollDirection = Mathf.Sign(scrollInput);

			if (scrollDirection != 0f)
			{
				targetZoomDistance += scrollDirection * zoomStep;
				targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance);
			}
		}

		private float rotationX = 45f; 
		private float rotationY = 0f;
		private float minRotationX = 20f; // Min camera pitch (prevents looking straight up)
		private float maxRotationX = 80f; // Max camera pitch (prevents looking straight down)
		
		private void Rotate(Vector2 inputValue, bool axis_input=false)
		{
			rotationY -= inputValue.x * rotationSpeed;
			rotationX += inputValue.y * rotationSpeed;
			rotationX = Mathf.Clamp(rotationX, minRotationX, maxRotationX);
		}
		
		private void UpdatePosition()
		{
			if (targetPosition.sqrMagnitude > 0f)
			{
				Vector3 desiredDirection = targetPosition.normalized;
				velocity += desiredDirection * acceleration * Time.deltaTime;

				if (velocity.magnitude > maxSpeed)
					velocity = velocity.normalized * maxSpeed;
			}
			else
			{
				velocity = Vector3.MoveTowards(velocity, Vector3.zero, damping * Time.deltaTime);
			}

			// Apply final zoom factor
			float t = Mathf.InverseLerp(minZoomDistance, maxZoomDistance, targetZoomDistance);
			zoomPanFactor = Mathf.Lerp(minPanFactor, maxPanFactor, t);
			velocity *= zoomPanFactor;

			transform.position += velocity * Time.deltaTime;
			transform.position = new Vector3(
				Mathf.Clamp(transform.position.x, movementBoundsX.x, movementBoundsX.y),
				transform.position.y,
				Mathf.Clamp(transform.position.z, movementBoundsZ.x, movementBoundsZ.y)
			);

			targetPosition = Vector3.zero;
		}

		private void LookAtTarget()
		{
			// Always position camera at desired distance back from pivot
			Vector3 dir = cameraTransform.localPosition.normalized;
			cameraTransform.localPosition = Vector3.Lerp(
				cameraTransform.localPosition,
				dir * targetZoomDistance,
				Time.deltaTime * zoomDamping);

			cameraTransform.LookAt(transform.position);
		}
		
		private void UpdateVelocity()
		{
			horizontalVelocity = (transform.position - lastPosition) / Time.deltaTime;
			horizontalVelocity.y = 0f;
			lastPosition = transform.position;
		}

		private Vector3 GetCameraForward()
		{
			Vector3 forward = transform.forward;
			forward.y = 0f;
			return forward.normalized;
		}

		private Vector3 GetCameraRight()
		{
			Vector3 right = transform.right;
			right.y = 0f;
			return right.normalized;
		}
	}
}
