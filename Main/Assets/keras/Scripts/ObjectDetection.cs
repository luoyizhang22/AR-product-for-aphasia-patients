using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;
using System.Linq;
using UnityEngine.UI;

public class Test : MonoBehaviour
{
    public NNModel modelAsset; // �洢������ģ�͵���Դ
    private Model m_RuntimeModel; // ����ʱģ��
    private IWorker worker; // ����ִ��ģ������Ĺ�����
    public GameObject objPrefab; // obj����
    private int modelInputHeight = 1024; // ģ������������߶�
    private int modelInputWidth = 704; // ģ��������������

    private List<GameObject> createdBalls = new List<GameObject>(); // �����Ѵ�������

    private WebCamTexture webCamTexture; // ����ͷ����
    public Canvas canvas; // ���� Canvas ����

    //public GameObject plane; // ���� Plane ����

    public RawImage cameraFeed; // ������ʾ����ͷ�����UI���

    private void Start()
    {
        m_RuntimeModel = ModelLoader.Load(modelAsset); // ����������ģ��

        // ʹ��GPU��˴���������
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, m_RuntimeModel); // ����������

        //ʹ��CPU     worker = WorkerFactory.CreateWorker(WorkerFactory.Type.CSharpBurst, m_RuntimeModel); 

        // ��ʼ������ͷ
        webCamTexture = new WebCamTexture();
        webCamTexture.Play();

