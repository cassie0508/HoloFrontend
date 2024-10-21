Shader "PBM/ViewMerge"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite On ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            sampler2D _MainTex; // Virtual Content
            sampler2D _RealContentTex;            
            float4 _RealContentTex_ST;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }            

            fixed4 frag(v2f i) : SV_Target
            {

                float2 realContent_uv = TRANSFORM_TEX(i.uv, _RealContentTex);
                fixed4 realContentcol = tex2D(_RealContentTex, realContent_uv);
                fixed4 virtualContentcol = tex2D(_MainTex, i.uv);

                if(virtualContentcol.a > 0) {
                    return float4(virtualContentcol.rgb , 1);
                }
                else {
                    return float4(realContentcol.rgb, 1); ;
                }
                
            }
            ENDCG
        }
    }
}