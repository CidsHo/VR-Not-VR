/*
 * 这个脚本用于处理从机器学习模型（例如 YOLOV8）通过网络连接接收到的检测结果。
 * 它监听包含检测数据的JSON编码消息，解析数据，并相应地更新UI。
 * 该脚本适用于需要在Unity项目中进行目标检测和在画布上进行可视化的场景。
 *
 * 功能：
 * - 监听来自NetworkingClient的检测消息。
 * - 将JSON消息解析为检测列表。
 * - 动态更新UI，显示检测到的目标的边界框和类别名称。
 * - 使用TextMeshPro在检测框内显示类别名称。
 * - 自动清理旧的UI元素，确保只显示最新的检测结果。
 * - 在网络连接断开时清除边界框。
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;  // 引入TextMeshPro的命名空间，用于处理UI文字显示

[Serializable]  // 标记为可序列化，允许从JSON数据中解析
public class Detection
{
    public string class_name;  // 检测到的目标的类别名称
    public List<int> bbox;  // 边界框坐标，包含[x, y, x2, y2]，(x, y) 是矩形框的左上角点，它定义了矩形的起始位置。(x2, y2) 是矩形框的右下角点，它定义了矩形的终止位置。
}

[Serializable]  // 标记为可序列化，允许从JSON数据中解析
public class DetectionWrapper
{
    public List<Detection> detections;  // 包含检测对象的列表
}

public class MessageHandlerYOLO : MonoBehaviour
{
    public NetworkingClient networkingClient;  // 用于接收消息的网络客户端
    public GameObject imagePrefab;  // 用于显示检测结果的UI预制体
    public RectTransform canvasRectTransform;  // 画布的RectTransform，用于定位UI元素

    //adjustWidth 和 adjustHeight 通常设置为 1024 是因为这与模型输入图像的尺寸一致。
    //例如，如果目标检测模型（如 YOLO）期望输入图像为 1024x1024 分辨率，那么为了在 Unity 画布上正确地映射和显示检测结果，
    //坐标转换时需要假设原始图像的宽度和高度是 1024。这确保了从像素坐标到画布坐标的准确映射，避免了显示错位或尺寸不对的问题。
    public int adjustWidth = 1024;  // 检测结果的基准宽度，用于坐标转换
    public int adjustHeight = 1024;  // 检测结果的基准高度，用于坐标转换
    private readonly List<GameObject> activeImages = new();  // 当前显示的UI元素列表，用于管理和清理

    // 当脚本启用时，注册事件处理程序
    private void OnEnable()
    {
        networkingClient.OnMessageReceived += HandleMessage;  // 注册消息接收事件的处理程序
        networkingClient.OnDisconnected += HandleDisconnection;  // 注册断开连接事件的处理程序
    }

    // 当脚本禁用时，取消注册事件处理程序
    private void OnDisable()
    {
        networkingClient.OnMessageReceived -= HandleMessage;  // 取消注册消息接收事件的处理程序
        networkingClient.OnDisconnected -= HandleDisconnection;  // 取消注册断开连接事件的处理程序
    }

    // 处理接收到的消息
    private void HandleMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("Received an empty message.");  // 如果接收到的消息为空，记录错误
            return;
        }
        try
        {
            // 将JSON消息解析为DetectionWrapper对象
            DetectionWrapper detectionWrapper = JsonUtility.FromJson<DetectionWrapper>(message);
            if (detectionWrapper != null && detectionWrapper.detections != null)
            {
                UpdateUI(detectionWrapper.detections);  // 更新UI，显示解析出的检测结果
            }
            else
            {
                Debug.LogError("Parsed JSON is null or does not contain detections.");  // 如果解析结果为空或不包含检测数据，记录错误
            }
        }
        catch (ArgumentException e)
        {
            Debug.LogError("JSON parse error: " + e.Message);  // 如果JSON解析失败，记录错误信息
        }
    }

    // 更新UI，显示检测结果
    private void UpdateUI(List<Detection> detections)
    {
        ClearActiveImages();  // 清除之前的UI元素

        foreach (var detection in detections)
        {
            CreateImage(detection);  // 为每个检测对象创建新的UI元素
        }
    }

    // 创建显示检测结果的UI元素
    private void CreateImage(Detection detection)
    {
        GameObject imageGO = Instantiate(imagePrefab, canvasRectTransform);  // 实例化UI预制体
        RectTransform rectTransform = imageGO.GetComponent<RectTransform>();

        float x = detection.bbox[0];
        float y = detection.bbox[1];
        float x2 = detection.bbox[2];
        float y2 = detection.bbox[3];

        // 计算边界框的宽度和高度
        float width = x2 - x;  // 计算边界框的宽度，右下角的x坐标减去左上角的x坐标
        float height = y2 - y;  // 计算边界框的高度，右下角的y坐标减去左上角的y坐标

        // 获取画布的尺寸
        Vector2 canvasSize = canvasRectTransform.sizeDelta;  // 获取画布的宽度和高度（单位为画布坐标系）

        // 将原始图像中的边界框坐标转换为画布坐标系中的坐标
        float xPos = x / adjustWidth * canvasSize.x;  // 将原始图像的x坐标归一化后映射到画布的宽度，得到在画布上的x坐标
        float yPos = y / adjustHeight * canvasSize.y;  // 将原始图像的y坐标归一化后映射到画布的高度，得到在画布上的y坐标

        // 将边界框的宽度和高度转换为画布上的尺寸
        float rectWidth = width / adjustWidth * canvasSize.x;  // 将原始图像中边界框的宽度归一化后映射到画布的宽度，得到在画布上的宽度
        float rectHeight = height / adjustHeight * canvasSize.y;  // 将原始图像中边界框的高度归一化后映射到画布的高度，得到在画布上的高度


        // 设置UI元素的锚点位置，使其与画布中的边界框位置对应
        // xPos 是边界框左上角的 x 坐标，canvasSize.y - yPos - rectHeight 用于计算左上角的 y 坐标
        rectTransform.anchoredPosition = new Vector2(xPos, canvasSize.y - yPos - rectHeight);

        // 设置UI元素的宽度和高度，使其与检测到的边界框大小匹配
        rectTransform.sizeDelta = new Vector2(rectWidth, rectHeight);

        // 获取TextMeshPro组件，设置检测类别名称
        TextMeshProUGUI textMeshPro = imageGO.GetComponentInChildren<TextMeshProUGUI>();
        if (textMeshPro != null)
        {
            textMeshPro.text = detection.class_name;  // 显示类别名称
        }
        else
        {
            Debug.LogError("TextMeshProUGUI component not found in the image prefab.");  // 如果预制体中没有TextMeshPro组件，记录错误
        }

        activeImages.Add(imageGO);  // 将新的UI元素添加到活动列表中
    }

    // 清除当前显示的UI元素
    private void ClearActiveImages()
    {
        foreach (var image in activeImages)
        {
            Destroy(image);  // 销毁UI元素
        }
        activeImages.Clear();  // 清空活动列表
    }

    // 处理断开连接事件
    private void HandleDisconnection()
    {
        ClearActiveImages();  // 断开连接时清除所有UI元素
    }
}
