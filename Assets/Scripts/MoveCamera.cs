using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 5f; // 摄像机移动速度
    public float sensitivity = 2f; // 鼠标灵敏度
    public float liftSpeed = 5f; // 上升速度

    private float rotationX = 0f;
    private bool isJumping = false;

    void Update()
    {
        // 获取键盘输入
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // 计算摄像机移动方向
        Vector3 moveDirection = new Vector3(horizontalInput, 0, verticalInput).normalized;

        // 将移动方向转换为世界坐标
        moveDirection = transform.TransformDirection(moveDirection);

        // 移动摄像机
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        // 获取鼠标输入
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        // 根据鼠标移动调整摄像机的旋转角度
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); // 限制摄像机旋转角度在 -90 到 90 度之间
        transform.localRotation = Quaternion.Euler(rotationX, transform.localRotation.eulerAngles.y + mouseX, 0f);

        // 检测空格键按下和松开
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 启动上升
            isJumping = true;
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            // 停止上升
            isJumping = false;
        }

        // 如果正在上升，则向上移动摄像机
        if (isJumping)
        {
            transform.position += Vector3.up * liftSpeed * Time.deltaTime;
        }
    }
}
