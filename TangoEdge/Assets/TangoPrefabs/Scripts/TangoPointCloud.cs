// <copyright file="TangoPointCloud.cs" company="Google">
//
// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using Tango;
using UnityEngine;

/// <summary>
/// Utility functions for working with and visualizing point cloud data from the
/// Tango depth API. Used by the Tango Point Cloud prefab to enable depth point
/// functionality.
/// </summary>
public class TangoPointCloud : MonoBehaviour, ITangoPointCloud
{
    /// <summary>
    /// If set, the point cloud will be transformed to be in the Area 
    /// Description frame.
    /// </summary>
    public bool m_useAreaDescriptionPose;

    /// <summary>
    /// If set, update the point cloud's mesh (very slow, useful for debugging).
    /// </summary>
    public bool m_updatePointsMesh;

    /// <summary>
    /// The points of the point cloud, in world space.
    /// 
    /// Note that not every member of this array will be filled out. See
    /// m_pointsCount.
    /// </summary>
    [HideInInspector]
    public Vector3[] m_points;

    /// <summary>
    /// The number of points in m_points.
    /// </summary>
    [HideInInspector]
    public int m_pointsCount = 0;

    /// <summary>
    /// The Tango timestamp from the last update of m_points.
    /// </summary>
    [HideInInspector]
    public double m_depthTimestamp;

    /// <summary>
    /// The average depth (relative to the depth camera).
    /// </summary>
    [HideInInspector]
    public float m_overallZ = 0.0f;

    /// <summary>
    /// Time between the last two depth events.
    /// </summary>
    [HideInInspector]
    public float m_depthDeltaTime = 0.0f;

    /// <summary>
    /// The position of the floor at y height when FindFloor has been called.
    /// 
    /// The default value is 0, even if no floor has been found. When FindFloor has completed successfully,
    /// the result is assigned here.
    /// </summary>
    [HideInInspector]
    public float m_floorPlaneY = 0.0f;

    /// <summary>
    /// Check if a floor has been found. 
    /// 
    /// The value is <c>true</c> if the method FindFloor has successfully found a floor, which is assigned 
    /// to m_floorPlaneY. The value is always <c>false</c> if FindFloor has not been called.
    /// </summary>
    [HideInInspector]
    public bool m_floorFound = false;

    /// <summary>
    /// The maximum points displayed.  Just some constant value.
    /// </summary>
    private const int MAX_POINT_COUNT = 61440;

    /// <summary>
    /// The minimum number of points near a world position y to determine that it is a reasonable floor.
    /// </summary>
    private const int RECOGNITION_THRESHOLD = 1000;

    /// <summary>
    /// The minimum number of points near a world position y to determine that it is not simply noise points.
    /// </summary>
    private const int NOISE_THRESHOLD = 500;

    /// <summary>
    /// The interval in meters between buckets of points. For example, a high sensitivity of 0.01 will group 
    /// points into buckets every 1cm.
    /// </summary>
    private const float SENSITIVITY = 0.02f;
    private TangoApplication m_tangoApplication;

    // Matrices for transforming pointcloud to world coordinates.
    // This equation will take account of the camera sensors extrinsic.
    // Full equation is:
    // Matrix4x4 unityWorldTDepthCamera =
    // m_unityWorldTStartService * startServiceTDevice * Matrix4x4.Inverse(m_imuTDevice) * m_imuTDepthCamera;
    private DMatrix4x4 m_imuTDevice;
    private DMatrix4x4 m_imuTDepthCamera;

    // Convenience matrix for Matrix4x4.Inverse(m_imuTDevice) * m_imuTDepthCamera
    private DMatrix4x4 m_deviceTDepthCamera;

    /// <summary>
    /// Color camera intrinsics.
    /// </summary>
    private TangoCameraIntrinsics m_colorCameraIntrinsics;

    /// <summary>
    /// If the camera data has already been set up.
    /// </summary>
    private bool m_cameraDataSetUp;

    /// <summary>
    /// Mesh this script will modify.
    /// </summary>
    private Mesh m_mesh;
    private Renderer m_renderer;

