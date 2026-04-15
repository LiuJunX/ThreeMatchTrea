Shader "Match3/TileLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.05
        _Smoothness ("Smoothness", Range(0,1)) = 0.62
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        [HDR] _FresnelColor ("Fresnel Glow", Color) = (0,0,0,0)
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 3
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.15
        _ClipYMin ("Clip Y Min", Float) = -9999
        _ClipYMax ("Clip Y Max", Float) =  9999
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ─── Pass 0: Forward Lit ───
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment LitFrag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Shared across all tiles (same for every instance)
            CBUFFER_START(UnityPerMaterial)
                half  _Metallic;
                half  _Smoothness;
                half  _EdgeSoftness;
                float4 _BaseMap_ST;
            CBUFFER_END

            // Per-instance properties — enables GPU Instancing batching.
            // Color, emission, fresnel, and clip bounds vary per tile.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FresnelColor)
                UNITY_DEFINE_INSTANCED_PROP(half,  _FresnelPower)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipYMin)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipYMax)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings LitVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(input.normalOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS   = normInputs.normalWS;
                o.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                o.uv         = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                return o;
            }

            half4 LitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Y-axis clipping for hole portal effect
                clip(input.positionWS.y - UNITY_ACCESS_INSTANCED_PROP(Props, _ClipYMin));
                clip(UNITY_ACCESS_INSTANCED_PROP(Props, _ClipYMax) - input.positionWS.y);

                // Surface data
                InputData inputData = (InputData)0;
                inputData.positionWS              = input.positionWS;
                inputData.positionCS              = input.positionCS;
                inputData.normalWS                = normalize(input.normalWS);
                inputData.viewDirectionWS         = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord             = float4(0, 0, 0, 0); // No realtime shadows — blob shadows only
                inputData.fogCoord                = InitializeInputDataFog(float4(input.positionWS, 1), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI                 = SampleSH(inputData.normalWS);

                // Read per-instance properties
                half4 baseColor    = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                half4 emissionCol  = UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor);
                half4 fresnelCol   = UNITY_ACCESS_INSTANCED_PROP(Props, _FresnelColor);
                half  fresnelPow   = UNITY_ACCESS_INSTANCED_PROP(Props, _FresnelPower);

                // Fresnel edge test: clip extreme silhouette pixels instead of blending
                half fresnel = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half edgeAlpha = smoothstep(0, _EdgeSoftness, fresnel);
                clip(edgeAlpha * baseColor.a - 0.01);

                // Fresnel glow: bright rim when _FresnelColor is non-black
                half fresnelGlow = pow(1 - fresnel, fresnelPow);
                half3 totalEmission = emissionCol.rgb + fresnelCol.rgb * fresnelGlow;

                // Sample base map (defaults to white 1x1 if no texture assigned → no visual change)
                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = baseColor.rgb * texCol.rgb;
                surfaceData.metallic   = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS   = half3(0, 0, 1);
                surfaceData.emission   = totalEmission;
                surfaceData.occlusion  = 1;
                surfaceData.alpha      = 1;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;

                return color;
            }
            ENDHLSL
        }

        // ShadowCaster pass removed — blob shadows only, no realtime shadow map

        // ─── Pass 1: Depth Only ───
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half  _Metallic;
                half  _Smoothness;
                half  _EdgeSoftness;
                float4 _BaseMap_ST;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FresnelColor)
                UNITY_DEFINE_INSTANCED_PROP(half,  _FresnelPower)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipYMin)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipYMax)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(input.positionWS.y - UNITY_ACCESS_INSTANCED_PROP(Props, _ClipYMin));
                clip(UNITY_ACCESS_INSTANCED_PROP(Props, _ClipYMax) - input.positionWS.y);
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals pass removed — not needed without SSAO
        // Glow pass removed — emission in ForwardLit handles highlight effects
    }

    Fallback Off
}
