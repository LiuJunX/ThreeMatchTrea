Shader "Match3/TileLit"
{
    Properties
    {
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
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        // ─── Pass 0: Forward Lit ───
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On

            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment LitFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Metallic;
                half  _Smoothness;
                half4 _EmissionColor;
                half4 _FresnelColor;
                half  _FresnelPower;
                half  _EdgeSoftness;
                float _ClipYMin;
                float _ClipYMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
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

                return o;
            }

            half4 LitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Y-axis clipping for hole portal effect
                clip(input.positionWS.y - _ClipYMin);
                clip(_ClipYMax - input.positionWS.y);

                // Surface data
                InputData inputData = (InputData)0;
                inputData.positionWS              = input.positionWS;
                inputData.positionCS              = input.positionCS;
                inputData.normalWS                = normalize(input.normalWS);
                inputData.viewDirectionWS         = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord             = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord                = InitializeInputDataFog(float4(input.positionWS, 1), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI                 = SampleSH(inputData.normalWS);

                // Fresnel edge softness
                half fresnel = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half edgeAlpha = smoothstep(0, _EdgeSoftness, fresnel);

                // Fresnel glow: bright rim when _FresnelColor is non-black
                half fresnelGlow = pow(1 - fresnel, _FresnelPower);
                half3 totalEmission = _EmissionColor.rgb + _FresnelColor.rgb * fresnelGlow;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = _BaseColor.rgb;
                surfaceData.metallic   = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS   = half3(0, 0, 1);
                surfaceData.emission   = totalEmission;
                surfaceData.occlusion  = 1;
                surfaceData.alpha      = edgeAlpha;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = edgeAlpha;

                return color;
            }
            ENDHLSL
        }

        // ─── Pass 1: Shadow Caster (stays opaque) ───
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Metallic;
                half  _Smoothness;
                half4 _EmissionColor;
                half4 _FresnelColor;
                half  _FresnelPower;
                half  _EdgeSoftness;
                float _ClipYMin;
                float _ClipYMax;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                float3 posWS   = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));
                o.positionWS = posWS;

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                clip(input.positionWS.y - _ClipYMin);
                clip(_ClipYMax - input.positionWS.y);
                return 0;
            }
            ENDHLSL
        }

        // ─── Pass 2: Depth Only ───
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Metallic;
                half  _Smoothness;
                half4 _EmissionColor;
                half4 _FresnelColor;
                half  _FresnelPower;
                half  _EdgeSoftness;
                float _ClipYMin;
                float _ClipYMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                clip(input.positionWS.y - _ClipYMin);
                clip(_ClipYMax - input.positionWS.y);
                return 0;
            }
            ENDHLSL
        }

        // ─── Pass 3: Depth Normals ───
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Metallic;
                half  _Smoothness;
                half4 _EmissionColor;
                half4 _FresnelColor;
                half  _FresnelPower;
                half  _EdgeSoftness;
                float _ClipYMin;
                float _ClipYMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                clip(input.positionWS.y - _ClipYMin);
                clip(_ClipYMax - input.positionWS.y);
                half3 nWS = NormalizeNormalPerPixel(input.normalWS);
                float2 octNormal = PackNormalOctQuadEncode(nWS);
                return half4(octNormal, 0, 0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
