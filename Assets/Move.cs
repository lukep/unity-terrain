using UnityEngine;
using System.Collections;

public class Move : MonoBehaviour {
	public Camera cam;
	public float speed = 6.0F;
	public float jumpSpeed = 8.0F;
	public float gravity = 20.0F;
	private float mouseX = 0;
	private Vector3 moveDirection = Vector3.zero;
	void Update() {
		CharacterController controller = GetComponent<CharacterController>();

		if (controller.isGrounded) {
			moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
			moveDirection = transform.TransformDirection(moveDirection);
			moveDirection *= speed;
			if (Input.GetButton("Jump")){
				moveDirection.y = jumpSpeed;
			}

			Vector3 point = cam.ScreenToViewportPoint(Input.mousePosition);
			mouseX = (float)(point.x - 0.5); // Input.GetAxis("Mouse X");
			//Debug.Log (mouseX);

			if( Mathf.Abs(mouseX) > 0.2 ){
				transform.Rotate(Vector3.up * mouseX * speed/30);
				//transform.Rotate(Vector3.up * mouseX);
			}

		}
		moveDirection.y -= gravity * Time.deltaTime;
		controller.Move(moveDirection * Time.deltaTime);
	}
}