    // Pose controller from which the offset is queried.
    private TangoDeltaPoseController m_tangoDeltaPoseController;

    /// <summary>
    /// Set to <c>true</c> when currently attempting to find a floor using depth points, <c>false</c> when not
    /// floor finding.
    /// </summary>
    private bool m_findFloorWithDepth = false;

    /// <summary>
    /// Used for floor finding, container for the number of points that fall into a y bucket within a sensitivity range.
    /// </summary>
    private Dictionary<float, int> m_numPointsAtY;

    /// <summary>
    /// Used for floor finding, the list of y value buckets that have sufficient points near that y position height
    /// to determine that it not simply noise.
    /// </summary>
    private List<float> m_nonNoiseBuckets;

    /// <summary>
    /// The most recent point cloud data received.
    /// </summary>
    private TangoPointCloudData m_mostRecentPointCloud;

    /// <summary>
    /// Transformation for the most recent point cloud.
    /// </summary>
    private Matrix4x4 m_mostRecentUnityWorldTDepthCamera;

    /// @cond
    /// <summary>
    /// Use this for initialization.
    /// </summary>
    public void Start()
    {
        m_tangoApplication = FindObjectOfType<TangoApplication>();
        m_tangoApplication.Register(this);

        m_tangoDeltaPoseController = FindObjectOfType<TangoDeltaPoseController>();

        // Assign triangles, note: this is just for visualizing point in the mesh data.
        m_points = new Vector3[MAX_POINT_COUNT];

        m_mesh = GetComponent<MeshFilter>().mesh;
        m_mesh.Clear();

        // Points used for finding floor plane.
        m_numPointsAtY = new Dictionary<float, int>();
        m_nonNoiseBuckets = new List<float>();

        m_renderer = GetComponent<Renderer>();
    }

    /// <summary>
    /// Unity callback when the component gets destroyed.
    /// </summary>
    public void OnDestroy()
    {
        m_tangoApplication.Unregister(this);
    }

    /// <summary>
    /// Callback that gets called when depth is available from the Tango Service.
    /// </summary>
    /// <param name="pointCloud">Depth information from Tango.</param>
    public void OnTangoPointCloudAvailable(TangoPointCloudData pointCloud)
    {
        m_mostRecentPointCloud = pointCloud;

        // Calculate the time since the last successful depth data
        // collection.
        if (m_depthTimestamp != 0.0)
        {
            m_depthDeltaTime = (float)((pointCloud.m_timestamp - m_depthTimestamp) * 1000.0);
        }

        // Fill in the data to draw the point cloud.
        m_pointsCount = pointCloud.m_numPoints;
        if (m_pointsCount > 0)
        {
            _SetUpCameraData();

            DMatrix4x4 globalTLocal;
            bool globalTLocalSuccess = m_tangoApplication.GetGlobalTLocal(out globalTLocal);
            if (!globalTLocalSuccess)
            {
                return;
            }

            DMatrix4x4 unityWorldTGlobal = DMatrix4x4.FromMatrix4x4(TangoSupport.UNITY_WORLD_T_START_SERVICE) * globalTLocal.Inverse;

            TangoPoseData poseData;

            // Query pose to transform point cloud to world coordinates, here we are using the timestamp that we get from depth.
            bool poseSuccess = _GetDevicePose(pointCloud.m_timestamp, out poseData);
            if (!poseSuccess)
            {
                return;
            }

            DMatrix4x4 unityWorldTDevice = unityWorldTGlobal * DMatrix4x4.TR(poseData.translation, poseData.orientation);

            // The transformation matrix that represents the point cloud's pose. 
            // Explanation: 
            // The point cloud, which is in Depth camera's frame, is put in Unity world's 
            // coordinate system(wrt Unity world).
            // Then we are extracting the position and rotation from uwTuc matrix and applying it to 
            // the point cloud's transform.
            DMatrix4x4 unityWorldTDepthCamera = unityWorldTDevice * m_deviceTDepthCamera;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // Add offset to the point cloud depending on the offset from TangoDeltaPoseController.
            if (m_tangoDeltaPoseController != null)
            {
                m_mostRecentUnityWorldTDepthCamera = m_tangoDeltaPoseController.UnityWorldOffset * unityWorldTDepthCamera.ToMatrix4x4();
            }
            else
            {
                m_mostRecentUnityWorldTDepthCamera = unityWorldTDepthCamera.ToMatrix4x4();
            }

            // Converting points array to world space.
            m_overallZ = 0;
            for (int i = 0; i < m_pointsCount; ++i)
            {
                Vector3 point = pointCloud[i];
                m_points[i] = m_mostRecentUnityWorldTDepthCamera.MultiplyPoint3x4(point);
                m_overallZ += point.z;
            }

            m_overallZ = m_overallZ / m_pointsCount;
            m_depthTimestamp = pointCloud.m_timestamp;

            if (m_updatePointsMesh)
            {
                // Need to update indices too!
                int[] indices = new int[m_pointsCount];
                for (int i = 0; i < m_pointsCount; ++i)
                {
                    indices[i] = i;
                }

                m_mesh.Clear();
                m_mesh.vertices = m_points;
                m_mesh.SetIndices(indices, MeshTopology.Points, 0);
            }

            // The color should be pose relative; we need to store enough info to go back to pose values.
            m_renderer.material.SetMatrix("depthCameraTUnityWorld", m_mostRecentUnityWorldTDepthCamera.inverse);

            // Try to find the floor using this set of depth points if requested.
            if (m_findFloorWithDepth)
            {
                _FindFloorWithDepth();
            }
        }
        else
        {
            m_overallZ = 0;
        }
    }

