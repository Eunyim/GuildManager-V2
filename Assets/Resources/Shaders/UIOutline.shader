// UI 이미지 외곽선 (SpriteOutline의 UI판). UIHoverOutline이 마우스를 올린 동안만 이 재질로 바꿈
// 그림 가장자리 칸과 바로 바깥 칸을 _OutlineColor로 칠함. UI 마스크(스텐실)·RectMask2D도 지원
Shader "GuildManager/UIOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1, 0.86, 0.25, 1)
        _OutlineWidth ("Outline Width (texels)", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float4 _ClipRect;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 world : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float AlphaAt(float2 uv)
            {
                if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return 0;
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                float2 d = _MainTex_TexelSize.xy * _OutlineWidth;
                float l = AlphaAt(i.uv + float2(-d.x, 0));
                float r = AlphaAt(i.uv + float2( d.x, 0));
                float u = AlphaAt(i.uv + float2(0,  d.y));
                float b = AlphaAt(i.uv + float2(0, -d.y));
                bool solid = tex2D(_MainTex, i.uv).a > 0.5;
                if ((solid && min(min(l, r), min(u, b)) < 0.5) || (!solid && max(max(l, r), max(u, b)) > 0.5))
                    c = fixed4(_OutlineColor.rgb, _OutlineColor.a);
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
