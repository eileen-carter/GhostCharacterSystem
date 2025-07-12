using UnityEngine;
using UnityEngine.InputSystem;

namespace GirlsDevGames.GHOST_Character
{
	public class ThirdPersonCam : MonoBehaviour
	{
		[System.Serializable]
		public struct PositionSettings
		{        
			public Vector3 targetPosOffset;
			public float distanceToTarget;
			public float zoomSmooth;
			public float minZoom;
			public float maxZoom;

			public void set_defaults() 
			{
				targetPosOffset = new Vector3(0f, 1.5f, 0f);
				distanceToTarget = 5f;
				zoomSmooth = 50f;
				minZoom = 1f;
				maxZoom = 6f;
			}
		}

		[System.Serializable]
		public class OrbitSettings
		{
			public float xRotation;
			public float yRotation;
			public float minYRotation;
			public float maxYRotation;
			public float orbitSmooth;
			
			public void set_defaults() 
			{
			}
		}

		private PlayerInput input;
		private Vector2 mouseDelta;
		private float zoomInput;
		
		public Transform target;
		public Camera cam;
		
		public PositionSettings posSettings = new PositionSettings();
		public OrbitSettings orbitSettings = new OrbitSettings();
		private CamCollision collisionHandler;
		
		[Header("Settings")]
		public float mouseSpeedMultiplier = 6f;
		public bool doubleCollisionCheck = false;
		public bool lockOrbit = true;
		public bool lerpFollowTarget = false;
		
		[Header("Debug")]
		public bool debugMode = false;

		private Vector3 targetPos = Vector3.zero;
		private Vector3 destination = Vector3.zero;
		private Vector3 adjustedDestination = Vector3.zero;

		void Start()
		{
			cam = Camera.main;
			collisionHandler = GetComponent<CamCollision>();
			
			posSettings.set_defaults();
			orbitSettings.set_defaults();
			
			MoveToTarget();
		}
		
		private void OnEnable()
		{
			input = new PlayerInput();
			input.Enable();
		}

		private void OnDisable()
		{
			input.Disable();
		}
		
		void Update() {
			if (collisionHandler)
				CheckCollision();
		}
		
		void LateUpdate()
		{
			// Get input
			mouseDelta = input.Camera.Movement.ReadValue<Vector2>();
			if (mouseDelta.magnitude > mouseSpeedMultiplier)
				mouseDelta = mouseDelta.normalized * mouseSpeedMultiplier;
			
			zoomInput = input.Camera.Zoom.ReadValue<Vector2>().y;
			if (Mathf.Abs(zoomInput) > 0)

			if (zoomInput != 0) zoomInput /= Mathf.Abs(zoomInput);

			// Update camera
			if (collisionHandler && doubleCollisionCheck)
				CheckCollision();
			
			Zoom();
			Orbit();
			MoveToTarget();
			LookAtTarget();
		}
		
		void Orbit()
		{
			if (!lockOrbit) {
				orbitSettings.yRotation += mouseDelta.x;
				orbitSettings.yRotation = Mathf.Clamp(
					orbitSettings.yRotation,
					orbitSettings.minYRotation,
					orbitSettings.maxYRotation
				);
			}
		}

		void Zoom()
		{        
			if (zoomInput == 0)
				return;
		
			posSettings.distanceToTarget += zoomInput * posSettings.zoomSmooth * Time.deltaTime;
			posSettings.distanceToTarget = Mathf.Clamp(
				posSettings.distanceToTarget,
				posSettings.minZoom,
				posSettings.maxZoom);
			
			// check if we are going to collide at new target position
			
		}
		
		void CheckCollision()
		{
			float collisionDistance =
				collisionHandler.SphericalCollisionDetection(target.position, posSettings.distanceToTarget);
			
			if (collisionDistance != Mathf.Infinity && collisionDistance < posSettings.distanceToTarget)
			{
				posSettings.distanceToTarget = Mathf.Lerp(
					posSettings.distanceToTarget,
					collisionDistance,
					50f * mouseDelta.magnitude * Time.deltaTime);
				
				targetPos = target.position + posSettings.targetPosOffset;
				destination =  Quaternion.Euler(orbitSettings.xRotation, orbitSettings.yRotation, 0) * 
					-Vector3.forward * (posSettings.distanceToTarget);

				destination += targetPos;
				transform.position = destination;
			}
		}

		void MoveToTarget()
		{        
			targetPos = target.position + posSettings.targetPosOffset;

			destination =  Quaternion.Euler(
				orbitSettings.xRotation,
				orbitSettings.yRotation,
				0) * -Vector3.forward * (posSettings.distanceToTarget);
			destination += targetPos;
			
			if (lerpFollowTarget)
				transform.position = Vector3.Slerp(transform.position, destination, 10f * Time.deltaTime);
			else
				transform.position = destination;
		}

		void LookAtTarget()
		{
			Quaternion lookRotation = 
				Quaternion.LookRotation((target.position + posSettings.targetPosOffset) - transform.position);
			transform.rotation = lookRotation;
		}
	}
}
