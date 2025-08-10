import cv2  # 导入OpenCV库，用于图像处理
from ultralytics import YOLO  # 导入YOLO模型，用于目标检测
import numpy as np  # 导入NumPy库，用于数组操作
import socket  # 导入socket库，用于网络通信
import json  # 导入JSON库，用于处理JSON格式数据
import torch  # 导入PyTorch库，用于检查和使用硬件加速设备

# 设置服务器的函数，监听指定的IP地址和端口
def setup_server(server_ip, server_port):
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)  # 创建TCP套接字
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)  # 设置端口复用
    server_socket.bind((server_ip, server_port))  # 绑定IP地址和端口
    server_socket.listen(1)  # 开始监听连接
    print("Waiting for a connection...")  # 打印等待连接的消息
    return server_socket  # 返回服务器套接字

# 处理客户端连接的函数，捕获视频帧，进行目标检测，并将结果发送给客户端
def handle_client(client_socket, cap, model, class_names, device):
    while True:
        ret, frame = cap.read()  # 从摄像头读取一帧
        if not ret:
            break  # 如果读取失败，则退出循环

        h, w, _ = frame.shape  # 获取帧的高度和宽度
        min_dim = min(h, w)  # 计算最小的尺寸（高度或宽度）
        start_x = (w - min_dim) // 2  # 计算裁剪区域的起始X坐标
        start_y = (h - min_dim) // 2  # 计算裁剪区域的起始Y坐标
        cropped_frame = frame[start_y:start_y+min_dim, start_x:start_x+min_dim]  # 裁剪帧以使其为正方形

        resized_frame = cv2.resize(cropped_frame, (1024, 1024))  # 将裁剪后的帧调整为YOLO模型的输入尺寸

        results = model(resized_frame, device=device)  # 使用YOLO模型进行目标检测
        result = results[0]  # 获取检测结果
        bboxes = np.array(result.boxes.xyxy.cpu(), dtype="int")  # 获取边界框坐标
        classes = np.array(result.boxes.cls.cpu(), dtype="int")  # 获取检测到的类别索引
        detection_data = []  # 用于存储检测结果的数据列表

        for cls, bbox in zip(classes, bboxes):  # 遍历检测到的每个目标
            x, y, x2, y2 = bbox  # 解包边界框坐标
            class_name = class_names[cls]  # 获取类别名称
            detection_data.append({"class_name": class_name, "bbox": [int(x), int(y), int(x2), int(y2)]})  # 将检测结果添加到列表中

            cv2.rectangle(resized_frame, (x, y), (x2, y2), (0, 0, 255), 2)  # 在图像上绘制边界框
            cv2.putText(resized_frame, class_name, (x, y - 5), cv2.FONT_HERSHEY_PLAIN, 2, (255, 255, 255), 2)  # 在图像上绘制类别名称

        try:
            wrapped_data = json.dumps({"detections": detection_data})  # 将检测结果编码为JSON格式
            client_socket.send(wrapped_data.encode('utf-8'))  # 发送JSON数据给客户端
            client_socket.send(b'\n')  # 发送换行符以分隔消息
        except BrokenPipeError:
            print("Client disconnected.")  # 打印客户端断开连接的消息
            break  # 退出循环

        cv2.imshow("img", resized_frame)  # 显示处理后的图像
        key = cv2.waitKey(1)  # 等待键盘事件（此处未使用键值）

# 主函数，程序的入口点
def main():
    server_ip = "192.168.1.72"  # 设置服务器IP地址，监听所有网络接口
    server_port = 56789  # 设置服务器端口

    with open("classes.txt", "r") as f:  # 打开类别名称文件
        class_names = [line.strip() for line in f.readlines()]  # 读取文件中的类别名称

    # 打开摄像头 1
    # 0：通常表示系统中第一个摄像头设备。一般是内置摄像头（如笔记本电脑的内置摄像头）。
    # 1：通常表示系统中的第二个摄像头设备。OBS 的虚拟摄像头功能可以将 OBS 的输出画面作为一个摄像头设备输出。
    # 把 Quest 投屏画面作为 OBS 输出画面，可以使得 OBS 虚拟摄像头捕获到 Quest 拍摄的画面
    cap = cv2.VideoCapture(1)

    # 加载YOLO模型（使用预训练权重文件）
    model = YOLO("yolov8m.pt")

    if torch.cuda.is_available():
        device = "cuda"  # 如果CUDA可用，使用GPU加速
    elif torch.backends.mps.is_available():
        device = "mps"  # 如果MPS可用，使用苹果设备的加速
    else:
        device = "cpu"  # 否则，使用CPU

    print(f"Using device: {device}")  # 打印使用的设备

    server_socket = setup_server(server_ip, server_port)  # 设置服务器

    while True:
        client_socket, address = server_socket.accept()  # 接受客户端连接
        print(f"Connection from {address} has been established.")  # 打印连接信息
        handle_client(client_socket, cap, model, class_names, device)  # 处理客户端连接
        client_socket.close()  # 关闭客户端连接
        print("Connection closed. Waiting for new connection...")  # 打印等待新连接的信息

    cap.release()  # 释放摄像头资源
    cv2.destroyAllWindows()  # 关闭所有OpenCV窗口
    server_socket.close()  # 关闭服务器套接字

# 如果此脚本作为主程序运行，则执行main函数
if __name__ == "__main__":
    main()