        // ������ͷ��������Ӧ�õ�RawImage�����
        cameraFeed.texture = webCamTexture;

    }


    private void OnDestroy()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
    }

    private void Update()
    {
        if (webCamTexture.isPlaying) // �������ͷ���ڲ���
        {
            Texture2D currentFrame = GetCurrentFrame(); // ��ȡ��ǰ����ͷ֡
            if (currentFrame != null)
            {
                ClearBalls(); // �����һ֡��������
                Predict(currentFrame); // ִ��Ŀ����
            }
        }
    }

    public Texture2D GetCurrentFrame()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying)
            return null;

        Texture2D tex = new Texture2D(webCamTexture.height, webCamTexture.width, TextureFormat.RGB24, false); // ��߽���
        Color32[] pixels = webCamTexture.GetPixels32();
        Color32[] rotatedPixels = RotateTexture(pixels, webCamTexture.width, webCamTexture.height);

        tex.SetPixels32(rotatedPixels);
        tex.Apply();

        return tex;
    }

    private Color32[] RotateTexture(Color32[] pixels, int width, int height)
    {
        Color32[] rotatedPixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (width - x - 1) * height + y; // ��ʱ����ת90��
                rotatedPixels[index] = pixels[y * width + x];
            }
        }
        return rotatedPixels;
    }


    private float nmsThreshold = 0.5f; // �Ǽ���ֵ������ֵ

    public void Predict(Texture2D inputTex)
    {
        // Resize input texture to match model input size
        Texture2D resizedTex = ResizeTexture(inputTex, modelInputWidth, modelInputHeight);

        Tensor inputTensor = new Tensor(resizedTex, channels: 3);
        try
        {
            worker.Execute(inputTensor);
            Tensor outputTensor = worker.PeekOutput();
            try
            {
                List<(float confidence, Vector2 boxCenter, Vector2 boxSize)> detections = new List<(float, Vector2, Vector2)>();

                for (int boxIndex = 0; boxIndex < outputTensor.width; boxIndex++)
                {
                    float confidence = outputTensor[0, 0, boxIndex, 4];
                    if (confidence > 0.5f)
                    {
                        Vector2 boxCenter = new Vector2(outputTensor[0, 0, boxIndex, 0], outputTensor[0, 0, boxIndex, 1]);
                        Vector2 boxSize = new Vector2(outputTensor[0, 0, boxIndex, 2], outputTensor[0, 0, boxIndex, 3]);
                        detections.Add((confidence, boxCenter, boxSize));
                    }
                }

                // ִ�зǼ���ֵ����
                List<(float, Vector2, Vector2)> nmsDetections = PerformNonMaximumSuppression(detections);

                ClearBalls(); // �����һ֡��������

                // �����µ���
                foreach (var (confidence, boxCenter, boxSize) in nmsDetections)
                {
                    CreateBall(boxCenter, boxSize);
                }
            }
            finally
            {
                outputTensor.Dispose();
            }
        }
        finally
        {
            inputTensor.Dispose();
        }
    }



    private List<(float, Vector2, Vector2)> PerformNonMaximumSuppression(List<(float, Vector2, Vector2)> detections)
    {
        List<(float, Vector2, Vector2)> nmsDetections = new List<(float, Vector2, Vector2)>();
        List<(float, Vector2, Vector2)> sortedDetections = detections.OrderByDescending(d => d.Item1).ToList(); // �����ŶȽ�������

        while (sortedDetections.Count > 0)
        {
            (float confidence, Vector2 boxCenter, Vector2 boxSize) = sortedDetections[0];
            nmsDetections.Add((confidence, boxCenter, boxSize));
            sortedDetections.RemoveAt(0);

            for (int i = sortedDetections.Count - 1; i >= 0; i--)
            {
                (float otherConfidence, Vector2 otherBoxCenter, Vector2 otherBoxSize) = sortedDetections[i];
                float iou = CalculateIoU(boxCenter, boxSize, otherBoxCenter, otherBoxSize);
                if (iou > nmsThreshold)
                {
                    sortedDetections.RemoveAt(i);
                }
            }
        }

        return nmsDetections;
    }

    private float CalculateIoU(Vector2 box1Center, Vector2 box1Size, Vector2 box2Center, Vector2 box2Size)
    {
        float x1 = box1Center.x - box1Size.x / 2;
        float y1 = box1Center.y - box1Size.y / 2;
        float x2 = box1Center.x + box1Size.x / 2;
        float y2 = box1Center.y + box1Size.y / 2;

        float x3 = box2Center.x - box2Size.x / 2;
        float y3 = box2Center.y - box2Size.y / 2;
        float x4 = box2Center.x + box2Size.x / 2;
        float y4 = box2Center.y + box2Size.y / 2;

        float left = Mathf.Max(x1, x3);
        float right = Mathf.Min(x2, x4);
        float bottom = Mathf.Max(y1, y3);
        float top = Mathf.Min(y2, y4);

        float interArea = Mathf.Max(0, right - left) * Mathf.Max(0, top - bottom);
        float box1Area = (x2 - x1) * (y2 - y1);
        float box2Area = (x4 - x3) * (y4 - y3);
        float unionArea = box1Area + box2Area - interArea;

        return interArea / unionArea;
    }

    // ������ķ���
    void CreateBall(Vector2 pos, Vector2 size)
    {
        float maxSize = Mathf.Max(size.x, size.y);
        Vector2 newSize = new Vector2(maxSize, maxSize);

        // ����x��y��ƫ����
        pos += new Vector2(-300f, -450f);

        GameObject newObj = Instantiate(objPrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity); // ʵ����OBJԤ����
        newObj.transform.localScale = new Vector3(newSize.x * 0.3f, newSize.y * 0.3f, newSize.x * 0.3f); // ����С��С��ԭ����0.5��
        newObj.transform.SetParent(canvas.transform, false); // ���ø�����Ϊ Canvas����������ռ䲻��
        createdBalls.Add(newObj); // ���´�����OBJ��ӵ��б���
    }

    // �����һ֡��������
    void ClearBalls()
    {
        foreach (GameObject obj in createdBalls)
        {
            Destroy(obj); // ����֮ǰ������OBJ
        }
        createdBalls.Clear(); // ����б�
    }

    // Resize input texture to match model input size
    Texture2D ResizeTexture(Texture2D inputTex, int targetWidth, int targetHeight)
    {
        Texture2D resizedTex = new Texture2D(targetWidth, targetHeight);
        Graphics.ConvertTexture(inputTex, resizedTex);
        return resizedTex;
    }
}
