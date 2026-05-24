Shader "Custom/FireOverlay"
{
    Properties
    {
        // C# (TileVisualCoordinator) tarafından beslenen ana simülasyon dokusu
        _StateTex  ("State Texture", 2D)    = "black" {}
        _TexSize   ("Tex Size",     Vector) = (1,1,0,0)

        [Header(Circular and Step Settings)]
        _TransitionSteps   ("Transition Steps", Range(1, 10)) = 3.0
        _StepEdgeSoftness  ("Step Edge Softness", Range(0.01, 1)) = 0.50

        [Header(Wet Wetness Settings)]
        _WetColor      ("Wet Color Grey Blue", Color)      = (0.22, 0.28, 0.35, 1)
        _WetIntensity  ("Wet Color Intensity", Range(0,2))    = 1.00
        _WetAlpha      ("Wet Minimum Opacity", Range(0,1))    = 0.05
        _MaxWetAlpha   ("Wet Maximum Opacity Cap", Range(0,1)) = 0.50
        _MaxDarkness   ("Max Darkness Factor", Range(0,1))   = 0.40
        _WetThreshold  ("Wetness Threshold", Range(0, 0.2))   = 0.10

        [Header(Heat Fire Colors)]
        _HeatColorLow  ("Flame Outer Color Low", Color)    = (1.0, 0.80, 0.10, 1)
        _HeatColorHigh ("Flame Inner Color High", Color)   = (1.0, 0.05, 0.00, 1)
        _HeatIntensity ("Fire Intensity", Range(0,2))        = 0.50

        [Header(Raging Intense Fire)]
        _RagingBoost   ("Raging White Boost", Range(0,2))    = 0.30

        [Header(Ash Settings)]
        _AshColor      ("Ash Color", Color)                  = (0.24, 0.24, 0.24, 1)
        _AshIntensity  ("Ash Base Opacity", Range(0,1))       = 0.3
        _AshAlphaBoost ("Ash Thickness Boost", Range(1,5))    = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent+10"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Yumuşak saydamlık harmanlaması
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

            TEXTURE2D(_StateTex);
            SAMPLER(sampler_StateTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _StateTex_ST;
                float4 _TexSize;
                half  _TransitionSteps;
                half  _StepEdgeSoftness;
                half4 _WetColor;
                half  _WetIntensity;
                half  _WetAlpha;
                half  _MaxWetAlpha;
                half  _MaxDarkness;
                half  _WetThreshold;
                half4 _HeatColorLow;
                half4 _HeatColorHigh;
                half  _HeatIntensity;
                half  _RagingBoost;
                half4 _AshColor;
                half  _AshIntensity;
                half  _AshAlphaBoost;
            CBUFFER_END

            // C# tarafındaki FireState enum sabitlerinin Mavi (B) kanalındaki karşılıkları
            #define B_IGNITING 0.20
            #define B_BURNING  0.40
            #define B_RAGING   0.60
            #define B_ASH      0.80
            #define B_WET      1.00
            #define B_EPS      0.08 // Float sapmaları için tolerans payı

            struct Attributes { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 posHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.posHCS = TransformObjectToHClip(i.posOS.xyz);
                o.uv     = i.uv;
                return o;
            }

            /*
              FONKSİYON: ComputeCellVisual
              Mevcut hücrenin ham doku verisini (R:Heat, G:Wetness, B:State) yorumlar.
              Hücrenin saf rengini üretir ve komşu piksellerle harmanlanırken kullanılacak olan
              durum maskelerini (isAsh, isWet) dışarı aktarır (out).
            */
            void ComputeCellVisual(half4 stateData, out half4 cellColor, out half isAsh, out half isWet)
            {
                cellColor = half4(0,0,0,0);
                isAsh = 0.0;
                isWet = 0.0;

                // Eğer haritada bu hücre yoksa işlem yapma
                if (stateData.a < 0.01) return;

                half heat    = stateData.r;
                half wetness = stateData.g;
                half stateB  = stateData.b;

                // 1. DURUM: Islaklık / Söndürülmüş Katmanı (Öncelikli Katman)
                if (wetness > _WetThreshold)
                {
                    isWet = 1.0;
                    half darknessFactor = lerp(1.0, 1.0 - _MaxDarkness, saturate(wetness));
                    half3 finalWetColor = _WetColor.rgb * _WetIntensity * darknessFactor;
                    half rawAlpha = _WetAlpha + (wetness * (_MaxWetAlpha - _WetAlpha));
                    cellColor = half4(finalWetColor, clamp(rawAlpha, _WetAlpha, _MaxWetAlpha));
                    return;
                }

                // 2. DURUM: Kül Katmanı (Kalıcı olması gereken katman)
                if (stateB > B_ASH - B_EPS && stateB < B_ASH + B_EPS)
                {
                    isAsh = 1.0;
                    half finalAlpha = saturate(_AshIntensity * _AshAlphaBoost);
                    cellColor = half4(_AshColor.rgb, finalAlpha);
                    return;
                }

                // 3. DURUM: Yangın / Alev Katmanı (Dinamik Isı Tabanlı)
                if (heat > 0.01)
                {
                    // Isı şiddetine göre dış alev ile iç alev rengini harmanla
                    half3 heatColor = lerp(_HeatColorLow.rgb, _HeatColorHigh.rgb, heat);
                    
                    // Eğer RAGING durumundaysa alevin merkezine beyaz/sarı kor parlaklığı ekle
                    if (stateB > B_RAGING - B_EPS && stateB < B_ASH - B_EPS)
                        heatColor = lerp(heatColor, half3(1,1,0.8), _RagingBoost * heat);

                    // Tutuşma (IGNITING) aşamasındaysa hafif transparan başla, BURNING ise tam opak yap
                    half alphaMultiplier = (stateB < B_BURNING - B_EPS) ? 0.4 : 1.0;
                    cellColor = half4(heatColor, heat * _HeatIntensity * alphaMultiplier);
                }
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Pikselin doku üzerindeki tam yerini hesapla (0.5 çıkararak hücre merkezlerine odaklanıyoruz)
                float2 local = i.uv * _TexSize.xy;
                float2 samplePos = local - 0.5;
                float2 tileIndex = floor(samplePos);
                float2 f = frac(samplePos); // Hücre içi yumuşak geçiş çarpanı (0 ile 1 arası)

                // Pikseli çevreleyen en yakın 4 karo merkezinin UV koordinatları
                float2 uv00 = (tileIndex + float2(0.5, 0.5)) / _TexSize.xy;
                float2 uv10 = (tileIndex + float2(1.5, 0.5)) / _TexSize.xy;
                float2 uv01 = (tileIndex + float2(0.5, 1.5)) / _TexSize.xy;
                float2 uv11 = (tileIndex + float2(1.5, 1.5)) / _TexSize.xy;

                // 4 merkeze ait simülasyon verilerini dokudan oku
                half4 d00 = SAMPLE_TEXTURE2D(_StateTex, sampler_StateTex, uv00);
                half4 d10 = SAMPLE_TEXTURE2D(_StateTex, sampler_StateTex, uv10);
                half4 d01 = SAMPLE_TEXTURE2D(_StateTex, sampler_StateTex, uv01);
                half4 d11 = SAMPLE_TEXTURE2D(_StateTex, sampler_StateTex, uv11);

                // 4 karonun da saf renklerini ve durum maskelerini ayrı ayrı çıkart
                half4 c00, c10, c01, c11;
                half a00, a10, a01, a11;
                half w00, w10, w01, w11;

                ComputeCellVisual(d00, c00, a00, w00);
                ComputeCellVisual(d10, c10, a10, w10);
                ComputeCellVisual(d01, c01, a01, w01);
                ComputeCellVisual(d11, c11, a11, w11);

                // ADIM 1: RENKLERİ VE MASKELERİ BİLİNEAR (ÇİFT DOĞRUSAL) OLARAK YAY
                // Bu sayede pikseller izometrik sınır çizgilerinde yırtılmaz ve jilet gibi pürüzsüz harmanlanır
                half4 blendedColor = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);

                half ashBlend  = lerp(lerp(a00, a10, f.x), lerp(a01, a11, f.x), f.y);
                half heatBlend = lerp(lerp(d00.r, d10.r, f.x), lerp(d01.r, d11.r, f.x), f.y);
                half wetBlend  = lerp(lerp(d00.g, d10.g, f.x), lerp(d01.g, d11.g, f.x), f.y);

                // ADIM 2: DAİRESEL SÖNÜMLENME MASKESİ (Dış Dalga Genişlemesi)
                // Yangın veya su dalgasının dış cephesini şekillendirecek baskın gücü bul
                half transitionValue = max(heatBlend, wetBlend);
                
                // Aktif yanan karoların stabil iç bölgelerinde dalganın erkenden sönmesini engellemek için taban koruması
                if (transitionValue < 0.05 && (d00.b > 0.1 || d10.b > 0.1 || d01.b > 0.1 || d11.b > 0.1))
                {
                    transitionValue = 0.4;
                }

                // Yoğunluk gücünü tersine çevirerek merkezden dışarıya doğru azalan bir mesafe (t) elde et
                float t = saturate(1.0 - transitionValue);

                // ADIM 3: STİLİZE KADEMELİ BASAMAKLAMA MATEMATİĞİ (Quantization)
                // Kesintisiz pürüzsüz dalgayı, materyaldeki basamak sayısına göre dilimlere ayırır (Posterize/Cel-Shaded efekti)
                float stepped = floor(t * _TransitionSteps) / _TransitionSteps;
                float stepFrac = frac(t * _TransitionSteps);
                
                // Dilimlerin sınır çizgilerini yumuşatmak için kenar geçiş yumuşaklığı süzgeci
                float edge = smoothstep(0.5 - _StepEdgeSoftness * 0.5, 0.5 + _StepEdgeSoftness * 0.5, stepFrac);
                float finalProgress = saturate(stepped + (edge / _TransitionSteps));

                // ADIM 4: NİHAİ OPALIK VE KÜL KORUMA FİLTRESİ
                half4 finalColor = blendedColor;

                // Dinamik dalga maskesi (Dış cephede yumuşak basamaklı sönümlenme sağlar)
                half waveAlpha = smoothstep(1.0, 0.0, finalProgress);

                /*
                  KÜL KORUMASI KRİTİK NOKTASI:
                  Simülasyonda kül olan yerlerin ısısı sıfıra düşer. Isı sıfırlandığı için normalde waveAlpha 
                  bu alanları görünmez yapacaktı. Biz ashBlend maskesini devreye sokarak küllerin ısıdan bağımsız,
                  haritada kalıcı bir katman olarak mat bir şekilde parlamaya devam etmesini sağlıyoruz.
                */
                finalColor.a *= lerp(waveAlpha, 1.0, saturate(ashBlend * 1.5));

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}