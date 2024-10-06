Shader "Kinect4Azure/PointCloud"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderType" = "Geometry" }
        ZWrite On
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            uint _DepthWidth;
            uint _DepthHeight;
            uint _ColorWidth;
            uint _ColorHeight;
            float _MaxPointDistance;

            float4x4 _PointcloudOrigin;
            float4x4 _Col2DepCalibration;

            Texture2D<float> _DepthTex;
            Texture2D<float4> _ColorTex;
            Texture2D<float4> _XYLookup;

            #pragma shader_feature _ORIGINALPC_ON __

            // Duplicated Reality
            #pragma shader_feature _DUPLICATE_ON __
            float _ROI_Scale = 1;
            half4x4 _ROI_Inversed;
            half4x4 _Dupl_Inversed;
            half4x4 _Roi2Dupl;

            SamplerState sampler_ColorTex
            {
                Filter = MIN_MAG_MIP_LINEAR;
                AddressU = Wrap;
                AddressV = Wrap;
            };

            struct appdata
            {
                uint vid : SV_VertexID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;

                uint index = v.vid;

                uint x = index % _DepthWidth;
                uint y = index / _DepthWidth;

                if(x == _DepthWidth - 1 || y == _DepthHeight - 1) return o; // Skip boundary points

                float3 texel = float3(x, y, 0);

                float depth = _DepthTex.Load(texel) * 65536;
                if (depth < 1) return o; // Skip invalid points

                float4 xy = _XYLookup.Load(texel) * 2 - 1;

                float4x4 OxC = mul(_PointcloudOrigin, _Col2DepCalibration);
                float4 posWorld = mul(OxC, float4(float3(xy.x, -xy.y, 1) * depth * 0.001f, 1.0f));

                // Convert world position to clip space
                o.pos = UnityObjectToClipPos(posWorld);

                // Compute UV coordinates for texture sampling
                o.uv = float2(x / _DepthWidth, y / _DepthHeight);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(1.0, 1.0, 1.0, 1.0);
            }
            ENDCG
        }
    }
}