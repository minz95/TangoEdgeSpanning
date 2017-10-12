using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Tango;

public class EdgeTest : MonoBehaviour, ITangoVideoOverlay
{
    // Constant values for overlay.
    public const float UI_LABEL_START_X = 15.0f;
    public const float UI_LABEL_START_Y = 15.0f;
    public const float UI_LABEL_SIZE_X = 1920.0f;
    public const float UI_LABEL_SIZE_Y = 35.0f;
    private const float METER_TO_PIXEL = 3779.527559055f;

    /// <summary>
    /// A reference to TangoApplication in current scene.
    /// </summary>
    private TangoApplication m_tangoApplication;
    private Camera m_camera;
    private TangoUnityImageData m_imagebuffer;
    public TangoPointCloud m_pointCloud;
    public LineRenderer m_lineRenderer;
    private Vector3[] m_startPoint;
    private Vector3[] m_endPoint;
    private Vector3[] m_nearestPoint;
    private int m_edgeCount;


    void Start () {
        //m_camera = FindObjectOfType<Camera>();
        //m_camera = Camera.main;
        m_tangoApplication = FindObjectOfType<TangoApplication>();
        m_pointCloud = FindObjectOfType<TangoPointCloud>();
        m_imagebuffer = new TangoUnityImageData();

        if (m_tangoApplication != null)
        {
            m_tangoApplication.Register(this);
        }
        else
        {
            Debug.Log("No Tango Manager found in scene.");
        }
    }
    
    
    // Update is called once per frame
    void Update ()
    {
        TangoSupport.TangoSupportEdge[] edges;
        int num_edges;
        //Vector2 guiPosition = new Vector2(Input.GetTouch(0).position.x, Screen.height - Input.GetTouch(0).position.y);
        // RaycastHit hit = new RaycastHit();
        //for (int i = 0; i < Input.touchCount; ++i)
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            Vector2 guiPosition = Vector2.zero;
            if (t.phase == TouchPhase.Began)
            {
                // Construct a ray from the current touch coordinates
                //Ray ray = Camera.main.ScreenPointToRay(t.position);
                //if (Physics.Raycast(Camera.main.ScreenPointToRay(t.position), out hit))
                /*   
                end_points = new Vector3[2];
                end_points[0] = Vector3.zero;
                end_points[1] = Vector3.zero;
                nearest_on_edge = Vector3.zero;
                */
                // Touch t = Input.GetTouch(i);
                // Debug.Log("<<<<<<<<<<<<<<<< x position: " + t.position.x);
                guiPosition = new Vector2(t.position.x, Screen.height - t.position.y);

                m_camera = Camera.main; 
                if (m_pointCloud == null)
                    m_pointCloud = FindObjectOfType<TangoPointCloud>();
                // m_pointCloud = new TangoPointCloud();
                
                if (!FindObjectOfType<TangoPointCloud>())
                {
                    Debug.Log("<<<<<<<<<<<<<<<< need point cloud to find the edges");
                }
                else
                {
                    m_pointCloud = FindObjectOfType<TangoPointCloud>();
                }

                if (m_pointCloud != null)
                {
                    bool edgeResult = m_pointCloud.FindEdges(m_imagebuffer, m_camera, guiPosition,
                        out edges, out num_edges);
                    if (edgeResult == true)
                    {
                        Debug.Log("Touch Points : x  " + t.position.x + " y  " + t.position.y);
                        m_edgeCount = num_edges;
                        m_startPoint = new Vector3[num_edges];
                        m_endPoint = new Vector3[num_edges];
                        m_nearestPoint = new Vector3[num_edges];
                        for(int i = 0; i < num_edges; i++)
                        {
                            m_startPoint[i] = new Vector3(edges[i].end_points_x1,
                                                          -edges[i].end_points_y1,
                                                          edges[i].end_points_z1);
                            Debug.Log(m_startPoint[i]);
                            m_endPoint[i] = new Vector3(edges[i].end_points_x2,
                                                        -edges[i].end_points_y2,
                                                        edges[i].end_points_z2);
                            Debug.Log(m_endPoint[i]);
                            m_nearestPoint[i] = new Vector3(edges[i].closest_point_on_edge_x,
                                                            -edges[i].closest_point_on_edge_y,
                                                            edges[i].closest_point_on_edge_z);
                            Debug.Log(m_nearestPoint[i]);
                        }
                        _RenderLine();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Render the line from the start point to the end point.
    /// </summary>
    private void _RenderLine()
    {
        for (int i = 0; i < m_edgeCount; i++)
        {
            m_lineRenderer.SetPosition(0, m_startPoint[i]);
            m_lineRenderer.SetPosition(1, m_endPoint[i]);
        }
    }

    /// <summary>
    /// Minji Kim 2017-10-10
    /// Render Line with customized points
    /// </summary>
    private void _RenderCustomize(Vector3 v1, Vector3 v2)
    {
        m_lineRenderer.SetPosition(0, v1);
        m_lineRenderer.SetPosition(1, v2);
    }

    /// <summary>
    /// Display simple GUI.
    /// </summary>
 /*
    public void OnGUI()
    {
        if (m_tangoApplication.HasRequiredPermissions)
        {
            GUI.color = Color.black;
            GUI.Label(new Rect(UI_LABEL_START_X,
                               UI_LABEL_START_Y,
                               UI_LABEL_SIZE_X,
                               UI_LABEL_SIZE_Y),
                      "<size=25>" + m_distanceText + "</size>");
        }
    }
*/
    /// <summary>
    /// Unity callback when the component gets destroyed.
    /// </summary>
    public void OnDestroy()
    {
        m_tangoApplication.Unregister(this);
    }

    void ITangoVideoOverlay.OnTangoImageAvailableEventHandler(TangoEnums.TangoCameraId cameraId, TangoUnityImageData imageBuffer)
    {
        m_imagebuffer = imageBuffer;
        //Debug.Log("<<<<<<<<<< In Image Available Event Handler: " + imageBuffer.data.GetValue(0));
    }
}
