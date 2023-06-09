using System;
using UnityEngine;

public class ResizeBoard : MonoBehaviour
{
    private Camera _camera;
    private float _prevRatio;

    private void Start()
    {
        _camera = GetComponent<Camera>();
    }
    
    void Update()
    {
        var ratio =  Screen.width / (float) Screen.height;
        if(Math.Abs(ratio - _prevRatio) < 0.01) return;
        _prevRatio = ratio;
        var y = Math.Min(Math.Max(-14 * ratio + 39, 18), 38);
        _camera.transform.position = new Vector3(0, y, 0);
    }
}
