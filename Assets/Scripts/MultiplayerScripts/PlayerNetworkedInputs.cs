using Fusion;

public class PlayerNetworkedInputs : NetworkBehaviour
{
    private const float MoveSpeed = 5f;

    private NetworkCharacterController _cc;
    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            data.direction.Normalize();
            _cc.Move(MoveSpeed * data.direction * Runner.DeltaTime);
        }
    }
}
