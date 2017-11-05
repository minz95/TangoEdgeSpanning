using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Tango;
using UnityEngine;

public class FindEdgeTest : MonoBehaviour, ITangoVideoOverlay, ITangoPointCloud {
    private Texture2D m_texture, m_rgbTexture;
    public float th = 0.09f;
    private Texture2D img, rImg, gImg, bImg, kImg, roImg, goImg, boImg, koImg, ORimg, 
              ANDimg, SUMimg, AVGimg;
    private float[,] rL, gL, bL, kL, ORL, ANDL, SUML, AVGL;
    private Color temp;
    private float t;
    private TangoApplication m_tangoApplication;
    private TangoUnityImageData m_imageBuffer;
    private TangoPointCloud m_pointCloud;
    int m_width;
    int m_height;
    int m_numPixel = 25;
    int m_touchCount;
    Camera m_cam;
    private bool m_waitingForImage;
    private bool m_waitingForDepth;
    private bool m_isTouching;
    public GameObject lineDrawPrefabs;
    private GameObject lineDrawPrefab;
    public GameObject particlePrefab;
    private LineRenderer lineRenderer;
    private List<Vector3> drawPoints = new List<Vector3>();
    List<ParticleSystem.Particle> pointList;
    List<ParticleSystem.Particle> basicBrushPointList = new List<ParticleSystem.Particle>();

    //public ParticleSystem basicBrushParticleSystem;
    //bool particleSystemNeedsUpdate = false;

    void ITangoVideoOverlay.OnTangoImageAvailableEventHandler(TangoEnums.TangoCameraId cameraId,
                                                              TangoUnityImageData imageBuffer)
    {
        if (m_waitingForImage)
        {
            
            m_imageBuffer = imageBuffer;
        }

        m_waitingForImage = false;
    }

    // Use this for initialization
    void Start () {
        m_tangoApplication = FindObjectOfType<TangoApplication>();
        if (m_tangoApplication != null)
        {
            m_tangoApplication.Register(this);
        }
        else
        {
            Debug.Log("No Tango Manager found in scene.");
        }

        m_cam = Camera.main;
        m_pointCloud = FindObjectOfType<TangoPointCloud>();

        img = new Texture2D(m_numPixel*2 + 1, m_numPixel * 2 + 1);
        //initialize textures
        rImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        bImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        gImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        kImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        //initialize gradient arrays
        rL = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        gL = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        bL = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        kL = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        ORL = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        ANDL = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        SUML = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        AVGL = new float[m_numPixel * 2 + 1, m_numPixel * 2 + 1];
        //initialize final textures
        roImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        goImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        boImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        koImg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        ORimg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        ANDimg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        SUMimg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);
        AVGimg = new Texture2D(m_numPixel * 2 + 1, m_numPixel * 2 + 1);

