# MercuryAR

An AR application developed with Unity that recognizes specific objects and renders models. The program can be packaged and deployed on Android devices.

## Program Versions

- **YOLO Version:** YOLOv8  
  [Ultralytics GitHub](https://github.com/ultralytics/ultralytics)

- **Model Version:** ONNX 10

- **Unity Version:** 2022.3.23f1

- **Barracuda Version:** 3.0.1  
  [Unity Barracuda Documentation](https://docs.unity3d.com/Packages/com.unity.barracuda@3.0/manual/index.html)

---

## Notes

1. **YOLO Folder**:  
   Contains all training, prediction, and export code. For dependencies and setup details, refer to the [official documentation](https://docs.ultralytics.com).

2. **Unity Project Files**:  
   All folders except **YOLO** are Unity project files that can be imported into Unity.  
   The main project script is `ObjectDetection.cs`, and detailed logic is provided in the code comments.

3. **ONNX Model Import**:  
   When importing a custom ONNX model, choose version 9 or 10. Other versions may cause errors with Barracuda.

4. **Camera Feed Rotation**:  
   On Android devices, the camera feed is automatically rotated 90 degrees. This has been compensated for in the code.  
   If you want to use the program on a PC, modify the code to remove the rotation-related instructions.
