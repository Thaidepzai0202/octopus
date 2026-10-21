// Sprite của boss sư tử (URP 2D, không chịu ánh sáng; giống Sprite-Unlit-Default) thêm 2 thứ LionRigAnimator chỉnh mỗi frame:
//  _Flash      chớp khi trúng đòn: rgb = màu chớp, a = mức phủ (0 = không chớp, 1 = trắng / đỏ đặc cả hình)
//  _Ember      độ rực của các chỗ màu đỏ (bờm, vết nứt, móng, mắt): 1 = như tranh, > 1 = nổi giận rực sáng thêm
//              theo _EmberColor, < 1 = lửa tắt dần (chỗ đỏ ngả sang tro xám tối) khi chết
Shader "Octopus/Lion Sprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Flash ("Flash (rgb màu, a mức)", Color) = (1, 1, 1, 0)
        _Ember ("Ember", Float) = 1
        _EmberColor ("Ember Color", Color) = (1, 0.32, 0.12, 1)
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

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LionVertex
            #pragma fragment LionFragment

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
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Flash;
                half _Ember;
                half4 _EmberColor;
            CBUFFER_END

            Varyings LionVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LionFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 rgb = tex.rgb;
                // Chỗ đỏ rực của tranh (đỏ hơn hẳn xanh lá / xanh dương)
                half red = saturate((rgb.r - max(rgb.g, rgb.b) - 0.12h) * 3.0h);
                half ember = _Ember;
                half grey = dot(rgb, half3(0.3h, 0.45h, 0.25h));
                half3 ash = half3(grey, grey * 0.92h, grey * 0.9h) * 0.55h;
                rgb = lerp(rgb, ash, red * saturate(1.0h - ember));
                rgb += _EmberColor.rgb * red * max(ember - 1.0h, 0.0h) * (0.4h + rgb.r);
                half4 c = half4(rgb, tex.a) * input.color;
                c.rgb = lerp(c.rgb, _Flash.rgb, _Flash.a);
                return c;
            }
            ENDHLSL
        }
    }
}
