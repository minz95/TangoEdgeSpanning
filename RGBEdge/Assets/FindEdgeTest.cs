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
    bool b_pointsUpdated = false;
    bool b_excludingPoints = false;
    
    public GameObject particlePrefab;
    ParticleSystem m_particleSystem;
    public GameObject PolygonCollider;
    PolygonCollider2D m_polyColl;

    private List<Vector3> drawPoints = new List<Vector3>();
    Vector2[] polyPoints = { };
    ParticleSystem.Particle[] cloud;
    private List<Vector3> pointsInEdges = new List<Vector3>();
    public int resolution = 30;

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

        //pointList = basicBrushPointList;
        //basicBrushParticleSystem = FindObjectOfType<ParticleSystem>();
        m_waitingForImage = false;
        m_isTouching = false;
        m_touchCount = 10;
        particlePrefab = Instantiate(particlePrefab) as GameObject;
        DontDestroyOnLoad(particlePrefab);
        m_particleSystem = particlePrefab.GetComponent<ParticleSystem>();
        DontDestroyOnLoad(m_particleSystem);
        PolygonCollider = Instantiate(PolygonCollider) as GameObject;
        m_polyColl = PolygonCollider.GetComponent<PolygonCollider2D>();
    }
	
	// Update is called once per frame
	void Update () {
        // [EXPERIMENTAL]
        // draw polygon collider using the edge points
        // don't do other tasks when excluding points 
        if (Input.touchCount > 0)
        {
            float x_pos = Input.touches[0].position.x;
            float y_pos = Input.touches[0].position.y;
            if (x_pos > Screen.width-220 && x_pos < Screen.width-20 && 
                y_pos > 100 && y_pos < 280)
            {
                b_excludingPoints = true;
            }
            else
            {
                if (!b_excludingPoints)
                {
                    if (Input.touches[0].phase == TouchPhase.Began)
                        StartCoroutine(_WaitForImage(Input.touches[0].position));
                    else if (Input.touches[0].phase == TouchPhase.Moved)
                    {
                        // calculate edges only once per 10 frames
                        if (m_touchCount == 0)
                        {
                            StartCoroutine(_WaitForImage(Input.touches[0].position));
                            m_touchCount = 10;
                        }
                        else
                        {
                            m_touchCount--;
                            // add particles according to the resolution
                            int near_idx = m_pointCloud.FindClosestPoint(m_cam, Input.touches[0].position, 10);
                            drawPoints.Add(m_pointCloud.m_points[near_idx]);
                            SetPoints();
                        }
                    }
                    else if (Input.touches[0].phase == TouchPhase.Ended)
                        StartCoroutine(_WaitForImage(Input.touches[0].position));
                }
            }
        }
        if (b_pointsUpdated)
        {
            m_particleSystem.SetParticles(cloud, cloud.Length);
            b_pointsUpdated = false;
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
        GUI.color = Color.white;

        // Minji Kim
        // gui for showing points inside the selected edges
        if (GUI.Button(new Rect(Screen.width - 220, 100, 200, 180), "<size=30>Find Floor</size>"))
        {
            if(b_excludingPoints)
            {
                Debug.Log("<<<< button: find edges mode");
                // clear polygon
                ClearPolygon();
                b_excludingPoints = false;
            } else
            {
                b_excludingPoints = true;
                Debug.Log("<<<< button: find polygon mode");
                // show circular progress bar while the task is processing
                // call find polygon function
                FindPolygon();
                // call points inside polygon function
                FindPointsInPolygon();
                // call draw color mesh with points function, or just color the points using particles
                SetPoints();
                // show gui button for storing selected point cloud || mesh
                // if button is clicked, store the point cloud and the mesh
            }
        }
        /*
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
        */
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

    public void SetPoints(/*Vector3[] positions, Color[] colors*/)
    {
        Debug.Log("<<<<<<<<<<<<<<< Set points is called");
        if (b_excludingPoints)
        {
            Debug.Log("<<<<<<<<<<<< setting points inside the edges");
            cloud = new ParticleSystem.Particle[pointsInEdges.Count];

            for (int jj = 0; jj < pointsInEdges.Count; ++jj)
            {
                cloud[jj].position = pointsInEdges[jj];
                cloud[jj].startColor = new Color(0, 255, 0);
                cloud[jj].startSize = 0.1f;
            }
        }
        else
        {
            Debug.Log("<<<<<<<<<<<<<<< setting points of the edges");
            cloud = new ParticleSystem.Particle[drawPoints.Count];
            polyPoints = new Vector2[drawPoints.Count];

            for (int ii = 0; ii < drawPoints.Count; ++ii)
            {
                cloud[ii].position = drawPoints[ii];
                cloud[ii].startColor = new Color(255, 0, 0);     
                cloud[ii].startSize = 0.1f;
                polyPoints[ii] = new Vector2(drawPoints[ii].x, drawPoints[ii].y);
            }
        }
        b_pointsUpdated = true;
    }

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
        m_texture = new Texture2D(m_numPixel*2+1, m_numPixel*2+1);

        Vector2 uvCoord = m_cam.ScreenToViewportPoint(touchPosition);
        TangoARScreen arScreen = m_cam.gameObject.GetComponent<TangoARScreen>();
        uvCoord = arScreen.ViewportPointToCameraImagePoint(uvCoord);
        if (width > height)
            uvCoord = new Vector2((1.0f-uvCoord.x) * m_imageBuffer.width, (1.0f-uvCoord.y) * m_imageBuffer.height);
        else
            uvCoord = new Vector2((1.0f - uvCoord.x) * m_imageBuffer.height, (1.0f - uvCoord.y) * m_imageBuffer.width);
        
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
                }
            }
        }
        Debug.Log("<<<<<<< Before calculating edges");
        CalculateEdges();
        Debug.Log("<<<<<<< Before finding nearest point");
        // find the nearest point on the edges
        Vector2 near_pos = FindNearestPointOnEdges(AVGimg);
        Debug.Log("<<<<<<< After finding nearest point");
        // draw the point to the nearest position on the screen (if it is near enough)
        Vector2 snap_pos = new Vector2(touchPosition.x + near_pos[0], 
                                        touchPosition.y + near_pos[1]);
        int near_idx = m_pointCloud.FindClosestPoint(m_cam, snap_pos, 10);
        drawPoints.Add(m_pointCloud.m_points[near_idx]);
        Debug.Log("<<<<<<< Before setting points to particles");
        SetPoints();
        Debug.Log("<<<<<<< After setting points to particles");
    }

    private Vector2 FindNearestPointOnEdges(Texture2D edges)
    {
        Vector2 min = new Vector2( 100, 100 );
        for(int i = 0; i < edges.width; i++)
        {
            for(int j = 0; j < edges.height; j++)
            {
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
            min[0] = m_numPixel; min[1] = m_numPixel;
        return min;
    }

    // remove all of the points from one particular particle array
    public void ClearPoints()
    {
        drawPoints.Clear();

        b_pointsUpdated = true;
    }

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

    // Make Polygon with given edge points
    void FindPolygon()
    {
        Debug.Log("Want to find polygon now >>>>>>>>>>>");
        if (polyPoints.Length < 3) return;
        m_polyColl.points = polyPoints;
        m_polyColl.pathCount = 700;
        m_polyColl.SetPath(0, polyPoints);
        //m_polyColl.isTrigger = true;
        Debug.Log("Polygon is triggerred now >>>>>>>>>>>");
    }

    void ClearPolygon()
    {
        m_polyColl.points = new Vector2[0];
        //m_polyColl.isTrigger = false;
    }

    // very slow operation
    void FindPointsInPolygon()
    {
        if(m_pointCloud == null)
        {
            Debug.Log("point cloud is null");
            return;
        }
        for(int i = 0; i < m_pointCloud.m_pointsCount; i++)
        {
            if (m_polyColl.OverlapPoint((Vector2)m_pointCloud.m_points[i]))
            {
                pointsInEdges.Add(m_pointCloud.m_points[i]);
            }
        }
    }

    void ITangoPointCloud.OnTangoPointCloudAvailable(TangoPointCloudData pointCloud)
    {
        m_waitingForDepth = false;
    }
}
