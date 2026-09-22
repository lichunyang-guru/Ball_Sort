Shader "BallSort/WaterBodyReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _TrackMinimumV ("Main Track Lower Edge", Float) = 0
        [HideInInspector] _TrackMaskEnabled ("Track Mask Enabled", Float) = 0
        [HideInInspector] _TrackMask ("Track Mask", 2D) = "white" {}
        [HideInInspector] _TrackWorldToU ("Track World To U", Vector) = (1,0,0,0)
        [HideInInspector] _TrackWorldToV ("Track World To V", Vector) = (0,1,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _DropColorMatch ("Match Drop To Water Color", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [HideInInspector] _RevealProgress ("Reveal Progress", Float) = 1
        [HideInInspector] _RevealPlane ("Reveal Plane", Vector) = (0,1,0,100000)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float _TrackMinimumV;
            float _TrackMaskEnabled;
            sampler2D _TrackMask;
            float4 _TrackWorldToU;
            float4 _TrackWorldToV;
            float _DropColorMatch;
            float _RevealProgress;
            float4 _RevealPlane;
            struct RevealVaryings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            RevealVaryings Vert(appdata_t input)
            {
                RevealVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 position = UnityFlipSprite(input.vertex, _Flip);
                output.vertex = UnityObjectToClipPos(position);
                output.worldPosition = mul(unity_ObjectToWorld, position).xyz;
                output.texcoord = input.texcoord;
                output.color = input.color * _Color * _RendererColor;
                return output;
            }
            fixed4 Frag(RevealVaryings input) : SV_Target
            {
                clip(_RevealProgress - 0.00001);
                if (_RevealProgress < 1)
                    clip(_RevealPlane.w - dot(_RevealPlane.xyz, input.worldPosition));
                fixed4 texel = SampleSpriteTexture(input.texcoord);
                fixed4 color = texel * input.color;
                if (_DropColorMatch > 0.5)
                {
                    // 灰底只提供轻微明暗，水滴主体保持水柱的色相和亮度。
                    float gray = dot(texel.rgb, float3(0.299, 0.587, 0.114));
                    float shade = 0.82 + gray * 0.28;
                    color.rgb = saturate(input.color.rgb * shade);
                    float highlight = smoothstep(0.8, 1.0, gray) * 0.18;
                    color.rgb = lerp(color.rgb, float3(1, 1, 1), highlight);
                }
                if (_TrackMaskEnabled > 0.5)
                {
                    float4 world = float4(input.worldPosition, 1);
                    float2 maskUV = float2(dot(world, _TrackWorldToU), dot(world, _TrackWorldToV));
                    clip(maskUV);
                    clip(maskUV.y - _TrackMinimumV);
                    clip(1.0 - maskUV);
                    color.a *= tex2D(_TrackMask, maskUV).a;
                }
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
