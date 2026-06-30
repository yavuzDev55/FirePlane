Shader "Custom/BiomeOverlay"
{
    Properties
    {
        _BiomeTex      ("Biome Color Texture", 2D)      = "black" {}
        _BiomeIndexTex ("Biome Index Texture", 2D)      = "black" {}
        _BiomeTexArray ("Biome Texture Array", 2DArray) = "" {}
        _TexSize       ("Tex Size",           Vector)   = (1,1,0,0)
        _MaxBiomes     ("Max Biomes",         Float)    = 8.0
        
        [Header(Stylized Transition Settings)]
        _GlobalIntensity  ("Global Intensity",   Range(0,1))    = 0.8
        _TransitionSteps  ("Transition Steps",   Range(1,10))   = 3.0
        _StepEdgeSoftness ("Step Edge Softness", Range(0.01,1)) = 0.5
        
        [Header(Texture Control)]
        _TextureScale     ("Texture Tiling (Scale)", Float)     = 10.0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
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

            TEXTURE2D(_BiomeTex);
            SAMPLER(sampler_BiomeTex);

            TEXTURE2D(_BiomeIndexTex);
            SAMPLER(sampler_BiomeIndexTex);

            TEXTURE2D_ARRAY(_BiomeTexArray);
            SAMPLER(sampler_BiomeTexArray);

            CBUFFER_START(UnityPerMaterial)
                float4 _BiomeTex_ST;
                float4 _TexSize;
                half   _MaxBiomes;
                half   _GlobalIntensity;
                half   _TransitionSteps;
                half   _StepEdgeSoftness;
                float  _TextureScale;
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

            // Hücre rengini alan yardımcı fonksiyon (Gradyanlar ile çizgi yırtılması engellendi)
            half4 GetCellColor(half4 colorData, float2 uvCoords, float2 dx, float2 dy, float texIndex)
            {
                if (colorData.a < 0.01) return half4(0,0,0,0);
                
                // GRAD versiyonu sınır çizgilerindeki mipmap tırtıklarını yok eder
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY_GRAD(_BiomeTexArray, sampler_BiomeTexArray, uvCoords, texIndex, dx, dy);
                
                return half4(colorData.rgb * texColor.rgb, colorData.a);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // FireOverlay Bilinear Örnekleme Alanı
                float2 local = i.uv * _TexSize.xy;
                float2 samplePos = local - 0.5;
                float2 tileIndex = floor(samplePos);
                float2 f = frac(samplePos);

                float2 uv00 = (tileIndex + float2(0.5, 0.5)) / _TexSize.xy;
                float2 uv10 = (tileIndex + float2(1.5, 0.5)) / _TexSize.xy;
                float2 uv01 = (tileIndex + float2(0.5, 1.5)) / _TexSize.xy;
                float2 uv11 = (tileIndex + float2(1.5, 1.5)) / _TexSize.xy;

                half4 color00 = SAMPLE_TEXTURE2D(_BiomeTex, sampler_BiomeTex, uv00);
                half4 color10 = SAMPLE_TEXTURE2D(_BiomeTex, sampler_BiomeTex, uv10);
                half4 color01 = SAMPLE_TEXTURE2D(_BiomeTex, sampler_BiomeTex, uv01);
                half4 color11 = SAMPLE_TEXTURE2D(_BiomeTex, sampler_BiomeTex, uv11);

                half4 idx00 = SAMPLE_TEXTURE2D(_BiomeIndexTex, sampler_BiomeIndexTex, uv00);
                half4 idx10 = SAMPLE_TEXTURE2D(_BiomeIndexTex, sampler_BiomeIndexTex, uv10);
                half4 idx01 = SAMPLE_TEXTURE2D(_BiomeIndexTex, sampler_BiomeIndexTex, uv01);
                half4 idx11 = SAMPLE_TEXTURE2D(_BiomeIndexTex, sampler_BiomeIndexTex, uv11);

                float t00 = floor(idx00.r * _MaxBiomes + 0.5);
                float t10 = floor(idx10.r * _MaxBiomes + 0.5);
                float t01 = floor(idx01.r * _MaxBiomes + 0.5);
                float t11 = floor(idx11.r * _MaxBiomes + 0.5);

                // Dokunun döşenmesi için çalışan UV koordinatı
                float2 arrayUV = i.uv * _TextureScale;

                // Frac öncesi sınır çizgilerindeki patlamaları önlemek için gradyanları alıyoruz
                float2 dx = ddx(arrayUV);
                float2 dy = ddy(arrayUV);
                arrayUV = frac(arrayUV); 

                // 4 Hücrenin renk ve doku harmanlaması
                half4 c00 = GetCellColor(color00, arrayUV, dx, dy, t00);
                half4 c10 = GetCellColor(color10, arrayUV, dx, dy, t10);
                half4 c01 = GetCellColor(color01, arrayUV, dx, dy, t01);
                half4 c11 = GetCellColor(color11, arrayUV, dx, dy, t11);

                half4 blendedColor = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
                half alphaBlend = lerp(lerp(color00.a, color10.a, f.x), lerp(color01.a, color11.a, f.x), f.y);
                
                if (alphaBlend < 0.01) return half4(0,0,0,0);

                // FireOverlay Kademeli Dalga Sistemi
                float t = saturate(1.0 - alphaBlend);
                float stepped = floor(t * _TransitionSteps) / _TransitionSteps;
                float stepFrac = frac(t * _TransitionSteps);
                
                float edge = smoothstep(0.5 - _StepEdgeSoftness * 0.5, 0.5 + _StepEdgeSoftness * 0.5, stepFrac);
                float finalProgress = saturate(stepped + (edge / _TransitionSteps));

                half waveAlpha = smoothstep(1.0, 0.0, finalProgress);
                blendedColor.a *= waveAlpha * _GlobalIntensity;

                return blendedColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}