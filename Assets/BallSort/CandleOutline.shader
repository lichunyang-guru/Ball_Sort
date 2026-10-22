Shader "BallSort/CandleOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width (Screen Pixels)", Float) = 1.5
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment OutlineFrag
            #pragma target 3.0
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _OutlineWidth;

            fixed4 OutlineFrag(v2f input) : SV_Target
            {
                fixed4 body = SampleSpriteTexture(input.texcoord);
                float sampleSpacing = max(0, _OutlineWidth) / 3.0;
                float2 pixelX = ddx(input.texcoord) * sampleSpacing;
                float2 pixelY = ddy(input.texcoord) * sampleSpacing;
                fixed expandedAlpha = body.a;
                // Scale the circular sampling kernel to the configured screen-pixel radius.
                [unroll] for (int y = -3; y <= 3; y++)
                {
                    [unroll] for (int x = -3; x <= 3; x++)
                    {
                        if (x * x + y * y <= 9)
                        {
                            float2 uv = input.texcoord + pixelX * x + pixelY * y;
                            expandedAlpha = max(expandedAlpha, SampleSpriteTexture(uv).a);
                        }
                    }
                }
                fixed outlineAlpha = max(0, expandedAlpha - body.a);
                fixed3 rgb = body.rgb * input.color.rgb * body.a + fixed3(1,1,1) * outlineAlpha;
                return fixed4(rgb * input.color.a, expandedAlpha * input.color.a);
            }
            ENDCG
        }
    }
}
