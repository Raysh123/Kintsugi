using UnityEngine;

public class Outsideview : MonoBehaviour
{
    [SerializeField] private GameObject building;
    [SerializeField] private Transform outsideCameraPosition;
    //[Tooltip("How smoothly the camera moves to the outside position.")]
    //[SerializeField] private float moveSmoothTime = 0.3f;

    private TopDownCameraFollow _cameraFollow;
    private Vector3 _returnPosition;
    private bool _playerInside;

    private void Awake()
    {
        _cameraFollow = Camera.main.GetComponent<TopDownCameraFollow>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        building.SetActive(true);

        _cameraFollow.overrideFollow = true;
        _cameraFollow.enableOrbitRotation = false;
        Camera.main.transform.SetPositionAndRotation(
            outsideCameraPosition.position,
            outsideCameraPosition.rotation
        );
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        building.SetActive(false);

        _cameraFollow.overrideFollow = false;
        _cameraFollow.enableOrbitRotation = true;
    }
}