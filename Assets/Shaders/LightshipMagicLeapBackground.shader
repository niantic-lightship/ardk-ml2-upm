Shader "Unlit/LightshipMagicLeapBackground"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _EnvironmentDepth("Depth Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Name "AR Camera Depth Background (Lightship)"
            Cull Off
            ZWrite On
            ZTest Always
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "Always"
            }

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local __ MAGICLEAP_ENVIRONMENT_DEPTH_ENABLED
            #pragma multi_compile_local __ CAMERA_DEBUG

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 texcoord : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct fragment_output
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            float _UnityCameraForwardScale;

            float4x4 _leftDisplayTransform;
            float4x4 _rightDisplayTransform;

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Transform the position from object space to clip space.
                o.position = UnityObjectToClipPos(v.vertex);

                float4x4 displayMatrix = (unity_StereoEyeIndex) ? _rightDisplayTransform : _leftDisplayTransform;
                o.texcoord = mul(float3(v.uv, 1.0f), displayMatrix).xy;
                return o;
            }

#if CAMERA_DEBUG
            sampler2D _MainTex;
#endif

#if MAGICLEAP_ENVIRONMENT_DEPTH_ENABLED
            sampler2D_float _EnvironmentDepth;
#endif // MAGICLEAP_ENVIRONMENT_DEPTH_ENABLED

            inline float ConvertDistanceToDepth(float d)
            {
                // Account for scale
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;

                // Clip any distances smaller than the near clip plane, and compute the depth value from the distance.
                return (d < _ProjectionParams.y) ? 0.0f : ((1.0f / _ZBufferParams.z) * ((1.0f / d) - _ZBufferParams.w));
            }

            fragment_output frag(v2f i)
            {
                // Sample color
                float4 color;
                float alpha = 0.0f;
                float depth = 0.0f;

#if MAGICLEAP_ENVIRONMENT_DEPTH_ENABLED
                // Sample the environment depth (in meters).
                float envDistance = tex2D(_EnvironmentDepth, i.texcoord).x;
                depth = ConvertDistanceToDepth(envDistance);
#endif // MAGICLEAP_ENVIRONMENT_DEPTH_ENABLED

                fragment_output o;
#if CAMERA_DEBUG
                color = tex2D(_MainTex, i.texcoord);

                // Make the background transparent if the texcoord is outside the range [0, 1].
                const float in_bounds_x = step(0.0f, i.texcoord.x) * step(i.texcoord.x, 1.0f);
                const float in_bounds_y = step(0.0f, i.texcoord.y) * step(i.texcoord.y, 1.0f);

                alpha = 0.1f * in_bounds_x * in_bounds_y;
#endif

                o.color = half4(color.r, color.g, color.b, alpha);

                o.depth = depth;
                return o;
            }
            ENDCG
        }
    }
}