    /// @endcond
    /// <summary>
    /// Finds the closest point from a point cloud to a position on screen.
    /// 
    /// This function is slow, as it looks at every single point in the point
    /// cloud. Avoid calling this more than once a frame.
    /// </summary>
    /// <returns>The index of the closest point, or -1 if not found.</returns>
    /// <param name="cam">The current camera.</param>
    /// <param name="pos">Position on screen (in pixels).</param>
    /// <param name="maxDist">The maximum pixel distance to allow.</param>
    public int FindClosestPoint(Camera cam, Vector2 pos, int maxDist)
    {
        int bestIndex = -1;
        float bestDistSqr = 0;

        for (int it = 0; it < m_pointsCount; ++it)
        {
            Vector3 screenPos3 = cam.WorldToScreenPoint(m_points[it]);
            Vector2 screenPos = new Vector2(screenPos3.x, screenPos3.y);

            float distSqr = Vector2.SqrMagnitude(screenPos - pos);
            if (distSqr > maxDist * maxDist)
            {
                continue;
            }

            if (bestIndex == -1 || distSqr < bestDistSqr)
            {
                bestIndex = it;
                bestDistSqr = distSqr;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Estimates the depth of a point on a screen, based on nearest neighbors.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a successful depth estimate was obtained.
    /// </returns>
    /// <param name="cam">The Unity camera.</param>
    /// <param name="pos">The point in pixel coordinates to perform depth estimation.</param>
    /// <param name="colorCameraPoint">
    /// The point (x, y, z), where (x, y) is the back-projection of the UV
    /// coordinates to the color camera space and z is the z coordinate of
    /// the point in the point cloud nearest to the user selection after
    /// projection onto the image plane. If there is not a point cloud point
    /// close to the user selection after projection onto the image plane,
    /// then the point will be set to (0.0, 0.0, 0.0) and isValidPoint will
    /// be set to false.
    /// </param>
    public bool EstimateDepthOnScreen(Camera cam, Vector2 pos, out Vector3 colorCameraPoint)
    {
        // Set up parameters
        Matrix4x4 colorCameraTUnityWorld = TangoSupport.COLOR_CAMERA_T_UNITY_CAMERA * cam.transform.worldToLocalMatrix;
        Vector2 normalizedPos = cam.ScreenToViewportPoint(pos);

        // If the camera has a TangoARScreen attached, it is not displaying the entire color camera image.  Correct
        // the normalized coordinates by taking the clipping into account.
        TangoARScreen arScreen = cam.gameObject.GetComponent<TangoARScreen>();
        if (arScreen != null)
        {
            normalizedPos = arScreen.ViewportPointToCameraImagePoint(normalizedPos);
        }

        bool returnValue = TangoSupport.ScreenCoordinateToWorldNearestNeighbor(
            m_mostRecentPointCloud,
            arScreen.m_screenUpdateTime,
            normalizedPos,
            out colorCameraPoint);

        return returnValue;
    }

    /// <summary>
    /// Given a screen coordinate, finds a plane that most closely fits the
    /// depth values in that area.
    /// 
    /// This function is slow, as it looks at every single point in the point
    /// cloud. Avoid calling this more than once a frame. This also assumes the
    /// Unity camera intrinsics match the device's color camera.
    /// </summary>
    /// <returns><c>true</c>, if a plane was found; <c>false</c> otherwise.</returns>
    /// <param name="cam">The Unity camera.</param>
    /// <param name="pos">The point in screen space to perform detection on.</param>
    /// <param name="planeCenter">Filled in with the center of the plane in Unity world space.</param>
    /// <param name="plane">Filled in with a model of the plane in Unity world space.</param>
    public bool FindPlane(Camera cam, Vector2 pos, out Vector3 planeCenter, out Plane plane)
    {
        if (m_pointsCount == 0)
        {
            // No points to check, maybe not connected to the service yet
            planeCenter = Vector3.zero;
            plane = new Plane();
            return false;
        }

        Vector2 normalizedPos = cam.ScreenToViewportPoint(pos);

        // If the camera has a TangoARScreen attached, it is not displaying the entire color camera image.  Correct
        // the normalized coordinates by taking the clipping into account.
        TangoARScreen arScreen = cam.gameObject.GetComponent<TangoARScreen>();
        if (arScreen != null)
        {
            normalizedPos = arScreen.ViewportPointToCameraImagePoint(normalizedPos);
        }

        DVector4 planeModel = new DVector4();

        bool returnValue = TangoSupport.FitPlaneModelNearClick(
            m_mostRecentPointCloud,
            arScreen.m_screenUpdateTime,
            normalizedPos,
            out planeCenter,
            out planeModel);

        planeCenter = m_mostRecentUnityWorldTDepthCamera.MultiplyPoint3x4(planeCenter);
        Vector3 normal = new Vector3((float)planeModel.x,
                                     (float)planeModel.y,
                                     (float)planeModel.z);

        normal = m_mostRecentUnityWorldTDepthCamera.MultiplyVector(normal);
        Vector3.Normalize(normal);
        float distance = (float)planeModel.w / normal.magnitude;

        plane = new Plane(normal, distance);

        return returnValue;
    }

    private bool CreateMaterial(string shaderName, out Material material)
    {
        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            // If can't find shader, use a material that will output (0, 0, 0, 0)
            material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.color = Color.clear;
            return false;
        }
        else
        {
            material = new Material(shader);
            return true;
        }
    }
    
    /// <summary>
    /// Minji Kim
    /// from VideoOverlayListener
    /// get tango unity image data
    /// </summary>
    /// <param name="colorImageData"></param>
    private void GetTangoUnityImageData(ref TangoUnityImageData colorImageData)
    {
        const int EMULATED_CAMERA_WIDTH = 1280;
        const int EMULATED_CAMERA_HEIGHT = 720;
        const int EMULATED_CAMERA_PACKED_WIDTH = 1280 / 4;
        const int EMULATED_CAMERA_PACKED_Y_HEIGHT = 720;
        const int EMULATED_CAMERA_PACKED_UV_HEIGHT = 720 / 2;
        const string EMULATED_RGB2YUV_Y_SHADERNAME = "Hidden/Tango/RGB2YUV_Y";
        const string EMULATED_RGB2YUV_CBCR_SHADERNAME = "Hidden/Tango/RGB2YUV_CbCr";
        Material m_yuvFilterY = null;
        Material m_yuvFilterCbCr = null;
        RenderTexture m_emulatedColorRenderTexture = null;
        Texture2D[] m_emulationByteBufferCaptureTextures = null;
        float m_lastColorEmulationTime = ((float)DateTime.Now.Hour + ((float)DateTime.Now.Minute * 0.01f));
        CreateMaterial(EMULATED_RGB2YUV_Y_SHADERNAME, out m_yuvFilterY);
        CreateMaterial(EMULATED_RGB2YUV_CBCR_SHADERNAME, out m_yuvFilterCbCr);
        m_emulatedColorRenderTexture = new RenderTexture(EMULATED_CAMERA_WIDTH, EMULATED_CAMERA_HEIGHT, 24,
                                                             RenderTextureFormat.ARGB32);
        m_emulationByteBufferCaptureTextures = new Texture2D[2];
        m_emulationByteBufferCaptureTextures[0] = new Texture2D(EMULATED_CAMERA_PACKED_WIDTH,
                                                                EMULATED_CAMERA_PACKED_Y_HEIGHT,
                                                                TextureFormat.ARGB32, false);
        m_emulationByteBufferCaptureTextures[1] = new Texture2D(EMULATED_CAMERA_PACKED_WIDTH,
                                                                EMULATED_CAMERA_PACKED_UV_HEIGHT,
                                                                TextureFormat.ARGB32, false);

        int yDataSize = EMULATED_CAMERA_WIDTH * EMULATED_CAMERA_HEIGHT;
        int cbCrDataSize = yDataSize / 2;
        int dataSize = yDataSize + cbCrDataSize;
        if (colorImageData.data == null || colorImageData.data.Length != dataSize)
        {
            colorImageData.data = new byte[dataSize];
        }

        RenderTexture yRT = RenderTexture.GetTemporary(EMULATED_CAMERA_PACKED_WIDTH,
                                                       EMULATED_CAMERA_PACKED_Y_HEIGHT,
                                                       0, RenderTextureFormat.ARGB32);
        RenderTexture cbCrRT = RenderTexture.GetTemporary(EMULATED_CAMERA_PACKED_WIDTH,
                                                          EMULATED_CAMERA_PACKED_UV_HEIGHT,
                                                          0, RenderTextureFormat.ARGB32);
        Graphics.Blit(m_emulatedColorRenderTexture, yRT, m_yuvFilterY);
        m_emulationByteBufferCaptureTextures[0].ReadPixels(new Rect(0, 0, yRT.width, yRT.height), 0, 0);
        Graphics.Blit(m_emulatedColorRenderTexture, cbCrRT, m_yuvFilterCbCr);
        m_emulationByteBufferCaptureTextures[1].ReadPixels(new Rect(0, 0, cbCrRT.width, cbCrRT.height), 0, 0);

        Color32[] colors = m_emulationByteBufferCaptureTextures[0].GetPixels32();
        for (int i = 0; i < yDataSize / 4; i++)
        {
            colorImageData.data[(i * 4)] = colors[i].r;
            colorImageData.data[(i * 4) + 1] = colors[i].g;
            colorImageData.data[(i * 4) + 2] = colors[i].b;
            colorImageData.data[(i * 4) + 3] = colors[i].a;
        }

        int startOffset = colors.Length * 4;
        colors = m_emulationByteBufferCaptureTextures[1].GetPixels32();
        for (int i = 0; i < cbCrDataSize / 4; i++)
        {
            colorImageData.data[(i * 4) + startOffset] = colors[i].r;
            colorImageData.data[(i * 4) + startOffset + 1] = colors[i].g;
            colorImageData.data[(i * 4) + startOffset + 2] = colors[i].b;
            colorImageData.data[(i * 4) + startOffset + 3] = colors[i].a;
        }

        RenderTexture.ReleaseTemporary(yRT);
        RenderTexture.ReleaseTemporary(cbCrRT);

        colorImageData.format = TangoEnums.TangoImageFormatType.TANGO_HAL_PIXEL_FORMAT_YV12;
        colorImageData.width = EMULATED_CAMERA_WIDTH;
        colorImageData.height = EMULATED_CAMERA_HEIGHT;
        colorImageData.stride = EMULATED_CAMERA_WIDTH;
        colorImageData.timestamp = m_lastColorEmulationTime;
    }
    
    /// <summary>
    /// Minji Kim 2017.09.25
    /// Find Edge Wrapper
    /// call FindEdgesNearPoint
    /// TangoSupport_findEdgesNearPoint
    /// </summary>
    /// <param name="cam"></param>
    /// <param name="pos"></param>
    /// <returns></returns>
    public bool FindEdges(TangoUnityImageData imageBuffer, 
        Camera cam, Vector2 pos, out TangoSupport.TangoSupportEdge[] edges, out int num_edges)
        //out Vector3[] end_points, out Vector3 nearest_on_edge)
    {
        if (m_pointsCount == 0)
        {
            // No points to check, maybe not connected to the service yet
            edges = new TangoSupport.TangoSupportEdge[1];
            num_edges = 0;
            return false;
        }

        Vector2 normalizedPos = cam.ScreenToViewportPoint(pos);

        // If the camera has a TangoARScreen attached, it is not displaying the entire color camera image.  Correct
        // the normalized coordinates by taking the clipping into account.
        TangoARScreen arScreen = cam.gameObject.GetComponent<TangoARScreen>();
        if (arScreen != null)
        {
            normalizedPos = arScreen.ViewportPointToCameraImagePoint(normalizedPos);
        }

        // if the image data has not been updated, update it manually
        if (imageBuffer.data == null)
        {
            GetTangoUnityImageData(ref imageBuffer);
        }

        bool returnValue = TangoSupport.FindEdgesNearPoint(
            imageBuffer,
            m_mostRecentPointCloud,
            arScreen.m_screenUpdateTime,
            normalizedPos,
            out edges,
            out num_edges
            );

        Vector3[] start = new Vector3[num_edges];
        Vector3[] end = new Vector3[num_edges];
        Vector3[] near = new Vector3[num_edges];
        TangoSupport.TangoSupportEdge[] n_edges = new TangoSupport.TangoSupportEdge[num_edges];
        for(int j = 0; j < num_edges; j++)
        {
            start[j] = new Vector3(edges[j].end_points_x1, edges[j].end_points_y1, edges[j].end_points_z1);
            end[j] = new Vector3(edges[j].end_points_x2, edges[j].end_points_y2, edges[j].end_points_z2);
            near[j] = new Vector3(edges[j].closest_point_on_edge_x, edges[j].closest_point_on_edge_y, 
                edges[j].closest_point_on_edge_z);
            start[j] = m_mostRecentUnityWorldTDepthCamera.MultiplyVector(start[j]);
            end[j] = m_mostRecentUnityWorldTDepthCamera.MultiplyVector(end[j]);
            near[j] = m_mostRecentUnityWorldTDepthCamera.MultiplyVector(near[j]);
            Vector3.Normalize(start[j]);
            Vector3.Normalize(end[j]);
            Vector3.Normalize(near[j]);
            n_edges[j].end_points_x1 = start[j][0];
            n_edges[j].end_points_y1 = start[j][1];
            n_edges[j].end_points_z1 = start[j][2];
            n_edges[j].end_points_x2 = end[j][0];
            n_edges[j].end_points_y2 = end[j][1];
            n_edges[j].end_points_z2 = end[j][2];
            n_edges[j].closest_point_on_edge_x = near[j][0];
            n_edges[j].closest_point_on_edge_y = near[j][1];
            n_edges[j].closest_point_on_edge_z = near[j][2];
        }
        edges = n_edges;

        return returnValue;
    }

    /// <summary>
    /// Start processing the point cloud depth points to find the position of the floor.
    /// </summary>
    public void FindFloor()
    {
        m_floorFound = false;
        m_findFloorWithDepth = true;
        m_floorPlaneY = 0.0f;
    }

    /// <summary>
    /// Get the device pose.
    /// </summary>
    /// <returns><c>true</c>, if the device pose is valid, <c>false</c> otherwise.</returns>
    /// <param name="timestamp">The time stamp.</param>
    /// <param name="poseData">The pose data returned from the function.</param>
    private bool _GetDevicePose(double timestamp, out TangoPoseData poseData)
    {
        TangoCoordinateFramePair pair;
        poseData = new TangoPoseData();
        
        pair.targetFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_DEVICE;
        if (m_useAreaDescriptionPose)
        {
            if (m_tangoApplication.m_enableCloudADF)
            {
                pair.baseFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_GLOBAL_WGS84;
            }
            else
            {
                pair.baseFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_AREA_DESCRIPTION;
            }
        }
        else
        {
            pair.baseFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_START_OF_SERVICE;
        }

        PoseProvider.GetPoseAtTime(poseData, timestamp, pair);
        if (poseData.status_code != TangoEnums.TangoPoseStatusType.TANGO_POSE_VALID)
        {
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Sets up extrinsic matrixes and camera intrinsics for this hardware.
    /// </summary>
    private void _SetUpCameraData()
    {
        if (m_cameraDataSetUp)
        {
            return;
        }

        double timestamp = 0.0;
        TangoCoordinateFramePair pair;
        TangoPoseData poseData = new TangoPoseData();

        // Query the extrinsics between IMU and device frame.
        pair.baseFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_IMU;
        pair.targetFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_DEVICE;
        PoseProvider.GetPoseAtTime(poseData, timestamp, pair);
        m_imuTDevice = DMatrix4x4.FromMatrix4x4(poseData.ToMatrix4x4());

        // Query the extrinsics between IMU and depth camera frame.
        pair.baseFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_IMU;
        pair.targetFrame = TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_CAMERA_DEPTH;
        PoseProvider.GetPoseAtTime(poseData, timestamp, pair);
        m_imuTDepthCamera = DMatrix4x4.FromMatrix4x4(poseData.ToMatrix4x4());

        m_deviceTDepthCamera = m_imuTDevice.Inverse * m_imuTDepthCamera;

        // Also get the camera intrinsics
        m_colorCameraIntrinsics = new TangoCameraIntrinsics();
        VideoOverlayProvider.GetIntrinsics(TangoEnums.TangoCameraId.TANGO_CAMERA_COLOR, m_colorCameraIntrinsics);

        m_cameraDataSetUp = true;
    }

    /// <summary>
    /// Use the last received set of depth points to find a reasonable floor.
    /// </summary>
    private void _FindFloorWithDepth()
    {
        m_numPointsAtY.Clear();
        m_nonNoiseBuckets.Clear();

        // Count each depth point into a bucket based on its world position y value.
        for (int i = 0; i < m_pointsCount; i++)
        {
            Vector3 point = m_points[i];
            if (!point.Equals(Vector3.zero))
            {
                // Group similar points into buckets based on sensitivity. 
                float roundedY = Mathf.Round(point.y / SENSITIVITY) * SENSITIVITY;
                if (!m_numPointsAtY.ContainsKey(roundedY))
                {
                    m_numPointsAtY.Add(roundedY, 0);
                }

                m_numPointsAtY[roundedY]++;

                // Check if the y plane is a non-noise plane.
                if (m_numPointsAtY[roundedY] > NOISE_THRESHOLD && !m_nonNoiseBuckets.Contains(roundedY))
                {
                    m_nonNoiseBuckets.Add(roundedY);
                }
            }
        }

        // Find a plane at the y value. The y value must be below the camera y position.
        m_nonNoiseBuckets.Sort();
        for (int i = 0; i < m_nonNoiseBuckets.Count; i++)
        {
            float yBucket = m_nonNoiseBuckets[i];
            int numPoints = m_numPointsAtY[yBucket];
            if (numPoints > RECOGNITION_THRESHOLD && yBucket < Camera.main.transform.position.y)
            {
                // Reject the plane if it is not the lowest.
                if (yBucket > m_nonNoiseBuckets[0])
                {
                    return;
                }

                m_floorFound = true;
                m_findFloorWithDepth = false;
                m_floorPlaneY = yBucket;
                m_numPointsAtY.Clear();
                m_nonNoiseBuckets.Clear();
            }
        }
    }
}
