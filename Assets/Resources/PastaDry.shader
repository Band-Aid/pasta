Shader "Pasta/Dry Semolina"
{
    Properties { _BaseColor ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv; o.color = input.color;
                return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                Light light = GetMainLight();
                float3 view = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                float diffuse = saturate(dot(n, light.direction)) * 0.48 + 0.54;
                float spec = pow(saturate(dot(n, normalize(light.direction + view))), 40) * 0.2;
                float grain = frac(sin(dot(floor(i.uv * float2(650, 100)), float2(12.9898,78.233))) * 43758.5453);
                float ridges = sin(i.uv.y * 180) * 0.025;
                half3 dry = i.color.rgb * (0.95 + grain * 0.08 + ridges);
                return half4(dry * diffuse * lerp(half3(1,1,1), light.color, 0.35) + spec * half3(1,0.88,0.6), 1);
            }
            ENDHLSL
        }
    }
}
