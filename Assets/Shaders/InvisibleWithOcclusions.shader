Shader "Lightship/InvisibleWithOcclusions"
{
    SubShader
    {
        Tags {"Queue"="Background+2" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
        Pass
        {
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local

            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(0, 0, 0, 0);
            }

            ENDCG
        }
    }
}
