// URP port of "Oculus Sample/Alpha Hand Outline (SBB)" from the Meta XR Core SDK
// Building Blocks. The original is a built-in-pipeline CGPROGRAM shader; under
// URP + Single Pass Instanced (Quest Link) it renders into the left eye only.
// This port uses URP HLSL with full stereo-instancing support so hands render
// in both eyes. Visual logic is unchanged.
Shader "QuestBase/Alpha Hand Outline (URP)"
{
    Properties
    {
        _ColorPrimary ("Color Primary", Color) = (0.396078, 0.725490, 1)
        _ColorTop ("Color Top", Color) = (0.031896, 0.0343398, 0.0368894)
        _ColorBottom ("Color Bottom", Color) = (0.0137021, 0.0144438, 0.0152085)
        _RimFactor ("Rim Factor", Range(0.01, 1.0)) = 0.65
        _FresnelPower ("Fresnel Power", Range(0.01,1.0)) = 0.16

        _HandAlpha ("Hand Alpha", Range(0, 1)) = 1.0
        _MinVisibleAlpha ("Minimum Visible Alpha", Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half3 _ColorPrimary;
        half3 _ColorTop;
        half3 _ColorBottom;
        float _RimFactor;
        float _FresnelPower;
        float _HandAlpha;
        float _MinVisibleAlpha;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 lightDir : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings HandVert (Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.lightDir = GetMainLight().direction;
            return o;
        }

        half4 HandFrag (Varyings i) : SV_Target
        {
            float3 normalDir = normalize(i.normalWS);
            float3 lightDir = normalize(i.lightDir);
            half lightDotNormal = saturate(dot(lightDir, normalDir));

            half rim = pow(1.0 - lightDotNormal, 0.5) * (1.0 - _RimFactor) + _RimFactor;
            rim = saturate(rim);

            half3 emission = lerp(half3(0, 0, 0), _ColorPrimary, rim);
            emission += rim * 0.5;
            emission *= 0.95;

            float fresnel = saturate(pow(1.0 - lightDotNormal, _FresnelPower));
            half3 color = lerp(_ColorTop, _ColorBottom, fresnel);
            color *= emission;

            half alphaValue = step(_MinVisibleAlpha, _HandAlpha) * _HandAlpha;

            return half4(color, alphaValue);
        }

        half4 DepthOnlyFrag (Varyings i) : SV_Target
        {
            return 0;
        }
        ENDHLSL

        // write depth to see the top-most layer
        Pass
        {
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex HandVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        // shading pass
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, Zero One

            HLSLPROGRAM
            #pragma vertex HandVert
            #pragma fragment HandFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
