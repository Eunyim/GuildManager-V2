// 마우스를 올린 오브젝트의 외곽선을 한 색(기본 노랑)으로 칠하는 셰이더. InteractiveObject가 호버 동안만 이 재질로 바꿨다가 되돌림
// 그림 가장자리(불투명 칸 중 투명 칸과 맞닿은 칸)와 바로 바깥 칸을 _OutlineColor로 칠함 → 도트처럼 또렷한 테두리
Shader "GuildManager/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.86, 0.25, 1)
        _OutlineWidth ("Outline Width (texels)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 그림 바깥(0~1 밖)은 투명으로 봄
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

                bool solid = c.a > 0.5;
                bool edgeInside = solid && min(min(l, r), min(u, b)) < 0.5;  // 가장자리 칸
                bool edgeOutside = !solid && max(max(l, r), max(u, b)) > 0.5; // 바로 바깥 칸
                if (edgeInside || edgeOutside) return fixed4(_OutlineColor.rgb, _OutlineColor.a);
                return c;
            }
            ENDCG
        }
    }
}
