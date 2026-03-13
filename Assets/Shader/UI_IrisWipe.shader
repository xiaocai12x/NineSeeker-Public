Shader "UI/IrisWipe"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,1)
        _Center ("Center (UV)", Vector) = (0.5, 0.5, 0, 0) 
        _Radius ("Radius", Range(0, 1.5)) = 0.5            
        _Smoothness ("Smoothness", Range(0, 0.1)) = 0.02   
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            float4 _Color;
            float4 _Center;
            float _Radius;
            float _Smoothness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                
                float2 uv = i.uv;
                uv.x *= aspect;
                
                float2 center = _Center.xy;
                center.x *= aspect;

                float dist = distance(uv, center);

                float alpha = smoothstep(_Radius - _Smoothness, _Radius, dist);

                return fixed4(0, 0, 0, alpha * _Color.a); 
            }
            ENDCG
        }
    }
}