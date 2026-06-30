Shader "Custom/MissionOverlay"
{
    Properties
    {
        _MissionTex  ("Mission Texture", 2D)   = "black" {}
        _TexSize     ("Tex Size",       Vector) = (1,1,0,0)

        [Header(Zone Colors)]
        _ExtinguishColor  ("Extinguish Color",  Color) = (0.4, 0.0, 1.0, 0.6)
        _ContainmentColor ("Containment Color", Color) = (1.0, 0.2, 1.0, 0.6)
        _CompletedColor   ("Completed Color",   Color) = (0.0, 1.0, 0.3, 0.6)
        _FailedColor      ("Failed Color",      Color) = (1.0, 0.0, 0.0, 0.6)

        [Header(Transition Settings)]
        _TransitionSteps  ("Transition Steps",  Range(1,10))   = 3.0
        _StepEdgeSoftness ("Step Edge Softness",Range(0.01,1)) = 0.5
        _FillAlpha        ("Fill Alpha",        Range(0,1))    = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent+20"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MissionTex);
            SAMPLER(sampler_MissionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MissionTex_ST;
                float4 _TexSize;
                half4  _ExtinguishColor;
                half4  _ContainmentColor;
                half4  _CompletedColor;
                half4  _FailedColor;
                half   _TransitionSteps;
                half   _StepEdgeSoftness;
                half   _FillAlpha;
            CBUFFER_END

            struct Attributes { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 posHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.posHCS = TransformObjectToHClip(i.posOS.xyz);
                o.uv     = i.uv;
                return o;
            }

            half4 GetZoneColor(half missionType, half missionState)
            {
                if (missionState > 0.4 && missionState < 0.6)
                    return _CompletedColor;
                if (missionState > 0.9)
                    return _FailedColor;

                if (missionType > 0.4 && missionType < 0.6)
                    return _ExtinguishColor;
                if (missionType > 0.9)
                    return _ContainmentColor;

                return half4(0,0,0,0);
            }

            // FireOverlay'deki ComputeCellVisual gibi
            half4 ComputeMissionVisual(half4 data)
            {
                if (data.a < 0.01) return half4(0,0,0,0);

                half missionType  = data.r;
                half missionState = data.b;

                half4 zoneColor = GetZoneColor(missionType, missionState);
                if (zoneColor.a < 0.01) return half4(0,0,0,0);

                return half4(zoneColor.rgb, zoneColor.a * _FillAlpha);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // UV → tile index
                float2 local     = i.uv * _TexSize.xy;
                float2 samplePos = local - 0.5;
                float2 tileIndex = floor(samplePos);
                float2 f         = frac(samplePos);

                // 4 komşu tile UV
                float2 uv00 = (tileIndex + float2(0.5, 0.5)) / _TexSize.xy;
                float2 uv10 = (tileIndex + float2(1.5, 0.5)) / _TexSize.xy;
                float2 uv01 = (tileIndex + float2(0.5, 1.5)) / _TexSize.xy;
                float2 uv11 = (tileIndex + float2(1.5, 1.5)) / _TexSize.xy;

                // 4 komşu tile verisi
                half4 d00 = SAMPLE_TEXTURE2D(_MissionTex, sampler_MissionTex, uv00);
                half4 d10 = SAMPLE_TEXTURE2D(_MissionTex, sampler_MissionTex, uv10);
                half4 d01 = SAMPLE_TEXTURE2D(_MissionTex, sampler_MissionTex, uv01);
                half4 d11 = SAMPLE_TEXTURE2D(_MissionTex, sampler_MissionTex, uv11);

                // Hiçbirinde mission yok ise şeffaf
                if (d00.a < 0.01 && d10.a < 0.01 &&
                    d01.a < 0.01 && d11.a < 0.01)
                    return half4(0,0,0,0);

                // 4 tile rengi hesapla
                half4 c00 = ComputeMissionVisual(d00);
                half4 c10 = ComputeMissionVisual(d10);
                half4 c01 = ComputeMissionVisual(d01);
                half4 c11 = ComputeMissionVisual(d11);

                // Bilinear blend
                half4 blendedColor = lerp(
                    lerp(c00, c10, f.x),
                    lerp(c01, c11, f.x),
                    f.y
                );

                // Alpha geçiş değeri
                half alphaBlend = lerp(
                    lerp(d00.a, d10.a, f.x),
                    lerp(d01.a, d11.a, f.x),
                    f.y
                );

                // Basamaklı dalga (FireOverlay ile aynı)
                float t        = saturate(1.0 - alphaBlend);
                float stepped  = floor(t * _TransitionSteps) / _TransitionSteps;
                float stepFrac = frac(t * _TransitionSteps);
                float edge     = smoothstep(
                    0.5 - _StepEdgeSoftness * 0.5,
                    0.5 + _StepEdgeSoftness * 0.5,
                    stepFrac
                );
                float finalProgress = saturate(stepped + (edge / _TransitionSteps));
                half  waveAlpha     = smoothstep(1.0, 0.0, finalProgress);

                half4 finalColor = blendedColor;
                finalColor.a = blendedColor.a * waveAlpha;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}