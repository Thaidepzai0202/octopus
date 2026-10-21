// Sprite của 3 nhân vật người (URP 2D, không chịu ánh sáng; giống Sprite-Unlit-Default) thêm 3 thứ HeroRigAnimator chỉnh
// mỗi frame (mỗi nhân vật 2 vật liệu riêng: thân và vũ khí):
//  _Flash      chớp khi trúng đòn: rgb = màu chớp, a = mức phủ (0 = không chớp)
//  _Ghost      0 = bình thường, 1 = trong mờ như bóng ma (ngả xanh theo _GhostColor)
//  _Glow       vũ khí rực lên (0..1): chỗ màu rực của tranh (lưỡi kiếm đỏ, pha lê tím, lửa xanh) sáng bừng theo _GlowColor
//  _Clip       0..1: không vẽ phần dưới mặt sàn _ClipY (world y): vũ khí quệt đất / kiếm cắm xuống đất không xuyên qua sàn
//  _SrcBlend / _DstBlend: cách trộn (mặc định trộn thường; quầng sáng vũ khí dùng cộng sáng SrcAlpha + One)
Shader "Octopus/Hero Sprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Flash ("Flash (rgb màu, a mức)", Color) = (1, 1, 1, 0)
        _Ghost ("Ghost", Range(0, 1)) = 0
        _GhostColor ("Ghost Color", Color) = (0.45, 0.75, 1, 1)
        _Glow ("Glow", Range(0, 1)) = 0
        _GlowColor ("Glow Color", Color) = (1, 0.2, 0.15, 1)
        _Clip ("Clip", Range(0, 1)) = 0
        _ClipY ("Clip Y (world)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex HeroVertex
            #pragma fragment HeroFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float worldY : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Flash;
                half _Ghost;
                half4 _GhostColor;
                half _Glow;
                half4 _GlowColor;
                half _Clip;
                float _ClipY;
            CBUFFER_END

            Varyings HeroVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.worldY = TransformObjectToWorld(input.positionOS).y;
                return o;
            }

            half4 HeroFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 rgb = tex.rgb;
                // Chỗ màu rực (bão hoà + sáng): lưỡi kiếm đỏ, pha lê, lửa
                half hi = max(rgb.r, max(rgb.g, rgb.b));
                half lo = min(rgb.r, min(rgb.g, rgb.b));
                half hot = saturate((hi - lo) * 2.2h - 0.35h) * smoothstep(0.25h, 0.75h, hi);
                rgb += _GlowColor.rgb * hot * _Glow * 1.4h + rgb * _Glow * 0.2h;
                half4 c = half4(rgb, tex.a) * input.color;
                // Bóng ma: ngả xanh lạnh, sáng viền nhẹ, trong mờ
                half3 ghost = c.rgb * _GhostColor.rgb * 1.15h + _GhostColor.rgb * 0.12h;
                c.rgb = lerp(c.rgb, ghost, _Ghost);
                c.a *= 1.0h - 0.62h * _Ghost;
                c.a *= lerp(1.0h, (half)saturate((input.worldY - _ClipY) / 0.03), _Clip);
                c.rgb = lerp(c.rgb, _Flash.rgb, _Flash.a);
                return c;
            }
            ENDHLSL
        }
    }
}
