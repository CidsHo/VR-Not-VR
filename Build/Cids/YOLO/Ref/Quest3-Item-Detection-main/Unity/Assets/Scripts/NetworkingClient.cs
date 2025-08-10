using System;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class NetworkingClient : MonoBehaviour
{
    // 服务器的IP地址和端口号
    public string host = "192.168.68.102";
    public int port = 8080;

    // TCP客户端和网络流
    private TcpClient tcpClient;
    private NetworkStream networkStream;

    // 是否正在尝试连接的标志
    private bool attemptingConnection = false;

    // 用于读取数据的协程
    private Coroutine readDataCoroutine;

    // 委托和事件，用于处理消息接收和断开连接
    public delegate void MessageReceivedHandler(string message);
    public event MessageReceivedHandler OnMessageReceived;
    public delegate void DisconnectedHandler();
    public event DisconnectedHandler OnDisconnected;

    // UI元素用于显示连接状态
    [SerializeField] private Image connectionStatusImage;
    [SerializeField] private Color connectedColor = Color.green;  // 连接时的颜色
    [SerializeField] private Color disconnectedColor = Color.red; // 断开时的颜色

    // 在启动时初始化连接状态为断开状态
    void Start()
    {
        UpdateConnectionStatus(false);
    }

    // 异步连接到服务器的方法
    public async void Connect()
    {
        // 如果正在尝试连接或已经连接，则不进行新的连接
        if (attemptingConnection || (tcpClient != null && tcpClient.Connected)) return;
        attemptingConnection = true;

        try
        {
            tcpClient = new TcpClient();  // 创建新的TCP客户端
            await tcpClient.ConnectAsync(host, port);  // 异步连接到服务器
            networkStream = tcpClient.GetStream();  // 获取网络流
            UpdateConnectionStatus(true);  // 更新连接状态为已连接
            readDataCoroutine ??= StartCoroutine(ReadDataAsync());  // 开始读取数据的协程
        }
        catch (Exception e)
        {
            // 连接失败时打印错误信息并更新连接状态为断开
            Debug.LogError("Failed to connect to the TCP server: " + e.Message);
            UpdateConnectionStatus(false);
        }
        finally
        {
            attemptingConnection = false;  // 结束连接尝试
        }
    }

    // 断开与服务器的连接
    public void Disconnect()
    {
        if (tcpClient != null)
        {
            if (tcpClient.Connected)
            {
                networkStream.Close();  // 关闭网络流
                tcpClient.Close();  // 关闭TCP客户端
                UpdateConnectionStatus(false);  // 更新连接状态为断开
                OnDisconnected?.Invoke();  // 触发断开连接事件
            }

            tcpClient = null;  // 释放TCP客户端资源
            networkStream = null;  // 释放网络流资源

            if (readDataCoroutine != null)
            {
                StopCoroutine(readDataCoroutine);  // 停止读取数据的协程
                readDataCoroutine = null;  // 释放协程资源
            }
        }
    }

    // 更新服务器主机名的方法
    public void UpdateHost(string newHost)
    {
        host = newHost;  // 更新主机名
    }

    // 更新连接状态的UI显示
    private void UpdateConnectionStatus(bool isConnected)
    {
        if (connectionStatusImage != null)
        {
            // 根据连接状态设置UI图像的颜色
            connectionStatusImage.color = isConnected ? connectedColor : disconnectedColor;
        }
    }

    // 协程，用于异步读取服务器发送的数据
    IEnumerator ReadDataAsync()
    {
        byte[] buffer = new byte[4096];  // 用于存储接收的数据的缓冲区

        while (true)
        {
            if (tcpClient != null && tcpClient.Connected && networkStream != null)
            {
                if (networkStream.DataAvailable)  // 检查是否有数据可用
                {
                    Task<int> readTask = networkStream.ReadAsync(buffer, 0, buffer.Length);  // 异步读取数据
                    yield return new WaitUntil(() => readTask.IsCompleted);  // 等待读取完成

                    if (readTask.Exception == null)
                    {
                        int bytesRead = readTask.Result;  // 获取读取的数据字节数
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);  // 将字节数据转换为字符串
                        if (!string.IsNullOrEmpty(message))
                        {
                            OnMessageReceived?.Invoke(message);  // 触发消息接收事件
                        }
                    }
                    else
                    {
                        // 如果读取过程中发生异常，打印错误信息
                        Debug.LogError(readTask.Exception.ToString());
                    }
                }
            }
            yield return null;  // 等待下一帧
        }
    }

    // 在应用程序退出时断开连接
    void OnApplicationQuit()
    {
        Disconnect();  // 断开与服务器的连接
    }
}
