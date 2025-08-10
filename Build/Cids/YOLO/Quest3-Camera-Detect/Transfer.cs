public class DataReceiver : MonoBehaviour {
    private UdpClient receiveClient;
    private Thread receiveThread;
    
    void Start() {
        receiveClient = new UdpClient(54321);
        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();
    }
    
    void ReceiveData() {
        while(true) {
            IPEndPoint endpoint = null;
            byte[] data = receiveClient.Receive(ref endpoint);
            string json = Encoding.UTF8.GetString(data);
            
            // Unity主线程执行
            MainThreadDispatcher.Execute(() => {
                GetComponent<ContourRenderer>().RenderContours(
                    JsonConvert.DeserializeObject<ContourData>(json)
                );
            });
        }
    }
}