        pointList = basicBrushPointList;
        //basicBrushParticleSystem = FindObjectOfType<ParticleSystem>();
        m_waitingForImage = false;
        m_isTouching = false;
        m_touchCount = 0;
    }
	
	// Update is called once per frame
	void Update () {
        
        if (Input.touchCount > 0)
        {
            if (Input.touches[0].phase == TouchPhase.Began)
            {
                Debug.Log("<<<<<<<<<<<<<<< touch phase began");
                m_isTouching = true;
                lineDrawPrefab = Instantiate(lineDrawPrefabs) as GameObject;
                lineRenderer = lineDrawPrefab.GetComponent<LineRenderer>();
                lineRenderer.positionCount = 0;
            } else if(Input.touches[0].phase == TouchPhase.Ended)
            {
                Debug.Log("<<<<<<<<<<<<<<<<<<<<< touch phase ended");
                m_isTouching = false;
                StartCoroutine(_WaitForImage(Input.touches[0].position));
                drawPoints.Clear();
            }
            /*
            if (particleSystemNeedsUpdate)
            {
                UpdateParticles();
                particleSystemNeedsUpdate = false;
            }
            */
        }

        if(m_isTouching)
        {
            if(m_touchCount == 0)
            {
                Debug.Log("<<<<<<<<<<<<< touching: before coroutine");
                StartCoroutine(_WaitForImage(Input.touches[0].position));
                /*
                if (!drawPoints.Contains(Input.touches[0].position))
                {
                    Debug.Log("<<<<<<<<<<<<<<< draw point added and draw line");
                    drawPoints.Add(Input.touches[0].position);
                    lineRenderer.positionCount = drawPoints.Count;
                    lineRenderer.SetPosition(drawPoints.Count - 1, Input.touches[0].position);
                }
                */
                m_touchCount = 20;
            } else
            {
                m_touchCount--;
            }
            
        }

        if (Input.GetKey(KeyCode.Escape))
        {
            // This is a fix for a lifecycle issue where calling
            // Application.Quit() here, and restarting the application
            // immediately results in a deadlocked app.
            AndroidHelper.AndroidQuit();
        }
        

    }

    public static Color YUV2Color(byte y, byte u, byte v)
    {
        const float Umax = 0.436f;
        const float Vmax = 0.615f;

        float y_scaled = y / 255.0f;
        float u_scaled = 2 * (u / 255.0f - 0.5f) * Umax;
        float v_scaled = 2 * (v / 255.0f - 0.5f) * Vmax;

        return new Color(y_scaled + 1.13983f * v_scaled,
                         y_scaled - 0.39465f * u_scaled - 0.58060f * v_scaled,
                         y_scaled + 2.03211f * u_scaled);
    }

    void CalculateEdges()
    {
        img = m_texture;

        //calculate new textures
        
        for (int x = 0; x < img.width; x++)
        {
            for (int y = 0; y < img.height; y++)
            {
                temp = img.GetPixel(x, y);
                rImg.SetPixel(x, y, new Color(temp.r, 0, 0));
                gImg.SetPixel(x, y, new Color(0, temp.g, 0));
                bImg.SetPixel(x, y, new Color(0, 0, temp.b));
                t = temp.r + temp.g + temp.b;
                t /= 3f;
                kImg.SetPixel(x, y, new Color(t, t, t));
            }
        }
        
        rImg.Apply();
        gImg.Apply();
        bImg.Apply();
        kImg.Apply();
        

        //calculate gradient values
        for (int x = 0; x < img.width; x++)
        {
            for (int y = 0; y < img.height; y++)
            {
                rL[x, y] = gradientValue(x, y, 0, rImg);
                gL[x, y] = gradientValue(x, y, 1, gImg);
                bL[x, y] = gradientValue(x, y, 2, bImg);
                kL[x, y] = gradientValue(x, y, 2, kImg);
                ORL[x, y] = (rL[x, y] >= th || gL[x, y] >= th || bL[x, y] >= th) ? th : 0f;
                ANDL[x, y] = (rL[x, y] >= th && gL[x, y] >= th && bL[x, y] >= th) ? th : 0f;
                SUML[x, y] = rL[x, y] + gL[x, y] + bL[x, y];
                AVGL[x, y] = SUML[x, y] / 3f;
            }
        }
        //create texture from gradient values
        TextureFromGradientRef(rL, th, ref roImg);
        TextureFromGradientRef(gL, th, ref goImg);
        TextureFromGradientRef(bL, th, ref boImg);
        TextureFromGradientRef(kL, th, ref koImg);
        TextureFromGradientRef(ORL, th, ref ORimg);
        TextureFromGradientRef(ANDL, th, ref ANDimg);
        TextureFromGradientRef(SUML, th, ref SUMimg);
        TextureFromGradientRef(AVGL, th, ref AVGimg);
    }

    
    // Update is called once per frame
    void OnGUI()
    {
        GUILayout.BeginHorizontal();
        
        GUILayout.BeginVertical();
        
        GUILayout.Label("original");
        GUILayout.Label(img);
        
        GUILayout.Label("red");
        GUILayout.Label(rImg);
        
        GUILayout.Label("blue");
        GUILayout.Label(bImg);
        GUILayout.EndVertical();
        
        GUILayout.BeginVertical();
        GUILayout.Label("green");
        GUILayout.Label(gImg);
        GUILayout.Label("grey");
        GUILayout.Label(kImg);
        GUILayout.Label("grey detection");
        GUILayout.Label(koImg);
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("red detection");
        GUILayout.Label(roImg);
        GUILayout.Label("green detection");
        GUILayout.Label(goImg);
        GUILayout.Label("blue detection");
        GUILayout.Label(boImg);
        GUILayout.EndVertical();
        
        GUILayout.BeginVertical();
        GUILayout.Label("OR detection");
        GUILayout.Label(ORimg);
        GUILayout.Label("AND detection");
        GUILayout.Label(ANDimg);
        GUILayout.Label("SUM detection");
        GUILayout.Label(SUMimg);
        GUILayout.EndVertical();
        
        GUILayout.BeginVertical();
        GUILayout.Label("Average detection");
        GUILayout.Label(AVGimg);
        GUILayout.EndVertical();
        
        GUILayout.EndHorizontal();
    }
    
    
    float gradientValue(int ex, int why, int colorVal, Texture2D image)
    {
        float lx = 0f;
        float ly = 0f;
        if (ex > 0 && ex < image.width)
            lx = 0.5f * (image.GetPixel(ex + 1, why)[colorVal] - image.GetPixel(ex - 1, why)[colorVal]);
        if (why > 0 && why < image.height)
            ly = 0.5f * (image.GetPixel(ex, why + 1)[colorVal] - image.GetPixel(ex, why - 1)[colorVal]);
        return Mathf.Sqrt(lx * lx + ly * ly);
    }

    Texture2D TextureFromGradient(float[,] g, float thres)
    {
        Texture2D output = new Texture2D(g.GetLength(0), g.GetLength(1));
        for (int x = 0; x < output.width; x++)
        {
            for (int y = 0; y < output.height; y++)
            {
                if (g[x, y] >= thres)
                    output.SetPixel(x, y, Color.black);
                else
                    output.SetPixel(x, y, Color.white);
            }
        }
        output.Apply();
        return output;
    }

    void TextureFromGradientRef(float[,] g, float thres, ref Texture2D output)
    {
        for (int x = 0; x < output.width; x++)
        {
            for (int y = 0; y < output.height; y++)
            {
                if (g[x, y] >= thres)
                    output.SetPixel(x, y, Color.black);
                else
                    output.SetPixel(x, y, Color.white);
            }
        }
        output.Apply();
    }
    /*
    // public bool YV12ToPhoto(byte[] data, int width, int height) //, out Texture2D photo)
    public bool YV12ToRGB(TangoUnityImageData imageBuffer)
    {
        // Texture2D photo = new Texture2D(width, height);
        int width = (int)imageBuffer.width;
        int height = (int)imageBuffer.height;
        m_texture = new Texture2D(width, height);

        int uv_buffer_offset = width * height;

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                int x_index = j;
                if (j % 2 != 0)
                {
                    x_index = j - 1;
                }

                // Get the YUV color for this pixel.
                int yValue = imageBuffer.data[(i * width) + j];
                int uValue = imageBuffer.data[uv_buffer_offset + ((i / 2) * width) + x_index + 1];
                int vValue = imageBuffer.data[uv_buffer_offset + ((i / 2) * width) + x_index];

                // Convert the YUV value to RGB.
                float r = yValue + (1.370705f * (vValue - 128));
                float g = yValue - (0.689001f * (vValue - 128)) - (0.337633f * (uValue - 128));
                float b = yValue + (1.732446f * (uValue - 128));

                Color co = new Color();
                co.b = b < 0 ? 0 : (b > 255 ? 1 : b / 255.0f);
                co.g = g < 0 ? 0 : (g > 255 ? 1 : g / 255.0f);
                co.r = r < 0 ? 0 : (r > 255 ? 1 : r / 255.0f);
                co.a = 1.0f;

                m_texture.SetPixel(width - j - 1, height - i - 1, co);
            }
        }

        return true;
    }
    */
    private IEnumerator _WaitForImage(Vector2 touchPosition)
    {
        m_waitingForImage = true;

        // Turn on the camera and wait for a single depth update
        
        m_tangoApplication.EnableVideoOverlay = true;
        while (m_waitingForImage)
        {
            yield return null;
        }

        m_tangoApplication.EnableVideoOverlay = false;

        m_cam = Camera.main;
        byte[] yuv = m_imageBuffer.data;
        int width = (int)Screen.width;
        int height = (int)Screen.height;
        //Debug.Log("<<<<<<<<<<<<<<<<< width, height: " + m_imageBuffer.width + ", " + m_imageBuffer.height);
        m_texture = new Texture2D(m_numPixel*2+1, m_numPixel*2+1);
        //Vector2 uvCoord = new Vector2(((touchPosition.x) / width - 0.5f) * m_imageBuffer.width,
        //                                ((touchPosition.y) / height -0.5f) * m_imageBuffer.height);
        Debug.Log("<<<<<<<<<<<< touch point sexy (x, y): " + touchPosition.x + ", " + touchPosition.y);
        Vector2 uvCoord = m_cam.ScreenToViewportPoint(touchPosition);
        //Debug.Log("<<<<<<<<<<<< uv nomo (x, y): " + uvCoord.x + ", " + uvCoord.y);
        TangoARScreen arScreen = m_cam.gameObject.GetComponent<TangoARScreen>();
        uvCoord = arScreen.ViewportPointToCameraImagePoint(uvCoord);
        if (width > height)
            uvCoord = new Vector2((1.0f-uvCoord.x) * m_imageBuffer.width, (1.0f-uvCoord.y) * m_imageBuffer.height);
        else
            uvCoord = new Vector2((1.0f - uvCoord.x) * m_imageBuffer.height, (1.0f - uvCoord.y) * m_imageBuffer.width);
        //Debug.Log("<<<<<<<<<<<< uv tango cam img (x, y): " + uvCoord.x + ", " + uvCoord.y);
        //uvCoord = new Vector2(uvCoord.x, 1.0f - uvCoord.y);
        //Debug.Log("<<<<<<<<<<<< uv not flipped (x, y): " + uvCoord.x + ", " + uvCoord.y);
        //uvCoord = m_cam.ViewportToScreenPoint(uvCoord);
        //Debug.Log("<<<<<<<<<<<< uv coordinate (x, y): "+.x+", "+uvCoord.y);

        for (int i = -1* m_numPixel; i < m_numPixel; i++)
        {
            for(int j = -1* m_numPixel; j < m_numPixel; j++)
            {
                float x_pos = uvCoord.x + i;
                float y_pos = uvCoord.y + j;
                if (x_pos < 0 || x_pos > width || y_pos < 0 || y_pos > height)
                {
                    // Debug.Log("<<<<<< Continue : " + (int)x_pos + ", " + (int)y_pos + ", width: " + width + ", height:" + height);
                    continue;
                }
                else
                {
                    Vector3 rgb=_GetRgbFromImageBuffer(m_imageBuffer, (int)x_pos, (int)y_pos);
                    //Debug.Log("<<<<<< x: " + (int)x_pos + ", y: " + (int)y_pos + ", color: " + rgb);
                    /*
                    Color co = new Color(rgb.x < 0 ? 0 : (rgb.x > 255 ? 1 : rgb.x / 255.0f),
                        rgb.y < 0 ? 0 : (rgb.y > 255 ? 1 : rgb.y / 255.0f),
                        rgb.z < 0 ? 0 : (rgb.z > 255 ? 1 : rgb.z / 255.0f),
                        1.0f);
                    
                    rImg.SetPixel(m_numPixel + i, m_numPixel + j,
                        new Color(rgb.x < 0 ? 0 : (rgb.x > 255 ? 1 : rgb.x / 255.0f),0,0));
                    gImg.SetPixel(m_numPixel + i, m_numPixel + j,
                        new Color(0, rgb.y < 0 ? 0 : (rgb.y > 255 ? 1 : rgb.y / 255.0f), 0));
                    bImg.SetPixel(m_numPixel + i, m_numPixel + j,
                        new Color(0,0, rgb.z < 0 ? 0 : (rgb.z > 255 ? 1 : rgb.z / 255.0f)));
                    kImg.SetPixel(m_numPixel + i, m_numPixel + j, 
                                  new Color((rgb.x < 0 ? 0 : (rgb.x > 255 ? 1 : rgb.x / 255.0f))/3f,
                                  (rgb.y < 0 ? 0 : (rgb.y > 255 ? 1 : rgb.y / 255.0f))/3f,
                                  (rgb.z < 0 ? 0 : (rgb.z > 255 ? 1 : rgb.z / 255.0f))/3f));
                    */

                    Color co = new Color(rgb.x, rgb.y, rgb.z);
                    m_texture.SetPixel(m_numPixel - i, m_numPixel - j, co);
                    // Debug.Log("<<<<<< x: " + (m_numPixel+i) + ", y: "+(m_numPixel+j)+", color: "+co);
                }
            }
        }
        
        //YV12ToPhoto(yuv, m_width, m_height, out m_texture);
        CalculateEdges();

        // find the nearest point on the edges
        Vector2 near_pos = FindNearestPointOnEdges(AVGimg);
        //Debug.Log("<<<<<<<<<<<<< nearest position: "+near_pos[0]+","+near_pos[1]);
        // draw the point to the nearest position on the screen (if it is near enough)
        Vector2 snap_pos = new Vector2(touchPosition.x + near_pos[0], 
                                        touchPosition.y + near_pos[1]);
        int near_idx = m_pointCloud.FindClosestPoint(m_cam, snap_pos, 10);
        //Debug.Log("<<<<<<<<<< found the point on the edge: " + m_pointCloud.m_points[near_idx]);
        //DrawPoint(m_pointCloud.m_points[near_idx]);
        //particleSystemNeedsUpdate = true;
        if (!drawPoints.Contains(Input.touches[0].position))
        {
            //Debug.Log("<<<<<<<<<<<<<<< draw point added and draw line");
            drawPoints.Add(m_pointCloud.m_points[near_idx]);
            lineRenderer.positionCount = drawPoints.Count;
            //lineRenderer.SetPosition(drawPoints.Count - 1, m_pointCloud.m_points[near_idx]);
        }
    }

    private Vector2 FindNearestPointOnEdges(Texture2D edges)
    {
        Vector2 min = new Vector2( 100, 100 );
        for(int i = 0; i < edges.width; i++)
        {
            for(int j = 0; j < edges.height; j++)
            {
                //Debug.Log("edge pixel " + i + "," + j + ": " + edges.GetPixel(i,j));
                if(edges.GetPixel(i, j) == Color.black)
                {
                    if(Math.Pow(i-25,2)+Math.Pow(j-25,2) < Math.Pow(min[0]-25,2)+Math.Pow(min[1]-25,2))
                    { 
                        min[0] = i;
                        min[1] = j;
                    }
                } 
            }
        }
        if (min[0] == 100 && min[1] == 100)
            min[0] = 0; min[1] = 0;
        return min;
    }
    /*
    // draw one mouse click, one brush to canvas
    void DrawPoint(Vector3 p)
    {
        var particle = new ParticleSystem.Particle();

        particle.position = p;
        particle.rotation = UnityEngine.Random.Range(0f, 359f);
        particle.startLifetime = 10000;

        pointList.Add(particle);
    }
    
    // a complete array of particles is input into a particular particle system
    public void UpdateParticles()
    {
        // Note: this may not be the most efficient way to do this, if we hit performance issues start here
        var asArray = pointList.ToArray();

        basicBrushParticleSystem.SetParticles(asArray, asArray.Length);
    }
    

    // remove all of the points from one particular particle array
    public void ClearPoints()
    {
        pointList.Clear();

        particleSystemNeedsUpdate = true;
    }

    private void drawPoint(float[,] points)
    {

    }
    */
    /// <summary>
    /// Returns the RGB value at a given theta and phi given a TangoImageBuffer.
    /// </summary>
    /// <param name="buffer">The TangoImageBuffer to sample.</param>
    /// <param name="i">Range from [0..height].</param>
    /// <param name="j">Range from [0..width].</param>
    /// <returns>The RGB value on the buffer at the given theta and phi.</returns>
    private Vector3 _GetRgbFromImageBuffer(Tango.TangoUnityImageData buffer, int i, int j)
    {
        int width = (int)buffer.width;
        int height = (int)buffer.height;
        int uv_buffer_offset = width * height;

        int x_index = j;
        if (j % 2 != 0)
        {
            x_index = j - 1;
        }

        // Get the YUV color for this pixel.
        int yValue = buffer.data[(i * width) + j];
        int uValue = buffer.data[uv_buffer_offset + ((i / 2) * width) + x_index + 1];
        int vValue = buffer.data[uv_buffer_offset + ((i / 2) * width) + x_index];

        // Convert the YUV value to RGB.
        float r = yValue + (1.370705f * (vValue - 128));
        float g = yValue - (0.689001f * (vValue - 128)) - (0.337633f * (uValue - 128));
        float b = yValue + (1.732446f * (uValue - 128));
        Vector3 result = new Vector3(r / 255.0f, g / 255.0f, b / 255.0f);

        // Gamma correct color to linear scale.
        result.x = Mathf.Pow(Mathf.Max(0.0f, result.x), 2.2f);
        result.y = Mathf.Pow(Mathf.Max(0.0f, result.y), 2.2f);
        result.z = Mathf.Pow(Mathf.Max(0.0f, result.z), 2.2f);
        return result;
    }

    void ITangoPointCloud.OnTangoPointCloudAvailable(TangoPointCloudData pointCloud)
    {
        m_waitingForDepth = false;
    }
}
