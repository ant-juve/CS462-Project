// Radioactive sludge: dark, murky water with drifting patches of dull scum, slow heavy ripples and
// faint green wisps glowing under the surface. Lit by URP's own PBR lighting (works with Forward+),
// so lamps and reflection probes show up in the wet parts. Reflection probes and ambient light are
// baked and don't change when lamps dim, so RadiationPuddle sets _LightLevel from the lamps around
// it to dim them, and the glow, along with the room.
// Used by RadiationPuddle; lives in Resources so it's included in builds.
Shader "Radiation/Sludge"
{
    Properties
    {
        _BaseColor ("Water Color", Color) = (0.018, 0.026, 0.016, 1)
        _Smoothness ("Water Smoothness", Range(0, 1)) = 0.88
        _Opacity ("Opacity", Range(0, 1)) = 0.97

        _ScumColor ("Scum Color", Color) = (0.045, 0.06, 0.025, 1)
        _ScumAmount ("Scum Amount", Range(0, 1)) = 0.6
        _ScumScale ("Scum Scale", Float) = 1.6
        _ScumSmoothness ("Scum Smoothness", Range(0, 1)) = 0.3
        _DriftSpeed ("Drift Speed", Float) = 0.02

        _SheenStrength ("Oily Film", Range(0, 1)) = 0.15

        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.08
        _RippleScale ("Ripple Scale", Float) = 1.5
        _RippleSpeed ("Ripple Speed", Float) = 0.05

        [HDR] _GlowColor ("Glow", Color) = (0.03, 0.12, 0.02, 1)
        _GlowPulse ("Glow Pulse", Range(0, 1)) = 0.35
        _GlowPulseSpeed ("Glow Pulse Speed", Float) = 0.4

        _LightLevel ("Light Level", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            // Specular workflow, so the oily film can color the reflections directly
            #define _SPECULAR_SETUP 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _Opacity;
                half4 _ScumColor;
                half _ScumAmount;
                float _ScumScale;
                half _ScumSmoothness;
                float _DriftSpeed;
                half _SheenStrength;
                half _RippleStrength;
                float _RippleScale;
                float _RippleSpeed;
                half4 _GlowColor;
                half _GlowPulse;
                float _GlowPulseSpeed;
                half _LightLevel;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR; // alpha fades the soft edge
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half alpha : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth value noise, 0 to 1
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Two octaves, for blotchier, more natural shapes than a single layer
            float Blotches(float2 p)
            {
                return Noise(p) * 0.65 + Noise(p * 2.3 + 7.1) * 0.35;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.alpha = input.color.a;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                float2 xz = positionWS.xz;
                float time = _Time.y;
                float drift = time * _DriftSpeed;

                // Scum: patches of duller film floating on the water, slowly drifting
                float scum = smoothstep(0.45, 0.72, Blotches(xz * _ScumScale + float2(drift, drift * 0.6))) * _ScumAmount;

                // Ripples: big, slow undulations, damped where the scum sits on top
                float2 ripple = xz * _RippleScale + float2(time, -time * 0.7) * _RippleSpeed;
                const float delta = 0.05;
                float height = Noise(ripple);
                float slopeX = (Noise(ripple + float2(delta, 0)) - height) / delta;
                float slopeZ = (Noise(ripple + float2(0, delta)) - height) / delta;
                float rippleAmount = _RippleStrength * 0.5 * (1.0 - scum * 0.8);
                float3 normalWS = normalize(input.normalWS + float3(-slopeX, 0, -slopeZ) * rippleAmount);

                half3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
                half NdotV = saturate(dot(normalWS, viewWS));

                // A hint of oily film on the open water: a rainbow tint to the reflections at grazing angles
                float thickness = Noise(xz * 1.3 + Noise(xz * 0.7 - drift) * 2.0);
                half3 film = 0.5 + 0.5 * cos(TWO_PI * (thickness * 2.0 + (1.0 - NdotV) * 0.6) + half3(0.0, 2.094, 4.189));
                half sheen = _SheenStrength * (1.0 - scum);

                // Radioactive glow: faint wisps under the surface, slowly breathing brighter and dimmer.
                // The scum covers it up
                float wisps = smoothstep(0.5, 0.85, Blotches(xz * 2.2 - float2(drift * 1.5, drift)));
                float pulse = 1.0 + _GlowPulse * sin(time * _GlowPulseSpeed * TWO_PI + Noise(xz * 0.5) * 3.0);
                half3 glow = _GlowColor.rgb * (0.35 + 0.65 * wisps) * pulse * (1.0 - scum * 0.75);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_BaseColor.rgb, _ScumColor.rgb, scum);
                surface.specular = lerp(half3(0.03, 0.03, 0.03), film * 0.15, sheen); // murky water reflects ~3%
                surface.smoothness = lerp(_Smoothness, _ScumSmoothness, scum);
                surface.occlusion = _LightLevel; // only darkens reflections and ambient light, not the lamps' own light
                surface.alpha = input.alpha * _Opacity;
                surface.emission = glow * _LightLevel; // fades with the room's lamps too

                InputData lighting = (InputData)0;
                lighting.positionWS = positionWS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = viewWS;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    lighting.shadowCoord = TransformWorldToShadowCoord(positionWS);
                #endif
                lighting.fogCoord = input.fogFactor;
                lighting.bakedGI = SampleSH(normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                color.a = surface.alpha;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
