using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Tango;
using UnityEngine;

public class FindEdgeTest : MonoBehaviour, ITangoVideoOverlay {
    private Texture2D m_texture;
    public float th = 0.09f;
    private Texture2D img, rImg, gImg, bImg, kImg, roImg, goImg, boImg, koImg, ORimg, 
              ANDimg, SUMimg, AVGimg;
    private float[,] rL, gL, bL, kL, ORL, ANDL, SUML, AVGL;
    private Color temp;
    private float t;
    private TangoApplication m_tangoApplication;
    // texture data
    bool isDirty = false;
    byte[] m_yuv12 = null;
    int m_width;
    int m_height;
    Camera m_cam;

    void ITangoVideoOverlay.OnTangoImageAvailableEventHandler(TangoEnums.TangoCameraId cameraId,
                                                              TangoUnityImageData imageBuffer)
    {
        byte[] yuv = imageBuffer.data;
        int m_height = Convert.ToInt32(imageBuffer.width);
        int m_width = Convert.ToInt32(imageBuffer.height);
        YV12ToPhoto(yuv, m_width, m_height, out m_texture);
        /*
        int ii = 0;
        int ij = 0;
        int di = +1;
        int dj = +1;
        try
        {
            int a = 0;
            for (int i = 0, ci = ii; i < imageBuffer.height; ++i, ci += di)
            {
                for (int j = 0, cj = ij; j < imageBuffer.width; ++j, cj += dj)
                {
                    int y = (0xff & ((int)yuv[ci * imageBuffer.width + cj]));
                    int u = (0xff & ((int)yuv[frameSize + (ci >> 1) * imageBuffer.width + (cj & ~1) + 0]));
                    int v = (0xff & ((int)yuv[frameSize + (ci >> 1) * imageBuffer.width + (cj & ~1) + 1]));
                    y = y < 16 ? 16 : y;

                    int b = (int)(1.164f * (y - 16) + 1.596f * (v - 128));
                    int g = (int)(1.164f * (y - 16) - 0.813f * (v - 128) - 0.391f * (u - 128));
                    int r = (int)(1.164f * (y - 16) + 2.018f * (u - 128));

                    r = r < 0 ? 0 : (r > 255 ? 255 : r);
                    g = g < 0 ? 0 : (g > 255 ? 255 : g);
                    b = b < 0 ? 0 : (b > 255 ? 255 : b);

                    argb[a++] = Convert.ToByte(Convert.ToInt32(0xff000000) | (r << 16) | (g << 8) | b);
                    //Color32[] argbArray = ColorHelper.YUV_NV21_TO_RGB(imageBuffer.data, 1920, 1080);
                }
            }
        } catch(System.IndexOutOfRangeException ie)
        {
            Debug.LogError("<<<<<<<<<<<< index out of bound!");
        } catch(NullReferenceException ne)
        {
            Debug.LogError("<<<<<<<<<<<< null reference!");
        }
        Color32[] colorArray = new Color32[argb.Length / 4];
        for (int i = 0; i < argb.Length; i += 4)
        {
            var color = new Color32(argb[i + 0], argb[i + 1], argb[i + 2], argb[i + 3]);
            colorArray[i / 4] = color;
        }
        m_texture.SetPixels32(colorArray);
        */
    }
    Color YCbCrtoRGB(byte y, byte cb, byte cr)
    {
        return new Color(
         y + 1.402f * cr,
         y - 0.344136f * cb - 0.714136f * cr,
         y + 1.772f * cb
        );
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
        img = new Texture2D(m_cam.pixelWidth, m_cam.pixelHeight);
        //initialize textures
        rImg = new Texture2D(img.width, img.height);
        bImg = new Texture2D(img.width, img.height);
        gImg = new Texture2D(img.width, img.height);
        kImg = new Texture2D(img.width, img.height);
        //initialize gradient arrays
        rL = new float[img.width, img.height];
        gL = new float[img.width, img.height];
        bL = new float[img.width, img.height];
        kL = new float[img.width, img.height];
        ORL = new float[img.width, img.height];
        ANDL = new float[img.width, img.height];
        SUML = new float[img.width, img.height];
        AVGL = new float[img.width, img.height];
        //initialize final textures
        roImg = new Texture2D(img.width, img.height);
        goImg = new Texture2D(img.width, img.height);
        boImg = new Texture2D(img.width, img.height);
        koImg = new Texture2D(img.width, img.height);
        ORimg = new Texture2D(img.width, img.height);
        ANDimg = new Texture2D(img.width, img.height);
        SUMimg = new Texture2D(img.width, img.height);
        AVGimg = new Texture2D(img.width, img.height);
    }
	
	// Update is called once per frame
	void Update () {
        CalculateEdges();
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

    /*
        // Update is called once per frame
        void OnGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label("original");
            GUILayout.Label(camTex);
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
    */

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

    public bool YV12ToPhoto(byte[] data, int width, int height, out Texture2D photo)
    {
        photo = new Texture2D(width, height);

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
                int yValue = data[(i * width) + j];
                int uValue = data[uv_buffer_offset + ((i / 2) * width) + x_index + 1];
                int vValue = data[uv_buffer_offset + ((i / 2) * width) + x_index];

                // Convert the YUV value to RGB.
                float r = yValue + (1.370705f * (vValue - 128));
                float g = yValue - (0.689001f * (vValue - 128)) - (0.337633f * (uValue - 128));
                float b = yValue + (1.732446f * (uValue - 128));

                Color co = new Color();
                co.b = b < 0 ? 0 : (b > 255 ? 1 : b / 255.0f);
                co.g = g < 0 ? 0 : (g > 255 ? 1 : g / 255.0f);
                co.r = r < 0 ? 0 : (r > 255 ? 1 : r / 255.0f);
                co.a = 1.0f;

                photo.SetPixel(width - j - 1, height - i - 1, co);
            }
        }

        return true;
    }

}
